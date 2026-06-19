using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Common;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAppSettingsService _settings;
    private readonly IThemeService _themeService;
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

    /// <summary>Per-content-type default-theme pickers; index 0 is the "app default" (null) sentinel.</summary>
    public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];

    [ObservableProperty] private ThemeOption? _selectedSongTheme;
    [ObservableProperty] private ThemeOption? _selectedScriptureTheme;
    [ObservableProperty] private ThemeOption? _selectedMediaTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSavedConfirmation))]
    private bool _isSaved;

    public bool ShowSavedConfirmation => IsSaved;

    /// <summary>True once the operator edits a field without saving. Drives the prompt-on-leave.</summary>
    [ObservableProperty] private bool _hasUnsavedChanges;

    private bool    _loading;             // suppresses dirty-marking while Load() populates fields
    private string? _loadedLanguageCode;  // for reverting the live language preview on discard

    private void MarkDirty()
    {
        IsSaved = false;
        if (!_loading) HasUnsavedChanges = true;
    }

    /// <summary>Hosted on the Settings → Plugins tab (resolved in the same nav scope).</summary>
    public PluginsViewModel Plugins { get; }

    public SettingsViewModel(
        IAppSettingsService settings,
        IThemeService themeService,
        IProjectionService projectionService,
        ILocalizationService localization,
        IBackupService backup,
        IUpdateService update,
        IDialogService dialog,
        PluginsViewModel plugins,
        ILogger<SettingsViewModel> logger)
    {
        _settings          = settings;
        _themeService      = themeService;
        _projectionService = projectionService;
        _localization      = localization;
        _backup            = backup;
        _update            = update;
        _dialog            = dialog;
        Plugins            = plugins;
        _logger            = logger;
    }

    [RelayCommand]
    private async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        _loading = true;
        ClearError();
        try
        {
            _loadedLanguageCode = _localization.CurrentLanguageCode;
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
            await LoadThemesAsync(current);
        }
        finally
        {
            IsBusy = false;
            _loading = false;
            HasUnsavedChanges = false;
        }
    }

    private async Task LoadThemesAsync(AppSettings current)
    {
        AvailableThemes.Clear();
        AvailableThemes.Add(new ThemeOption(null, L("Settings_ThemeAppDefault")));
        foreach (var theme in await _themeService.GetAllAsync())
            AvailableThemes.Add(new ThemeOption(theme.Id, theme.Name));

        SelectedSongTheme      = Pick(current.DefaultSongThemeId);
        SelectedScriptureTheme = Pick(current.DefaultScriptureThemeId);
        SelectedMediaTheme     = Pick(current.DefaultMediaThemeId);

        ThemeOption Pick(int? id) => AvailableThemes.FirstOrDefault(o => o.Id == id) ?? AvailableThemes[0];
    }

    /// <summary>
    /// Called by <see cref="MainViewModel"/> before navigating away. If there are pending edits,
    /// asks the operator to save them; declining reverts the live language preview to the saved one.
    /// </summary>
    public void OnLeaving()
    {
        if (!HasUnsavedChanges) return;

        if (_dialog.Confirm(L("Settings_LeaveUnsaved"), L("Settings_Title")))
            _ = SaveAsync(); // _settings is a singleton — completes even as this scoped VM is disposed
        else if (_loadedLanguageCode is not null)
            _localization.SetLanguage(_loadedLanguageCode); // undo the live preview

        HasUnsavedChanges = false;
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
                CheckForUpdatesOnStartup    = CheckForUpdatesOnStartup,
                DefaultSongThemeId          = SelectedSongTheme?.Id,
                DefaultScriptureThemeId     = SelectedScriptureTheme?.Id,
                DefaultMediaThemeId         = SelectedMediaTheme?.Id
            };

            await _settings.SaveAsync(updated);

            // Church tokens may appear in the active theme's header/footer — re-render.
            _projectionService.NotifyThemeChanged();

            _loadedLanguageCode = SelectedLanguage?.Code ?? _loadedLanguageCode;
            HasUnsavedChanges = false;
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

    partial void OnCheckForUpdatesOnStartupChanged(bool value) => MarkDirty();
    partial void OnSelectedTransitionChanged(SlideTransitionKind value) => MarkDirty();
    partial void OnSelectedSongThemeChanged(ThemeOption? value) => MarkDirty();
    partial void OnSelectedScriptureThemeChanged(ThemeOption? value) => MarkDirty();
    partial void OnSelectedMediaThemeChanged(ThemeOption? value) => MarkDirty();

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;
        _localization.SetLanguage(value.Code); // live preview; persisted on Save
        MarkDirty();
    }

    partial void OnChurchNameChanged(string value) => MarkDirty();
    partial void OnChurchCcliNumberChanged(string value) => MarkDirty();
    partial void OnDefaultAutoAdvanceSecondsChanged(int value) => MarkDirty();
    partial void OnDefaultBibleVersesPerSlideChanged(int value) => MarkDirty();
    partial void OnAnnouncementDurationSecondsChanged(int value) => MarkDirty();
    partial void OnSlideTransitionMillisecondsChanged(int value) => MarkDirty();
}
