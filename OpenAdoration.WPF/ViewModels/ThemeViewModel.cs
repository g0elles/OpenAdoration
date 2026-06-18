using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class ThemeViewModel : BaseViewModel, IDisposable
{
    private readonly IThemeService           _themeService;
    private readonly IDialogService          _dialogService;
    private readonly ILogger<ThemeViewModel> _logger;

    public AddEditThemeViewModel EditViewModel { get; }

    [ObservableProperty] private ObservableCollection<Theme> _themes = [];
    [ObservableProperty] private Theme? _selectedTheme;
    [ObservableProperty] private bool   _isEditing;

    public ThemeViewModel(
        IThemeService           themeService,
        IDialogService          dialogService,
        AddEditThemeViewModel   editViewModel,
        ILogger<ThemeViewModel> logger)
    {
        _themeService  = themeService;
        _dialogService = dialogService;
        _logger        = logger;
        EditViewModel  = editViewModel;

        EditViewModel.Saved     += OnThemeSaved;
        EditViewModel.Cancelled += OnEditCancelled;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var list = await _themeService.GetAllAsync();
            Themes = new ObservableCollection<Theme>(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load themes");
            SetError(L("Themes_ErrLoad"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewTheme()
    {
        SelectedTheme = null;
        EditViewModel.InitialiseNew();
        IsEditing = true;
    }

    [RelayCommand]
    private void EditTheme(Theme theme)
    {
        SelectedTheme = theme;
        EditViewModel.InitialiseEdit(theme);
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SetDefaultAsync(Theme theme)
    {
        if (IsBusy || theme.IsDefault) return;
        IsBusy = true;
        ClearError();
        try
        {
            await _themeService.SetDefaultAsync(theme.Id);
            _logger.LogInformation("Theme {ThemeId} set as default", theme.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default theme {ThemeId}", theme.Id);
            SetError(L("Themes_ErrSetDefault"));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    private static bool CanDeleteTheme(Theme? theme) => theme?.IsDefault == false;

    [RelayCommand(CanExecute = nameof(CanDeleteTheme))]
    private async Task DeleteThemeAsync(Theme theme)
    {
        if (!_dialogService.Confirm(
                L("Themes_ConfirmDelete", theme.Name),
                L("Themes_DeleteDialogTitle")))
            return;

        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            await _themeService.DeleteAsync(theme.Id);
            _logger.LogInformation("Theme deleted: {ThemeId}", theme.Id);
            if (SelectedTheme?.Id == theme.Id) SelectedTheme = null;
        }
        catch (InvalidOperationException)
        {
            // Repository throws this when trying to delete the default theme
            SetError(L("Themes_DeleteTooltip"));
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete theme {ThemeId}", theme.Id);
            SetError(L("Themes_ErrDelete"));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    // ── Event handlers from EditViewModel ─────────────────────────────────────

    private async void OnThemeSaved(object? sender, Theme theme)
    {
        IsEditing = false;
        await LoadAsync();
    }

    private void OnEditCancelled(object? sender, EventArgs e)
    {
        IsEditing = false;
    }

    public void Dispose()
    {
        EditViewModel.Saved     -= OnThemeSaved;
        EditViewModel.Cancelled -= OnEditCancelled;
    }
}
