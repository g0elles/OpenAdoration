using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Infrastructure.Extensions;
using OpenAdoration.Infrastructure.Logging;
using OpenAdoration.WPF.ViewModels;

// 'Application' alone is ambiguous with the OpenAdoration.Application namespace —
// alias it so the compiler resolves to System.Windows.Application unambiguously.
using WpfApp = System.Windows.Application;

namespace OpenAdoration.WPF;

public partial class App : WpfApp
{
    private IHost _host = null!;
    private ILogger<App> _logger = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenAdoration");

        var dbPath  = Path.Combine(appDataDir, "openadoration.db");
        var logDir  = Path.Combine(appDataDir, "logs");

        // Logging must be configured before the host is built
        LoggingConfiguration.Configure(logDir);

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.UseOpenAdorationSerilog())
            .ConfigureServices(services =>
            {
                services.AddInfrastructure(dbPath);
                RegisterViewModels(services);
                RegisterWindows(services);
            })
            .Build();

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("OpenAdoration starting up");

        try
        {
            await _host.Services.InitialiseDatabaseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database initialisation failed — cannot continue");
            System.Windows.MessageBox.Show(
                $"The database could not be initialised:\n\n{ex.Message}\n\nCheck the log files at:\n{logDir}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _logger.LogInformation("Startup complete");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("OpenAdoration shutting down");

        LoggingConfiguration.CloseAndFlush();

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SongsViewModel>();
        services.AddTransient<AddEditSongViewModel>();
        services.AddTransient<BibleViewModel>();
        services.AddTransient<ServiceScheduleViewModel>();
        services.AddTransient<MediaViewModel>();
        services.AddTransient<ThemeViewModel>();
    }

    private static void RegisterWindows(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ProjectionWindow>();
    }
}
