using System.Collections.Concurrent;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

/// <summary>Immutable rendering snapshot for one slide in the stage view.</summary>
public sealed record SlidePreview
{
    public static readonly SlidePreview Empty = new();

    public string  Content       { get; init; } = string.Empty;
    public string  SectionLabel  { get; init; } = string.Empty;

    // Layout flags (mutually exclusive)
    public bool IsBlank      { get; init; }
    public bool IsText       { get; init; }   // Song or Bible
    public bool IsImageMedia { get; init; }
    public bool IsVideoMedia { get; init; }

    // Media
    public string? MediaPath { get; init; }

    // Theme colours (WPF Media.Color so ColorToBrush converter works directly)
    public System.Windows.Media.Color BgColor   { get; init; } = System.Windows.Media.Colors.Black;
    public System.Windows.Media.Color FontColor { get; init; } = System.Windows.Media.Colors.White;
    public string? BgImagePath { get; init; }
    public bool    HasBgImage  { get; init; }
    public string? BgVideoPath { get; init; }
    public bool    HasBgVideo  { get; init; }

    // Theme text style
    public string                      FontFamily     { get; init; } = "Arial";
    public double                      FontSize       { get; init; } = 72;
    public System.Windows.TextAlignment TextAlignment { get; init; } = System.Windows.TextAlignment.Center;

    // Resolved header / footer
    public string HeaderText { get; init; } = string.Empty;
    public bool   HasHeader  { get; init; }
    public string FooterText { get; init; } = string.Empty;
    public bool   HasFooter  { get; init; }
}

public partial class StageViewModel : BaseViewModel, IDisposable
{
    private readonly IProjectionService     _projectionService;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly ITokenResolver         _tokenResolver;
    private readonly ILogger<StageViewModel> _logger;

    // Per-navigation theme cache — cleared on ThemeChanged and recreated with next scope
    private readonly ConcurrentDictionary<int, Theme> _themeCache = new();
    private Theme? _defaultTheme;

    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".m4v" };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

    // Status
    [ObservableProperty] private bool   _isProjecting;
    [ObservableProperty] private bool   _isServiceScheduleActive;
    [ObservableProperty] private string _contextLabel  = string.Empty;
    [ObservableProperty] private string _slidePosition = string.Empty;

    // Rendering snapshots
    [ObservableProperty] private SlidePreview _currentPreview = SlidePreview.Empty;
    [ObservableProperty] private bool         _hasNextSlide;
    [ObservableProperty] private SlidePreview _nextPreview = SlidePreview.Empty;

    // Mirrors the projector's transport so the preview pauses when the operator pauses.
    [ObservableProperty] private bool _isPreviewVideoPlaying = true;

    // Announcement banner (overlays the current-slide preview)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnnouncement))]
    private string _announcementText = string.Empty;

    public bool HasAnnouncement => !string.IsNullOrEmpty(AnnouncementText);

    public StageViewModel(
        IProjectionService projectionService,
        IServiceScopeFactory scopeFactory,
        ITokenResolver tokenResolver,
        ILogger<StageViewModel> logger)
    {
        _projectionService = projectionService;
        _scopeFactory      = scopeFactory;
        _tokenResolver     = tokenResolver;
        _logger            = logger;
    }

    // ── Schedule item navigation (delegated to ServiceScheduleViewModel via IProjectionService) ──

    [RelayCommand]
    private void NextItem() => _projectionService.RequestNextScheduleItem();

    [RelayCommand]
    private void PrevItem() => _projectionService.RequestPreviousScheduleItem();

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            _projectionService.SlideChanged                   += OnSlideChanged;
            _projectionService.ProjectionStateChanged         += OnProjectionStateChanged;
            _projectionService.ThemeChanged                   += OnThemeChanged;
            _projectionService.ServiceScheduleActiveChanged   += OnServiceScheduleActiveChanged;
            _projectionService.NextScheduleItemPreviewChanged += OnNextScheduleItemPreviewChanged;
            _projectionService.AnnouncementChanged            += OnAnnouncementChanged;
            _projectionService.MediaTransportChanged          += OnMediaTransportChanged;

            AnnouncementText = _projectionService.CurrentAnnouncement ?? string.Empty;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage view load failed");
            SetError(L("Stage_ErrLoad"));
        }
        finally { IsBusy = false; }
    }

    // ── Projection event handlers ─────────────────────────────────────────────

    private async void OnSlideChanged(object? sender, Slide? slide)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view slide refresh failed"); }
    }

    private async void OnProjectionStateChanged(object? sender, bool _)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view state refresh failed"); }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _defaultTheme = null;
        _themeCache.Clear();
        _ = RefreshAsync();
    }

    private async void OnServiceScheduleActiveChanged(object? sender, EventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view schedule-active refresh failed"); }
    }

    private async void OnNextScheduleItemPreviewChanged(object? sender, EventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view next-item preview refresh failed"); }
    }

    private void OnAnnouncementChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            AnnouncementText = _projectionService.CurrentAnnouncement ?? string.Empty);
    }

    private void OnMediaTransportChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            IsPreviewVideoPlaying = _projectionService.MediaTransport.IsPlaying);
    }

    // ── Core refresh ──────────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        var isProjecting = _projectionService.IsProjecting;
        var slides       = _projectionService.CurrentSlides;
        var idx          = _projectionService.CurrentSlideIndex;
        var current      = _projectionService.CurrentSlide;

        var currentTheme   = await ResolveThemeAsync(current?.ThemeId);
        var currentPreview = BuildPreview(current, currentTheme);

        SlidePreview nextPreview = SlidePreview.Empty;
        bool hasNext = false;
        var nextIdx  = idx + 1;

        if (isProjecting && nextIdx < slides.Count)
        {
            // Next slide within the current schedule item
            var next      = slides[nextIdx];
            var nextTheme = await ResolveThemeAsync(next.ThemeId);
            nextPreview = BuildPreview(next, nextTheme);
            hasNext     = true;
        }
        else if (isProjecting && _projectionService.NextScheduleItemPreviewSlide is { } nextItemSlide)
        {
            // On the last slide of this item — preview the first slide of the next schedule item
            var nextTheme = await ResolveThemeAsync(nextItemSlide.ThemeId);
            nextPreview = BuildPreview(nextItemSlide, nextTheme);
            hasNext     = true;
        }

        var isScheduleActive = _projectionService.IsServiceScheduleActive;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsProjecting             = isProjecting;
            IsServiceScheduleActive  = isScheduleActive;
            ContextLabel  = _projectionService.ContextLabel;
            SlidePosition = isProjecting && slides.Count > 0
                ? $"{idx + 1} / {slides.Count}"
                : string.Empty;

            CurrentPreview = currentPreview;
            HasNextSlide   = hasNext;
            NextPreview    = nextPreview;
        });
    }

    // ── Preview builder ───────────────────────────────────────────────────────

    private SlidePreview BuildPreview(Slide? slide, Theme? theme)
    {
        if (slide is null) return SlidePreview.Empty;

        var fontFamily  = theme?.FontFamily ?? "Arial";
        var fontSize    = (double)(theme?.FontSize ?? 72);
        var fontColor   = ParseColor(theme?.FontColor,       System.Windows.Media.Colors.White);
        var textAlign   = ParseAlignment(theme?.TextAlignment);
        var bgColor     = ParseColor(theme?.BackgroundColor, System.Windows.Media.Colors.Black);
        var bgImagePath = ValidPath(theme?.BackgroundImagePath);
        var bgVideoPath = ValidPath(theme?.BackgroundVideoPath);

        var headerText = ResolveZone(theme?.HeaderTemplate, slide.Context);
        var footerText = ResolveZone(theme?.FooterTemplate, slide.Context);

        var ext          = Path.GetExtension(slide.MediaPath ?? string.Empty).ToLowerInvariant();
        var isImageMedia = slide.Type == SlideType.Media && ImageExtensions.Contains(ext);
        var isVideoMedia = slide.Type == SlideType.Media && VideoExtensions.Contains(ext);

        return new SlidePreview
        {
            Content       = slide.Content,
            SectionLabel  = slide.Label,
            IsBlank       = slide.Type == SlideType.Blank,
            IsText        = slide.Type is SlideType.Song or SlideType.Bible,
            IsImageMedia  = isImageMedia,
            IsVideoMedia  = isVideoMedia,
            MediaPath     = slide.MediaPath,
            FontFamily    = fontFamily,
            FontSize      = fontSize,
            FontColor     = fontColor,
            TextAlignment = textAlign,
            BgColor       = bgColor,
            BgImagePath   = bgImagePath,
            HasBgImage    = bgImagePath is not null,
            BgVideoPath   = bgVideoPath,
            HasBgVideo    = bgVideoPath is not null,
            HeaderText    = headerText,
            HasHeader     = !string.IsNullOrEmpty(headerText),
            FooterText    = footerText,
            HasFooter     = !string.IsNullOrEmpty(footerText)
        };
    }

    private string ResolveZone(string? template, SlideContext context)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var resolved = _tokenResolver.Resolve(template, context);
        return resolved.Any(char.IsLetterOrDigit) ? resolved : string.Empty;
    }

    // ── Theme resolution ──────────────────────────────────────────────────────

    private async Task<Theme?> ResolveThemeAsync(int? themeId)
    {
        if (themeId.HasValue)
        {
            if (_themeCache.TryGetValue(themeId.Value, out var cached)) return cached;
        }
        else if (_defaultTheme is not null)
        {
            return _defaultTheme;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var svc = scope.ServiceProvider.GetRequiredService<IThemeService>();

            Theme theme;
            if (themeId.HasValue)
            {
                theme = await svc.GetByIdAsync(themeId.Value) ?? await svc.GetDefaultAsync();
                _themeCache[themeId.Value] = theme;
            }
            else
            {
                theme = await svc.GetDefaultAsync();
                _defaultTheme = theme;
            }
            return theme;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stage view failed to resolve theme (ThemeId={ThemeId})",
                themeId?.ToString() ?? "default");
            return null;
        }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static System.Windows.Media.Color ParseColor(string? hex, System.Windows.Media.Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try   { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static System.Windows.TextAlignment ParseAlignment(string? s) => s switch
    {
        "Left"  => System.Windows.TextAlignment.Left,
        "Right" => System.Windows.TextAlignment.Right,
        _       => System.Windows.TextAlignment.Center
    };

    private static string? ValidPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _projectionService.SlideChanged                   -= OnSlideChanged;
        _projectionService.ProjectionStateChanged         -= OnProjectionStateChanged;
        _projectionService.ThemeChanged                   -= OnThemeChanged;
        _projectionService.ServiceScheduleActiveChanged   -= OnServiceScheduleActiveChanged;
        _projectionService.NextScheduleItemPreviewChanged -= OnNextScheduleItemPreviewChanged;
        _projectionService.AnnouncementChanged            -= OnAnnouncementChanged;
        _projectionService.MediaTransportChanged          -= OnMediaTransportChanged;
    }
}
