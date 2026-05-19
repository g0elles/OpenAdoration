using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public partial class AddEditThemeViewModel : BaseViewModel
{
    private readonly IThemeService _themeService;
    private readonly ILogger<AddEditThemeViewModel> _logger;

    private int _themeId; // 0 = new

    [ObservableProperty] private string  _name              = string.Empty;
    [ObservableProperty] private string  _fontFamily        = "Arial";
    [ObservableProperty] private int     _fontSize          = 72;
    [ObservableProperty] private string  _fontColor         = "#FFFFFF";
    [ObservableProperty] private string  _backgroundColor   = "#000000";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private string? _backgroundImagePath;

    [ObservableProperty] private bool _isDefault;

    public bool   IsNew      => _themeId == 0;
    public string FormTitle  => IsNew ? "New Theme" : "Edit Theme";
    public bool   HasBackgroundImage =>
        !string.IsNullOrWhiteSpace(BackgroundImagePath) && File.Exists(BackgroundImagePath);

    /// <summary>Font families available in the picker — all standard Windows fonts.</summary>
    public static IReadOnlyList<string> AvailableFontFamilies { get; } =
    [
        "Arial",
        "Calibri",
        "Georgia",
        "Impact",
        "Segoe UI",
        "Times New Roman",
        "Trebuchet MS",
        "Verdana"
    ];

    public event EventHandler<Theme>? Saved;
    public event EventHandler?        Cancelled;

    public AddEditThemeViewModel(IThemeService themeService, ILogger<AddEditThemeViewModel> logger)
    {
        _themeService = themeService;
        _logger       = logger;
    }

    public void InitialiseNew()
    {
        _themeId           = 0;
        Name               = string.Empty;
        FontFamily         = "Arial";
        FontSize           = 72;
        FontColor          = "#FFFFFF";
        BackgroundColor    = "#000000";
        BackgroundImagePath = null;
        IsDefault          = false;
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    public void InitialiseEdit(Theme theme)
    {
        _themeId           = theme.Id;
        Name               = theme.Name;
        FontFamily         = theme.FontFamily;
        FontSize           = theme.FontSize;
        FontColor          = theme.FontColor;
        BackgroundColor    = theme.BackgroundColor;
        BackgroundImagePath = theme.BackgroundImagePath;
        IsDefault          = theme.IsDefault;
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Theme name is required.");
            return;
        }

        if (FontSize <= 0)
        {
            SetError("Font size must be greater than zero.");
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var theme = BuildTheme();

            if (IsNew)
            {
                var created = await _themeService.CreateAsync(theme);
                _logger.LogInformation("Theme created: {Name}", created.Name);
                Saved?.Invoke(this, created);
            }
            else
            {
                await _themeService.UpdateAsync(theme);
                _logger.LogInformation("Theme updated: {ThemeId}", _themeId);
                Saved?.Invoke(this, theme);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save theme");
            SetError("Failed to save. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearError();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Theme BuildTheme() => new()
    {
        Id                  = _themeId,
        Name                = Name.Trim(),
        FontFamily          = FontFamily,
        FontSize            = FontSize,
        FontColor           = FontColor,
        BackgroundColor     = BackgroundColor,
        BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundImagePath) ? null : BackgroundImagePath.Trim(),
        IsDefault           = IsDefault
    };
}
