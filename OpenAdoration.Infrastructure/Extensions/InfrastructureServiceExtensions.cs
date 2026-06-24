using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Application.Services;
using OpenAdoration.Infrastructure.Backup;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using OpenAdoration.Infrastructure.Settings;
using OpenAdoration.Infrastructure.Update;

namespace OpenAdoration.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers the full infrastructure and application service stack.
    /// Call once from the WPF app's composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string dbPath, string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);

        EnsureDirectoryExists(dbPath);

        // Database
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories (scoped — new instance per logical operation scope)
        services.AddScoped<ISongRepository, SongRepository>();
        services.AddScoped<IWorshipServiceRepository, WorshipServiceRepository>();
        services.AddScoped<IBibleRepository, BibleRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<IThemeRepository, ThemeRepository>();

        // Application services
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IBibleService, BibleService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<IWorshipServiceService, WorshipServiceService>();

        // Projection is singleton — owns the live session state for the app's lifetime
        services.AddSingleton<IProjectionService, ProjectionService>();

        // App settings — singleton, loads the JSON file once at construction
        services.AddSingleton<IAppSettingsService>(sp =>
            new AppSettingsService(settingsPath, sp.GetRequiredService<ILogger<AppSettingsService>>()));

        // Token resolution: singleton; reads church tokens from IAppSettingsService
        services.AddSingleton<ITokenResolver, TokenResolver>();

        // Where user data lives (DB + media + settings), for file-level features like backup.
        var mediaDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "Media");
        services.AddSingleton(new AppPaths(dbPath, mediaDir, settingsPath));
        services.AddScoped<IBackupService, ZipBackupService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        return services;
    }

    /// <summary>
    /// Applies any pending EF Core migrations. Safe to call on every launch — no-op when the
    /// schema is up to date. Before a schema change it snapshots the DB to <c>{db}.oabak.auto</c>
    /// (G26) and rolls back to it in place if the migration throws, so a failed auto-update
    /// migration can't leave a user's library half-migrated.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services)
    {
        var dbPath = services.GetRequiredService<AppPaths>().DbPath;

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await context.Database.GetPendingMigrationsAsync();
        if (!pending.Any()) return;

        // Only existing DBs can be rolled back; a first run has nothing to lose.
        var dbExists = File.Exists(dbPath);
        var snapshot = dbPath + ".oabak.auto";
        if (dbExists) SqliteSnapshot.Create(dbPath, snapshot);

        try
        {
            await context.Database.MigrateAsync();
        }
        catch
        {
            if (dbExists)
            {
                // Release EF's file handle, then restore the pre-migration copy in place.
                await context.DisposeAsync();
                SqliteConnection.ClearAllPools();
                File.Copy(snapshot, dbPath, overwrite: true);
            }
            throw;
        }
    }

    /// <summary>
    /// Adopts store-resident theme backgrounds that predate the Background media category into the
    /// library so they show in the Backgrounds subsection + picker. Best-effort and additive-only:
    /// any failure is logged and swallowed — it never blocks startup or touches existing data, so an
    /// auto-updated user keeps everything they had set up. Idempotent (no-op once adopted).
    /// </summary>
    public static async Task ReconcileBackgroundsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BackgroundReconcile");
        try
        {
            var themes = await scope.ServiceProvider.GetRequiredService<IThemeService>().GetAllAsync();
            var paths  = themes
                .SelectMany(t => new[] { t.BackgroundImagePath, t.BackgroundVideoPath })
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!);

            await scope.ServiceProvider.GetRequiredService<IMediaService>().ReconcileBackgroundsAsync(paths);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Background reconcile skipped (non-fatal) — existing data untouched");
        }
    }

    private static void EnsureDirectoryExists(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);

        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(
                $"Cannot determine parent directory for database path: '{dbPath}'", nameof(dbPath));

        Directory.CreateDirectory(directory);
    }
}
