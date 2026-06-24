using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public enum BackgroundType { Color, Image, Video }

/// <summary>Theme transition picker item. Null Kind = inherit the global default.</summary>
public sealed record TransitionOption(SlideTransitionKind? Kind, string Label)
{
    public override string ToString() => Label; // clean name for combo display + UI Automation
}

public partial class AddEditThemeViewModel : BaseViewModel
{
    private readonly IThemeService _themeService;
    private readonly IProjectionService _projectionService;
    private readonly IMediaService _mediaService;
    private readonly ILogger<AddEditThemeViewModel> _logger;

    private int _themeId; // 0 = new

    [ObservableProperty] private string  _name       = string.Empty;
    [ObservableProperty] private string  _fontFamily = "Arial";
    [ObservableProperty] private int     _fontSize   = 72;

    [ObservableProperty] private System.Windows.TextAlignment _textAlignment = System.Windows.TextAlignment.Center;

    public bool IsAlignLeft   => TextAlignment == System.Windows.TextAlignment.Left;
    public bool IsAlignCenter => TextAlignment == System.Windows.TextAlignment.Center;
    public bool IsAlignRight  => TextAlignment == System.Windows.TextAlignment.Right;

    partial void OnTextAlignmentChanged(System.Windows.TextAlignment value)
    {
        OnPropertyChanged(nameof(IsAlignLeft));
        OnPropertyChanged(nameof(IsAlignCenter));
        OnPropertyChanged(nameof(IsAlignRight));
    }

    [ObservableProperty] private System.Windows.Media.Color? _fontColor       = System.Windows.Media.Colors.White;
    [ObservableProperty] private System.Windows.Media.Color? _backgroundColor = System.Windows.Media.Colors.Black;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private string? _backgroundImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundVideo))]
    private string? _backgroundVideoPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(IsBackgroundImage))]
    [NotifyPropertyChangedFor(nameof(IsBackgroundVideo))]
    private BackgroundType _selectedBackgroundType = BackgroundType.Color;

    [ObservableProperty] private bool    _isDefault;
    [ObservableProperty] private string? _headerTemplate;
    [ObservableProperty] private string? _footerTemplate;

    public IReadOnlyList<TransitionOption> AvailableTransitions { get; }
    [ObservableProperty] private TransitionOption _selectedTransition = null!;

    public bool IsNew      => _themeId == 0;
    public string FormTitle => IsNew ? L("ThemeEdit_FormNew") : L("ThemeEdit_FormEdit");

    public bool IsBackgroundColor => SelectedBackgroundType == BackgroundType.Color;
    public bool IsBackgroundImage => SelectedBackgroundType == BackgroundType.Image;
    public bool IsBackgroundVideo => SelectedBackgroundType == BackgroundType.Video;

    public bool HasBackgroundImage =>
        !string.IsNullOrWhiteSpace(BackgroundImagePath) && File.Exists(BackgroundImagePath);
    public bool HasBackgroundVideo =>
        !string.IsNullOrWhiteSpace(BackgroundVideoPath) && File.Exists(BackgroundVideoPath);

    // Existing backgrounds in the managed library, for re-picking without hunting in foreign folders.
    public ObservableCollection<MediaFile> BackgroundImages { get; } = [];
    public ObservableCollection<MediaFile> BackgroundVideos { get; } = [];

    [ObservableProperty] private MediaFile? _selectedLibraryImage;
    [ObservableProperty] private MediaFile? _selectedLibraryVideo;

    partial void OnSelectedLibraryImageChanged(MediaFile? value)
    {
        if (value is not null) BackgroundImagePath = value.FilePath;
    }

    partial void OnSelectedLibraryVideoChanged(MediaFile? value)
    {
        if (value is not null) BackgroundVideoPath = value.FilePath;
    }

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

    public AddEditThemeViewModel(IThemeService themeService, IProjectionService projectionService, IMediaService mediaService, ILogger<AddEditThemeViewModel> logger)
    {
        _themeService      = themeService;
        _projectionService = projectionService;
        _mediaService      = mediaService;
        _logger            = logger;

        AvailableTransitions =
        [
            new TransitionOption(null, L("ThemeEdit_TransitionInherit")),
            .. Enum.GetValues<SlideTransitionKind>().Select(k => new TransitionOption(k, k.ToString()))
        ];
        SelectedTransition = AvailableTransitions[0];
    }

    // ── Initialise ────────────────────────────────────────────────────────────

    public void InitialiseNew()
    {
        _themeId               = 0;
        Name                   = string.Empty;
        FontFamily             = "Arial";
        FontSize               = 72;
        TextAlignment          = System.Windows.TextAlignment.Center;
        FontColor              = System.Windows.Media.Colors.White;
        BackgroundColor        = System.Windows.Media.Colors.Black;
        BackgroundImagePath    = null;
        BackgroundVideoPath    = null;
        SelectedBackgroundType = BackgroundType.Color;
        IsDefault              = false;
        HeaderTemplate         = null;
        FooterTemplate         = null;
        SelectedTransition     = AvailableTransitions[0]; // inherit global default
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
        _ = LoadBackgroundLibraryAsync();
    }

    public void InitialiseEdit(Theme theme)
    {
        _themeId            = theme.Id;
        Name                = theme.Name;
        FontFamily          = theme.FontFamily;
        FontSize            = theme.FontSize;
        TextAlignment       = ParseAlignment(theme.TextAlignment);
        FontColor           = ParseColor(theme.FontColor,      System.Windows.Media.Colors.White);
        BackgroundColor     = ParseColor(theme.BackgroundColor, System.Windows.Media.Colors.Black);
        BackgroundImagePath = theme.BackgroundImagePath;
        BackgroundVideoPath = theme.BackgroundVideoPath;
        IsDefault           = theme.IsDefault;
        HeaderTemplate      = theme.HeaderTemplate;
        FooterTemplate      = theme.FooterTemplate;
        SelectedTransition  = AvailableTransitions.FirstOrDefault(o => o.Kind == theme.SlideTransition)
                              ?? AvailableTransitions[0];

        SelectedBackgroundType = !string.IsNullOrWhiteSpace(theme.BackgroundVideoPath) ? BackgroundType.Video
            : !string.IsNullOrWhiteSpace(theme.BackgroundImagePath)                   ? BackgroundType.Image
            :                                                                            BackgroundType.Color;

        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
        _ = LoadBackgroundLibraryAsync();
    }

    // ── Background library ──────────────────────────────────────────────────────

    /// <summary>Loads the managed background library into the image/video pickers.</summary>
    public async Task LoadBackgroundLibraryAsync()
    {
        try
        {
            var backgrounds = await _mediaService.GetBackgroundsAsync();
            ReplaceAll(BackgroundImages, backgrounds.Where(b => b.Type == Domain.Enums.MediaType.Image));
            ReplaceAll(BackgroundVideos, backgrounds.Where(b => b.Type == Domain.Enums.MediaType.Video));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load background library");
        }
    }

    /// <summary>Imports a chosen file into the managed store as a background, then selects it.</summary>
    public async Task ImportBackgroundFileAsync(string sourcePath, bool isVideo)
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var media = await _mediaService.ImportBackgroundAsync(sourcePath);
            if (isVideo) BackgroundVideoPath = media.FilePath;
            else         BackgroundImagePath = media.FilePath;
            await LoadBackgroundLibraryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import background from {Source}", sourcePath);
            SetError(L("ThemeEdit_ErrBackgroundImport"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void ReplaceAll(ObservableCollection<MediaFile> target, IEnumerable<MediaFile> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Name)) { SetError(L("ThemeEdit_ErrNameRequired")); return; }
        if (FontSize <= 0)                   { SetError(L("ThemeEdit_ErrFontSize")); return; }

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
            _projectionService.NotifyThemeChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save theme");
            SetError(L("Common_SaveFailed"));
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

    [RelayCommand]
    private void SetAlignment(string alignment)
    {
        TextAlignment = alignment switch
        {
            "Left"  => System.Windows.TextAlignment.Left,
            "Right" => System.Windows.TextAlignment.Right,
            _       => System.Windows.TextAlignment.Center
        };
    }

    [RelayCommand]
    private void SetBackgroundType(string type)
    {
        SelectedBackgroundType = type switch
        {
            "Image" => BackgroundType.Image,
            "Video" => BackgroundType.Video,
            _       => BackgroundType.Color
        };
    }

    [RelayCommand]
    private void IncreaseFontSize()
    {
        if (FontSize < 300) FontSize = Math.Min(300, FontSize + 2);
    }

    [RelayCommand]
    private void DecreaseFontSize()
    {
        if (FontSize > 8) FontSize = Math.Max(8, FontSize - 2);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Theme BuildTheme() => new()
    {
        Id                  = _themeId,
        Name                = Name.Trim(),
        FontFamily          = FontFamily,
        FontSize            = FontSize,
        TextAlignment       = TextAlignment.ToString(),
        FontColor           = ColorToHex(FontColor,       "#FFFFFF"),
        BackgroundColor     = ColorToHex(BackgroundColor, "#000000"),
        BackgroundImagePath = IsBackgroundImage ? NullIfEmpty(BackgroundImagePath) : null,
        BackgroundVideoPath = IsBackgroundVideo ? NullIfEmpty(BackgroundVideoPath) : null,
        IsDefault           = IsDefault,
        HeaderTemplate      = NullIfEmpty(HeaderTemplate),
        FooterTemplate      = NullIfEmpty(FooterTemplate),
        SlideTransition     = SelectedTransition?.Kind
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

    private static System.Windows.TextAlignment ParseAlignment(string? s) => s switch
    {
        "Left"  => System.Windows.TextAlignment.Left,
        "Right" => System.Windows.TextAlignment.Right,
        _       => System.Windows.TextAlignment.Center
    };

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
