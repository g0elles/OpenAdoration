using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAppSettingsService _settings;
    private readonly IProjectionService _projectionService;
    private readonly ILocalizationService _localization;
    private readonly IBackupService _backup;
    private readonly IUpdateService _update;
    private readonly IDialogService _dialog;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty] private string _churchName = string.Empty;
    [ObservableProperty] private string _churchCcliNumber = string.Empty;
    [ObservableProperty] private int _defaultAutoAdvanceSeconds;
    [ObservableProperty] private int _defaultBibleVersesPerSlide = 1;
    [ObservableProperty] private int _announcementDurationSeconds = 25;
    [ObservableProperty] private int _slideTransitionMilliseconds = 300;
    [ObservableProperty] private SlideTransitionKind _selectedTransition = SlideTransitionKind.Fade;
    [ObservableProperty] private bool _checkForUpdatesOnStartup;

    public IReadOnlyList<SlideTransitionKind> AvailableTransitions { get; } = Enum.GetValues<SlideTransitionKind>();

    public IReadOnlyList<LanguageOption> AvailableLanguages => _localization.AvailableLanguages;

    [ObservableProperty] private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSavedConfirmation))]
    private bool _isSaved;

    public bool ShowSavedConfirmation => IsSaved;

    /// <summary>Hosted on the Settings → Plugins tab (resolved in the same nav scope).</summary>
    public PluginsViewModel Plugins { get; }

    public SettingsViewModel(
        IAppSettingsService settings,
        IProjectionService projectionService,
        ILocalizationService localization,
        IBackupService backup,
        IUpdateService update,
        IDialogService dialog,
        PluginsViewModel plugins,
        ILogger<SettingsViewModel> logger)
    {
        _settings          = settings;
        _projectionService = projectionService;
        _localization      = localization;
        _backup            = backup;
        _update            = update;
        _dialog            = dialog;
        Plugins            = plugins;
        _logger            = logger;
    }

    [RelayCommand]
    private void Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var current = _settings.Current;
            ChurchName                  = current.ChurchName ?? string.Empty;
            ChurchCcliNumber            = current.ChurchCcliNumber ?? string.Empty;
            DefaultAutoAdvanceSeconds   = current.DefaultAutoAdvanceSeconds;
            DefaultBibleVersesPerSlide  = current.DefaultBibleVersesPerSlide;
            AnnouncementDurationSeconds = current.AnnouncementDurationSeconds;
            SlideTransitionMilliseconds = current.SlideTransitionMilliseconds;
            SelectedTransition          = current.SlideTransition;
            CheckForUpdatesOnStartup    = current.CheckForUpdatesOnStartup;
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == _localization.CurrentLanguageCode)
                               ?? AvailableLanguages.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        IsSaved = false;
        try
        {
            var updated = new AppSettings
            {
                ChurchName                 = string.IsNullOrWhiteSpace(ChurchName) ? null : ChurchName.Trim(),
                ChurchCcliNumber           = string.IsNullOrWhiteSpace(ChurchCcliNumber) ? null : ChurchCcliNumber.Trim(),
                DefaultAutoAdvanceSeconds   = DefaultAutoAdvanceSeconds < 0 ? 0 : DefaultAutoAdvanceSeconds,
                DefaultBibleVersesPerSlide  = DefaultBibleVersesPerSlide < 1 ? 1 : DefaultBibleVersesPerSlide,
                AnnouncementDurationSeconds = AnnouncementDurationSeconds < 1 ? 1 : AnnouncementDurationSeconds,
                SlideTransitionMilliseconds = SlideTransitionMilliseconds < 0 ? 0 : SlideTransitionMilliseconds,
                SlideTransition             = SelectedTransition,
                UiCulture                   = SelectedLanguage?.Code,
                CheckForUpdatesOnStartup    = CheckForUpdatesOnStartup
            };

            await _settings.SaveAsync(updated);

            // Church tokens may appear in the active theme's header/footer — re-render.
            _projectionService.NotifyThemeChanged();

            IsSaved = true;
            _logger.LogInformation("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            SetError(L("Settings_ErrSave"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L("Settings_CreateBackupTitle"),
            Filter = L("Settings_BackupFilter") + "|*.oabak",
            FileName = $"OpenAdoration-backup-{DateTime.Now:yyyy-MM-dd}.oabak"
        };
        if (dialog.ShowDialog() != true || IsBusy) return;

        IsBusy = true;
        ClearError();
        try
        {
            await _backup.CreateAsync(dialog.FileName);
            _dialog.Inform(L("Settings_BackupCreated"), L("Settings_CreateBackupTitle"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            SetError(L("Settings_ErrCreateBackup"));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L("Settings_RestoreBackupTitle"),
            Filter = L("Settings_BackupFilter") + "|*.oabak",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true || IsBusy) return;

        if (!_dialog.Confirm(L("Settings_RestoreConfirm"), L("Settings_RestoreBackupTitle")))
            return;

        IsBusy = true;
        ClearError();
        try
        {
            var result = await _backup.RestoreAsync(dialog.FileName);
            if (result.Outcome == RestoreOutcome.Compatible)
            {
                _dialog.Inform(result.Message, L("Settings_RestoreBackupTitle"));
                System.Windows.Application.Current.Shutdown();
                return;
            }
            SetError(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            SetError(L("Settings_ErrRestore"));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var info = await _update.CheckAsync();
            if (info is null)
            {
                _dialog.Inform(L("Settings_UpToDate"), L("Settings_CheckUpdatesTitle"));
                return;
            }

            var sizeMb = info.MsiSizeBytes / 1024d / 1024d;
            if (!_dialog.Confirm(
                    L("Settings_UpdateConfirm", info.Version, sizeMb.ToString("0.#")),
                    L("Settings_UpdateAvailableTitle")))
                return;

            await _update.DownloadAndApplyAsync(info);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            SetError(L("Settings_ErrUpdate"));
        }
        finally { IsBusy = false; }
    }

    partial void OnCheckForUpdatesOnStartupChanged(bool value) => IsSaved = false;
    partial void OnSelectedTransitionChanged(SlideTransitionKind value) => IsSaved = false;

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;
        _localization.SetLanguage(value.Code); // live preview; persisted on Save
        IsSaved = false;
    }

    partial void OnChurchNameChanged(string value) => IsSaved = false;
    partial void OnChurchCcliNumberChanged(string value) => IsSaved = false;
    partial void OnDefaultAutoAdvanceSecondsChanged(int value) => IsSaved = false;
    partial void OnDefaultBibleVersesPerSlideChanged(int value) => IsSaved = false;
    partial void OnAnnouncementDurationSecondsChanged(int value) => IsSaved = false;
    partial void OnSlideTransitionMillisecondsChanged(int value) => IsSaved = false;
}
