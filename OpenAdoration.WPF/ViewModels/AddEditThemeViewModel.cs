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

    [ObservableProperty] private string  _name            = string.Empty;
    [ObservableProperty] private string  _fontFamily      = "Arial";
    [ObservableProperty] private int     _fontSize        = 72;

    // Colors stored as WPF Color — converted to/from hex string only at DB boundaries
    [ObservableProperty] private System.Windows.Media.Color?  _fontColor       = System.Windows.Media.Colors.White;
    [ObservableProperty] private System.Windows.Media.Color?  _backgroundColor = System.Windows.Media.Colors.Black;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private string? _backgroundImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundVideo))]
    private string? _backgroundVideoPath;

    [ObservableProperty] private bool _isDefault;

    public bool   IsNew      => _themeId == 0;
    public string FormTitle  => IsNew ? "New Theme" : "Edit Theme";
    public bool   HasBackgroundImage =>
        !string.IsNullOrWhiteSpace(BackgroundImagePath) && File.Exists(BackgroundImagePath);
    public bool   HasBackgroundVideo =>
        !string.IsNullOrWhiteSpace(BackgroundVideoPath) && File.Exists(BackgroundVideoPath);

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

    // ── Initialise ────────────────────────────────────────────────────────────

    public void InitialiseNew()
    {
        _themeId             = 0;
        Name                 = string.Empty;
        FontFamily           = "Arial";
        FontSize             = 72;
        FontColor            = System.Windows.Media.Colors.White;
        BackgroundColor      = System.Windows.Media.Colors.Black;
        BackgroundImagePath  = null;
        BackgroundVideoPath  = null;
        IsDefault            = false;
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    public void InitialiseEdit(Theme theme)
    {
        _themeId             = theme.Id;
        Name                 = theme.Name;
        FontFamily           = theme.FontFamily;
        FontSize             = theme.FontSize;
        FontColor            = ParseColor(theme.FontColor,      System.Windows.Media.Colors.White);
        BackgroundColor      = ParseColor(theme.BackgroundColor, System.Windows.Media.Colors.Black);
        BackgroundImagePath  = theme.BackgroundImagePath;
        BackgroundVideoPath  = theme.BackgroundVideoPath;
        IsDefault            = theme.IsDefault;
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Name))  { SetError("Theme name is required."); return; }
        if (FontSize <= 0)                     { SetError("Font size must be greater than zero."); return; }

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
        FontColor           = ColorToHex(FontColor,       "#FFFFFF"),
        BackgroundColor     = ColorToHex(BackgroundColor, "#000000"),
        BackgroundImagePath = NullIfEmpty(BackgroundImagePath),
        BackgroundVideoPath = NullIfEmpty(BackgroundVideoPath),
        IsDefault           = IsDefault
    };

    private static System.Windows.Media.Color ParseColor(string? hex, System.Windows.Media.Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try   { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static string ColorToHex(System.Windows.Media.Color? color, string fallback)
    {
        if (color is null) return fallback;
        var c = color.Value;
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
