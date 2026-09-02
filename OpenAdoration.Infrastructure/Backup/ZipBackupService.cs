using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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

    public async Task CreateAsync(string destinationPath, string? password = null, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var migrationId = (await ctx.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault()
            ?? throw new InvalidOperationException("Database has no applied migrations to back up.");

        var tempDb  = Path.Combine(Path.GetTempPath(), $"oabak-{Guid.NewGuid():N}.db");
        var tempZip = Path.Combine(Path.GetTempPath(), $"oabak-zip-{Guid.NewGuid():N}.tmp");
        try
        {
            SqliteSnapshot.Create(_paths.DbPath, tempDb);

            var manifest = new BackupManifest
            {
                AppVersion   = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                CreatedAtUtc = DateTime.UtcNow,
                MigrationId  = migrationId
            };

            BackupArchive.Pack(tempZip, tempDb, _paths.SettingsPath, _paths.MediaDirectory, manifest);

            if (string.IsNullOrEmpty(password))
            {
                File.Copy(tempZip, destinationPath, overwrite: true);
            }
            else
            {
                var zipBytes = await File.ReadAllBytesAsync(tempZip, ct);
                await File.WriteAllBytesAsync(destinationPath, BackupEncryption.Encrypt(zipBytes, password), ct);
            }

            _logger.LogInformation("Backup created at {Path} (encrypted: {Encrypted})", destinationPath, !string.IsNullOrEmpty(password));
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    public async Task<RestoreResult> RestoreAsync(string sourcePath, string? password = null, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var known = ctx.Database.GetMigrations().ToList();

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(sourcePath, ct);
            byte[] zipBytes;

            if (BackupEncryption.IsEncrypted(fileBytes))
            {
                if (string.IsNullOrEmpty(password))
                    return new RestoreResult(RestoreOutcome.PasswordRequired, MessageFor(RestoreOutcome.PasswordRequired));

                try
                {
                    zipBytes = BackupEncryption.Decrypt(fileBytes, password);
                }
                catch (CryptographicException)
                {
                    return new RestoreResult(RestoreOutcome.WrongPassword, MessageFor(RestoreOutcome.WrongPassword));
                }
            }
            else
            {
                zipBytes = fileBytes;
            }

            using var zipStream = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

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

    private static string MessageFor(RestoreOutcome outcome) => outcome switch
    {
        RestoreOutcome.NeedsNewerApp    => "This backup was made by a newer version of OpenAdoration. Update the app, then restore.",
        RestoreOutcome.Corrupt          => "This file isn't a valid OpenAdoration backup, or it's damaged.",
        RestoreOutcome.PasswordRequired => "This backup is password-protected. Enter the password to restore it.",
        RestoreOutcome.WrongPassword    => "That password is incorrect.",
        _                               => string.Empty
    };
}
