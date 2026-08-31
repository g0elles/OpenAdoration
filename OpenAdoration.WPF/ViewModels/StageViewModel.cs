using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers;

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

/// <summary>One compact row in the stage view's clickable "all slides" list.</summary>
public sealed record SlideListItem(int Index, string Label, string PreviewText, bool IsCurrent);

public partial class StageViewModel : BaseViewModel, IDisposable
{
    private readonly IProjectionService     _projectionService;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly ITokenResolver         _tokenResolver;
    private readonly IAppSettingsService    _appSettings;
    private readonly ILogger<StageViewModel> _logger;

    // Per-navigation theme cache — cleared on ThemeChanged and recreated with next scope
    private readonly ConcurrentDictionary<int, Theme> _themeCache = new();
    private Theme? _defaultTheme;

    // Status
    [ObservableProperty] private bool   _isProjecting;
    [ObservableProperty] private bool   _isServiceScheduleActive;
    [ObservableProperty] private string _contextLabel  = string.Empty;
    [ObservableProperty] private string _slidePosition = string.Empty;

    // Rendering snapshots
    [ObservableProperty] private SlidePreview _currentPreview = SlidePreview.Empty;
    [ObservableProperty] private bool         _hasNextSlide;
    [ObservableProperty] private SlidePreview _nextPreview = SlidePreview.Empty;

    // Compact clickable list of every slide in the current item, alongside the up-next preview.
    [ObservableProperty] private ObservableCollection<SlideListItem> _allSlides = [];

    // Mirrors the projector's transport so the preview pauses when the operator pauses.
    [ObservableProperty] private bool _isPreviewVideoPlaying = true;

    // Announcement banner (overlays the current-slide preview)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnnouncement))]
    private string _announcementText = string.Empty;

    public bool HasAnnouncement => !string.IsNullOrEmpty(AnnouncementText);

    // Persistent lower-third mirror — replays the projector's band styling and scroll ticker.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLowerThird))]
    private string _lowerThirdText = string.Empty;

    [ObservableProperty] private bool   _lowerThirdScrollEnabled;
    [ObservableProperty] private int    _lowerThirdScrollSpeed = 90;
    [ObservableProperty] private string _lowerThirdBandColor = "#CC101018";
    [ObservableProperty] private string _lowerThirdTextColor = "#FFFFFF";
    [ObservableProperty] private int    _lowerThirdFontSize   = 40;

    public bool HasLowerThird => !string.IsNullOrEmpty(LowerThirdText);

    public StageViewModel(
        IProjectionService projectionService,
        IServiceScopeFactory scopeFactory,
        ITokenResolver tokenResolver,
        IAppSettingsService appSettings,
        ILogger<StageViewModel> logger)
    {
        _projectionService = projectionService;
        _scopeFactory      = scopeFactory;
        _tokenResolver     = tokenResolver;
        _appSettings       = appSettings;
        _logger            = logger;
    }

    // ── Schedule item navigation (delegated to ServiceScheduleViewModel via IProjectionService) ──

    [RelayCommand]
    private void NextItem() => _projectionService.RequestNextScheduleItem();

    [RelayCommand]
    private void PrevItem() => _projectionService.RequestPreviousScheduleItem();

    // ── Slide list navigation ─────────────────────────────────────────────────

    [RelayCommand]
    private void JumpToSlide(int index) => _projectionService.GoTo(index);

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
            _projectionService.LowerThirdChanged              += OnLowerThirdChanged;
            _projectionService.MediaTransportChanged          += OnMediaTransportChanged;

            AnnouncementText = _projectionService.CurrentAnnouncement ?? string.Empty;
            RefreshLowerThirdSettings();
            LowerThirdText = _projectionService.CurrentLowerThird ?? string.Empty;
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

    private void OnLowerThirdChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // Settings first so the View sees fresh band/scroll state before it reacts to the text.
            RefreshLowerThirdSettings();
            LowerThirdText = _projectionService.CurrentLowerThird ?? string.Empty;
        });
    }

    // Matches the fixed design canvas in StageView.xaml (Grid Width="1920" Height="1080")
    // that the preview's outer Viewbox scales uniformly to fit the small panel.
    private const double DesignCanvasWidth  = 1920;
    private const double DesignCanvasHeight = 1080;

    /// <summary>Snapshots the lower-third band/scroll settings — mirrors ProjectionWindow.ApplyLowerThirdStyle.</summary>
    private void RefreshLowerThirdSettings()
    {
        var s = _appSettings.Current;
        var (scaleX, scaleY) = GetProjectorMirrorScale();
        LowerThirdScrollEnabled = s.LowerThirdScroll;
        LowerThirdScrollSpeed   = Math.Max(10, (int)Math.Round(s.LowerThirdScrollSpeed * scaleX));
        LowerThirdBandColor     = s.LowerThirdBandColor;
        LowerThirdTextColor     = s.LowerThirdTextColor;
        LowerThirdFontSize      = Math.Max(12, (int)Math.Round(s.LowerThirdFontSize * scaleY));
    }

    /// <summary>
    /// Font size and scroll speed are absolute values tuned for the projector's real screen.
    /// The stage preview renders everything inside a fixed 1920×1080 canvas (Viewbox-scaled to
    /// the small panel), so mirroring those values 1:1 only looks right when the real projector
    /// happens to be 1920×1080. Pre-scaling by (design ÷ real) here makes the final on-screen
    /// proportion — after the panel's own Viewbox scales the canvas down — match the real
    /// projector at any resolution. No secondary screen: <see cref="ProjectionWindow"/> falls
    /// back to its small floating preview window instead of a 1920×1080 mirror, so that window's
    /// fixed size is the "real" resolution to scale against, not 1920×1080 (which would wrongly
    /// assume a full-size mirror and leave the stage preview's lower third much smaller than
    /// what the floating preview window actually shows).
    /// </summary>
    private static (double ScaleX, double ScaleY) GetProjectorMirrorScale()
    {
        var bounds = ScreenHelper.GetSecondaryScreen()?.Bounds;
        return bounds is { } b
            ? ComputeMirrorScale(b.Width, b.Height)
            : ComputeMirrorScale(ProjectionWindow.FallbackPreviewWidth, ProjectionWindow.FallbackPreviewHeight);
    }

    /// <summary>Pure design÷real scale math, split out from the screen lookup so it's unit-testable.</summary>
    public static (double ScaleX, double ScaleY) ComputeMirrorScale(int realWidth, int realHeight)
    {
        if (realWidth <= 0 || realHeight <= 0) return (1.0, 1.0);
        return (DesignCanvasWidth / realWidth, DesignCanvasHeight / realHeight);
    }

    private void OnMediaTransportChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            IsPreviewVideoPlaying = _projectionService.MediaTransport.IsPlaying);
    }

    // ── Core refresh ──────────────────────────────────────────────────────────

    // Monotonic guard: rapid slide changes (auto-advance) fire concurrent RefreshAsync tasks.
    // Each tags itself; a task superseded by a newer one self-discards instead of writing a
    // stale preview — mirrors ProjectionWindow.OnSlideChanged (H2).
    private int _refreshSequence;

    private async Task RefreshAsync()
    {
        var seq = Interlocked.Increment(ref _refreshSequence);

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
        var allSlideItems    = BuildSlideListItems(slides, idx);

        // A newer refresh started while we awaited theme resolution — let it win.
        if (seq != _refreshSequence) return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (seq != _refreshSequence) return; // re-check on the UI thread before writing
            IsProjecting             = isProjecting;
            IsServiceScheduleActive  = isScheduleActive;
            ContextLabel  = _projectionService.ContextLabel;
            SlidePosition = isProjecting && slides.Count > 0
                ? $"{idx + 1} / {slides.Count}"
                : string.Empty;

            CurrentPreview = currentPreview;
            HasNextSlide   = hasNext;
            NextPreview    = nextPreview;
            AllSlides      = allSlideItems;
        });
    }

    // ── Slide list builder ───────────────────────────────────────────────────

    private const int PreviewTextMaxLength = 40;

    /// <summary>Pure mapping from the projected slides to compact list rows — split out (like
    /// <see cref="ComputeMirrorScale"/>) so it's unit-testable without a live Dispatcher.</summary>
    public static ObservableCollection<SlideListItem> BuildSlideListItems(IReadOnlyList<Slide> slides, int currentIndex)
    {
        var items = new ObservableCollection<SlideListItem>();
        for (var i = 0; i < slides.Count; i++)
            items.Add(new SlideListItem(i, slides[i].Label, TruncatePreview(slides[i].Content), i == currentIndex));
        return items;
    }

    private static string TruncatePreview(string content)
    {
        var firstLine = content.Split('\n')[0].Trim();
        return firstLine.Length > PreviewTextMaxLength
            ? firstLine[..PreviewTextMaxLength] + "…"
            : firstLine;
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

        var isImageMedia = slide.Type == SlideType.Media && MediaFormats.IsImage(slide.MediaPath);
        var isVideoMedia = slide.Type == SlideType.Media && MediaFormats.IsVideo(slide.MediaPath);

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
        _projectionService.LowerThirdChanged              -= OnLowerThirdChanged;
        _projectionService.MediaTransportChanged          -= OnMediaTransportChanged;
    }
}
