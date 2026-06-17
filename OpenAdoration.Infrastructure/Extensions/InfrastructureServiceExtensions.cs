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

        return services;
    }

    /// <summary>
    /// Applies any pending EF Core migrations.
    /// Safe to call on every launch — no-op when the schema is up to date.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
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
