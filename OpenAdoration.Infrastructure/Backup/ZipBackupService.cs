using System.IO.Compression;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Backup;

/// <summary>
/// Bundles the SQLite database (consistent online-backup snapshot), the media folder and
/// <c>settings.json</c> into one <c>.oabak</c>; restore stages the DB for swap-in on the
/// next launch. Restore refuses a backup whose schema migration this app doesn't know.
/// </summary>
public sealed class ZipBackupService : IBackupService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AppPaths _paths;
    private readonly ILogger<ZipBackupService> _logger;

    public ZipBackupService(
        IDbContextFactory<AppDbContext> factory, AppPaths paths, ILogger<ZipBackupService> logger)
    {
        _factory = factory;
        _paths = paths;
        _logger = logger;
    }

    public async Task CreateAsync(string destinationPath, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var migrationId = (await ctx.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault()
            ?? throw new InvalidOperationException("Database has no applied migrations to back up.");

        var tempDb = Path.Combine(Path.GetTempPath(), $"oabak-{Guid.NewGuid():N}.db");
        try
        {
            SnapshotDatabase(_paths.DbPath, tempDb);

            var manifest = new BackupManifest
            {
                AppVersion   = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                CreatedAtUtc = DateTime.UtcNow,
                MigrationId  = migrationId
            };

            BackupArchive.Pack(destinationPath, tempDb, _paths.SettingsPath, _paths.MediaDirectory, manifest);
            _logger.LogInformation("Backup created at {Path}", destinationPath);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }

    public async Task<RestoreResult> RestoreAsync(string sourcePath, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var known = ctx.Database.GetMigrations().ToList();

        try
        {
            using var zip = ZipFile.OpenRead(sourcePath);
            var outcome = BackupArchive.Evaluate(BackupArchive.ReadManifest(zip), known);
            if (outcome != RestoreOutcome.Compatible)
                return new RestoreResult(outcome, MessageFor(outcome));

            BackupArchive.Unpack(zip, _paths.DbPath + ".restore", _paths.SettingsPath, _paths.MediaDirectory);
            _logger.LogInformation("Backup staged for restore from {Path}", sourcePath);
            return new RestoreResult(RestoreOutcome.Compatible,
                "Backup restored. OpenAdoration will close — reopen it to finish applying the restored library.");
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Restore failed: corrupt backup {Path}", sourcePath);
            return new RestoreResult(RestoreOutcome.Corrupt, MessageFor(RestoreOutcome.Corrupt));
        }
    }

    // Online backup API: a consistent copy even while the app holds the DB open (handles WAL).
    private static void SnapshotDatabase(string dbPath, string destinationPath)
    {
        using var source = new SqliteConnection($"Data Source={dbPath}");
        using var destination = new SqliteConnection($"Data Source={destinationPath}");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static string MessageFor(RestoreOutcome outcome) => outcome switch
    {
        RestoreOutcome.NeedsNewerApp => "This backup was made by a newer version of OpenAdoration. Update the app, then restore.",
        RestoreOutcome.Corrupt       => "This file isn't a valid OpenAdoration backup, or it's damaged.",
        _                            => string.Empty
    };
}
