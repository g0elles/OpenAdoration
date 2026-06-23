using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Infrastructure.Extensions;
using OpenAdoration.Infrastructure.Logging;
using OpenAdoration.WPF.Localization;
using OpenAdoration.WPF.Plugins;
using OpenAdoration.WPF.Services;
using OpenAdoration.WPF.ViewModels;

// 'Application' alone is ambiguous with the OpenAdoration.Application namespace —
// alias it so the compiler resolves to System.Windows.Application unambiguously.
using Serilog;
using WpfApp = System.Windows.Application;

namespace OpenAdoration.WPF;

public partial class App : WpfApp
{
    private IHost _host = null!;
    private ILogger<App> _logger = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // OA_DATA_DIR overrides the data location (e2e isolation, portable installs);
        // falls back to %LOCALAPPDATA%\OpenAdoration.
        var appDataDir = Environment.GetEnvironmentVariable("OA_DATA_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAdoration");

        var dbPath       = Path.Combine(appDataDir, "openadoration.db");
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        var logDir       = Path.Combine(appDataDir, "logs");

        // Logging must be configured before the host is built
        LoggingConfiguration.Configure(logDir);

        // Apply a pending backup-restore (DB staged by IBackupService) before anything opens
        // the database. The live DB can't be swapped while in use, so it's done here at startup.
        ApplyPendingRestore(dbPath);

        // ── Global exception handlers (L10) ───────────────────────────────────
        // Catch unhandled exceptions on the WPF dispatcher, unobserved Task
        // exceptions, and CLR-level unhandled exceptions. Log them all so
        // crashes leave a trace even without a debugger attached.
        DispatcherUnhandledException += (_, ex) =>
        {
            var recoverable = IsRecoverable(ex.Exception);

            _logger?.LogCritical(ex.Exception, "Unhandled dispatcher exception (recoverable={Recoverable})", recoverable);
            Log.Fatal(ex.Exception, "Unhandled dispatcher exception (static sink)");

            // Reset projection in both paths so the display is never left in an unknown state.
            try
            {
                _host?.Services.GetService<IProjectionService>()?.Stop();
            }
            catch { /* best-effort — swallow to avoid re-entering this handler */ }

            if (recoverable)
            {
                System.Windows.MessageBox.Show(
                    string.Format(TranslationSource.Instance["App_ErrRecoverable"], logDir),
                    TranslationSource.Instance["App_ErrRecoverableTitle"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ex.Handled = true;
                return;
            }

            // Unknown failure type — application state may be corrupt. Inform and let WPF terminate
            // (ex.Handled stays false) rather than silently continuing in a bad state.
            System.Windows.MessageBox.Show(
                string.Format(TranslationSource.Instance["App_ErrCritical"], logDir),
                TranslationSource.Instance["App_ErrCriticalTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            _logger?.LogError(ex.Exception, "Unobserved task exception");
            ex.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            _logger?.LogCritical(ex.ExceptionObject as Exception,
                "Unhandled AppDomain exception (IsTerminating={IsTerminating})",
                ex.IsTerminating);
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.UseOpenAdorationSerilog())
            .ConfigureServices(services =>
            {
                services.AddInfrastructure(dbPath, settingsPath);
                RegisterViewModels(services);
                RegisterWindows(services);
            })
            .Build();

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("OpenAdoration starting up");

        // Load the FFmpeg media engine so the projector can decode any codec (incl. HEVC).
        // Non-fatal: if FFmpeg is missing the app still runs; only video playback is affected.
        Helpers.MediaEngine.Initialize(_logger);
        WarnIfVcRuntimeMissing();

        try
        {
            await _host.Services.InitialiseDatabaseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database initialisation failed — cannot continue");
            System.Windows.MessageBox.Show(
                string.Format(TranslationSource.Instance["App_ErrDbInit"], ex.Message, logDir),
                TranslationSource.Instance["App_ErrStartupTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Apply the saved/OS UI language before any window is created so the first
        // frame renders in the correct language (the service applies it in its ctor).
        _host.Services.GetRequiredService<ILocalizationService>();

        // Apply the saved app-chrome appearance (Light/Dark) before the first window renders.
        _host.Services.GetRequiredService<IAppThemeService>()
            .Apply(_host.Services.GetRequiredService<IAppSettingsService>().Current.Appearance);

        // Discover + load installed plugins (each isolated; failures are logged, not fatal).
        _host.Services.GetRequiredService<PluginManager>().LoadAll();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _logger.LogInformation("Startup complete");

        // Opt-in, non-blocking: never delays the first frame.
        _ = MaybeCheckForUpdatesAsync();
    }

    /// <summary>
    /// FFmpeg's native DLLs need the VC++ 2015–2022 runtime (vcruntime140/msvcp140), which ships
    /// with Windows 10/11 but can be absent on fresh or locked-down installs. Warn once, non-blocking —
    /// the app still runs; only video playback is affected.
    /// </summary>
    private void WarnIfVcRuntimeMissing()
    {
        var sys = Environment.SystemDirectory;
        if (File.Exists(Path.Combine(sys, "vcruntime140.dll")) &&
            File.Exists(Path.Combine(sys, "msvcp140.dll")))
            return;

        _logger.LogWarning("VC++ 2015–2022 runtime not detected — video playback may be unavailable.");
        System.Windows.MessageBox.Show(
            TranslationSource.Instance["App_VcRuntimeMissing"],
            TranslationSource.Instance["App_VcRuntimeMissingTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task MaybeCheckForUpdatesAsync()
    {
        if (!_host.Services.GetRequiredService<IAppSettingsService>().Current.CheckForUpdatesOnStartup)
            return;

        var info = await _host.Services.GetRequiredService<IUpdateService>().CheckAsync();
        if (info is null) return;

        var dialog = _host.Services.GetRequiredService<IDialogService>();
        var sizeMb = (info.MsiSizeBytes / 1024d / 1024d).ToString("0.#");
        if (!dialog.Confirm(
                string.Format(TranslationSource.Instance["Settings_UpdateConfirm"], info.Version, sizeMb),
                TranslationSource.Instance["Settings_UpdateAvailableTitle"]))
            return;

        try
        {
            // Only exit if the installer actually launched; if the operator cancels UAC, keep running.
            if (await _host.Services.GetRequiredService<IUpdateService>().DownloadAndApplyAsync(info))
                Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup update download/launch failed — continuing normally.");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("OpenAdoration shutting down");

        // Stop the host first so all hosted services can log their shutdown steps,
        // then flush — reversing this order drops any logs emitted during host stop.
        if (_host is not null)
        {
            // Let any in-flight settings write (fire-and-forget on Settings → leave) finish first.
            try { await _host.Services.GetRequiredService<IAppSettingsService>().FlushAsync(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Settings flush on exit failed."); }

            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        LoggingConfiguration.CloseAndFlush();

        base.OnExit(e);
    }

    /// <summary>
    /// Allowlist of dispatcher exceptions considered safe to swallow and continue from.
    /// These are transient I/O faults that do not corrupt app state. Anything not listed is
    /// treated as fatal — the app informs the user and terminates rather than continuing in a
    /// potentially inconsistent state. OperationCanceledException is deliberately excluded: a
    /// cancellation surfacing on the dispatcher thread is unexpected and may mask a deeper bug.
    /// </summary>
    private static bool IsRecoverable(Exception ex) => ex is
        System.IO.IOException or
        UnauthorizedAccessException;

    /// <summary>
    /// Swaps in a database staged by a backup restore (written as <c>&lt;db&gt;.restore</c>),
    /// then removes the staging file. No-op when nothing is pending.
    /// </summary>
    private static void ApplyPendingRestore(string dbPath)
    {
        var staged = dbPath + ".restore";
        if (!File.Exists(staged)) return;

        File.Copy(staged, dbPath, overwrite: true);
        File.Delete(staged);
        Log.Information("Applied pending database restore from {Staged}", staged);
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<IDialogService, MessageBoxDialogService>();
        services.AddSingleton<ISongLibraryNotifier, SongLibraryNotifier>();
        services.AddSingleton<BibleNavigationState>();
        services.AddSingleton<IBibleImportService, BibleImportService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IAppThemeService, AppThemeService>();
        services.AddSingleton(sp => new PluginManager(
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ILogger<PluginManager>>(),
            // Root the plugins dir at the active data dir (honours OA_DATA_DIR), not raw %LOCALAPPDATA%.
            Path.Combine(Path.GetDirectoryName(sp.GetRequiredService<OpenAdoration.Application.Common.AppPaths>().DbPath)!, "plugins")));
        services.AddTransient<PluginBibleImporter>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SongsViewModel>();
        services.AddTransient<AddEditSongViewModel>();
        services.AddTransient<BibleViewModel>();
        services.AddTransient<ServiceScheduleViewModel>();
        services.AddTransient<OpenAdoration.WPF.Helpers.VideoPsalmMigration.VideoPsalmServiceImporter>();
        services.AddTransient<MediaViewModel>();
        services.AddTransient<ThemeViewModel>();
        services.AddTransient<AddEditThemeViewModel>();
        services.AddTransient<StageViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PluginsViewModel>();
    }

    private static void RegisterWindows(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ProjectionWindow>();
    }
}
