using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAppSettingsService _settings;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty] private string _churchName = string.Empty;
    [ObservableProperty] private string _churchCcliNumber = string.Empty;
    [ObservableProperty] private int _defaultAutoAdvanceSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSavedConfirmation))]
    private bool _isSaved;

    public bool ShowSavedConfirmation => IsSaved;

    public SettingsViewModel(
        IAppSettingsService settings,
        IProjectionService projectionService,
        ILogger<SettingsViewModel> logger)
    {
        _settings          = settings;
        _projectionService = projectionService;
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
            ChurchName                = current.ChurchName ?? string.Empty;
            ChurchCcliNumber          = current.ChurchCcliNumber ?? string.Empty;
            DefaultAutoAdvanceSeconds = current.DefaultAutoAdvanceSeconds;
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
                ChurchName                = string.IsNullOrWhiteSpace(ChurchName) ? null : ChurchName.Trim(),
                ChurchCcliNumber          = string.IsNullOrWhiteSpace(ChurchCcliNumber) ? null : ChurchCcliNumber.Trim(),
                DefaultAutoAdvanceSeconds = DefaultAutoAdvanceSeconds < 0 ? 0 : DefaultAutoAdvanceSeconds
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
            SetError("Could not save settings.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnChurchNameChanged(string value) => IsSaved = false;
    partial void OnChurchCcliNumberChanged(string value) => IsSaved = false;
    partial void OnDefaultAutoAdvanceSecondsChanged(int value) => IsSaved = false;
}
