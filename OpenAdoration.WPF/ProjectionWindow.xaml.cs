using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenAdoration.WPF;

public partial class ProjectionWindow : Window
{
    private readonly IProjectionService   _projectionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITokenResolver       _tokenResolver;
    private readonly IAppSettingsService  _appSettings;
    private readonly ILogger<ProjectionWindow> _logger;

    // True only when MainWindow explicitly calls CloseForReal() on shutdown.
    // Prevents the operator's X click from destroying this Singleton window.
    private bool _allowClose;

    // Active theme for the current slide (applied by ApplyTheme / RenderSlide).
    private Theme? _activeTheme;

    // Monotonic counter incremented on every SlideChanged event.
    // After each async suspension point the handler checks whether a newer event
    // has already taken over, and abandons the render if so (P1-2: stale slide guard).
    private int _renderSequence;

    // Per-session theme resolution cache.  Both fields are written from
    // thread-pool continuations (inside async void OnSlideChanged), so
    // _themeCache uses ConcurrentDictionary for safe concurrent puts.
    // _defaultTheme is a reference assignment (atomic on all supported .NET
    // platforms) -- a benign double-write from two racing events is fine
    // because both would write the same Theme object fetched from the DB.
    private Theme?                                   _defaultTheme;
    private readonly ConcurrentDictionary<int, Theme> _themeCache = new();

    public ProjectionWindow(
        IProjectionService   projectionService,
        IServiceScopeFactory scopeFactory,
        ITokenResolver       tokenResolver,
        IAppSettingsService  appSettings,
        ILogger<ProjectionWindow> logger)
    {
        InitializeComponent();

        _projectionService = projectionService;
        _scopeFactory      = scopeFactory;
        _tokenResolver     = tokenResolver;
        _appSettings       = appSettings;
        _logger            = logger;

        _projectionService.SlideChanged           += OnSlideChanged;
        _projectionService.ProjectionStateChanged += OnProjectionStateChanged;
        _projectionService.ThemeChanged           += OnThemeChanged;
        _projectionService.AnnouncementChanged    += OnAnnouncementChanged;
    }

    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Shows the window if it is not already visible.
    /// On dual-screen: fullscreen on the secondary monitor.
    /// On single-screen: small floating window (800x450) in the bottom-right corner.
    /// Safe to call multiple times -- no-op if already shown.
    /// </summary>
    public void EnsureShown()
    {
        if (IsVisible) return;

        var secondary = ScreenHelper.GetSecondaryScreen();

        if (secondary is not null)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode  = ResizeMode.NoResize;
            ShowOnSecondaryScreen(secondary);
        }
        else
        {
            _logger.LogWarning("No secondary screen -- opening projection as a floating window");
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode  = ResizeMode.CanResize;
            Title       = "Projection Preview";

            var primary = System.Windows.Forms.Screen.PrimaryScreen!;
            Width  = 800;
            Height = 450;
            Left   = primary.WorkingArea.Right  - Width  - 20;
            Top    = primary.WorkingArea.Bottom - Height - 20;
            WindowState = WindowState.Normal;
            Show();
        }
    }

    private void ShowOnSecondaryScreen(System.Windows.Forms.Screen screen)
    {
        _logger.LogInformation("Projecting on: {Screen}", screen.DeviceName);
        ScreenHelper.PositionOnScreen(this, screen);
        WindowState = WindowState.Maximized;
        Show();
    }

    // -- Projection service callbacks -----------------------------------------

    private async void OnSlideChanged(object? sender, Slide? slide)
    {
        // Each event gets a unique sequence number so stale completions can self-discard.
        var seq = Interlocked.Increment(ref _renderSequence);
        try
        {
            // Resolve the theme WITHOUT writing _activeTheme yet.
            // This prevents a slow older event from overwriting _activeTheme that
            // a faster newer event already resolved (R1 -- theme race fix).
            var resolvedTheme = await ResolveThemeAsync(slide?.ThemeId);

            // Fast-path abandonment: if we are already stale, skip queuing to Dispatcher.
            if (seq != _renderSequence) return;

            // Assign _activeTheme and render inside the Dispatcher callback so both
            // happen atomically on the UI thread.  Re-check seq inside the callback
            // to guard against events that arrive between the await return and
            // actual callback execution (R2 -- Dispatcher window guard).
            await Dispatcher.InvokeAsync(() =>
            {
                if (seq != _renderSequence) return;
                _activeTheme = resolvedTheme; // shared write only after freshness is confirmed
                RenderSlide(slide);
            });
        }
        catch (Exception ex)
        {
            // async void exceptions escape ProjectionService's per-handler guard, so
            // we must catch here to prevent an unhandled exception on the UI sync context.
            _logger.LogError(ex, "Unhandled exception in projection slide handler -- display may be stale");
        }
    }

    private void OnProjectionStateChanged(object? sender, bool isProjecting)
    {
        // InvokeAsync -- non-blocking; safe even if called from a background thread (P10)
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (isProjecting)
                EnsureShown();
            else
                StopAndHide();
        });
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            _defaultTheme = null;
            _themeCache.Clear();
            _projectionService.RefreshCurrentSlide();
        });
    }

    // Banner overlay — independent of the slide layers, so the current slide stays intact.
    private void OnAnnouncementChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            var text = _projectionService.CurrentAnnouncement;
            if (string.IsNullOrWhiteSpace(text))
            {
                AnnouncementBanner.Visibility = Visibility.Collapsed;
                AnnouncementText.Text = string.Empty;
            }
            else
            {
                AnnouncementText.Text = text;
                AnnouncementBanner.Visibility = Visibility.Visible;
            }
        });
    }

    // -- Theme -----------------------------------------------------------------

    /// <summary>
    /// Resolves and returns the <see cref="Theme"/> for <paramref name="themeId"/>,
    /// or the default theme when <paramref name="themeId"/> is null.
    /// Results are cached in <see cref="_themeCache"/> / <see cref="_defaultTheme"/> for
    /// the duration of the projection session.
    /// Does NOT write <see cref="_activeTheme"/> -- the caller does that inside a
    /// Dispatcher action after confirming the render sequence is still current (R1).
    /// </summary>
    private async Task<Theme?> ResolveThemeAsync(int? themeId)
    {
        // Cache hit -- return without any shared-state mutation (other than the cache itself).
        if (themeId.HasValue)
        {
            if (_themeCache.TryGetValue(themeId.Value, out var cached))
                return cached;
        }
        else if (_defaultTheme is not null)
        {
            return _defaultTheme;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var themeService = scope.ServiceProvider.GetRequiredService<IThemeService>();

            Theme theme;
            if (themeId.HasValue)
            {
                // Fall back to the default theme if the requested ID no longer exists.
                theme = await themeService.GetByIdAsync(themeId.Value)
                        ?? await themeService.GetDefaultAsync();
                _themeCache[themeId.Value] = theme; // ConcurrentDictionary -- safe from any thread
            }
            else
            {
                theme = await themeService.GetDefaultAsync();
                _defaultTheme = theme; // reference assignment -- atomic on all .NET platforms
            }

            _logger.LogDebug("Resolved theme '{Name}' (ThemeId={ThemeId})",
                theme.Name, themeId?.ToString() ?? "default");
            return theme;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve theme (ThemeId={ThemeId}) -- using hardcoded fallback",
                themeId?.ToString() ?? "default");
            return null;
        }
    }

    /// <summary>Applies the cached theme to all rendering elements.</summary>
    private void ApplyTheme()
    {
        if (_activeTheme is null) return;

        var fontFamily = new System.Windows.Media.FontFamily(_activeTheme.FontFamily);
        var fontColor  = HexToBrush(_activeTheme.FontColor);

        // Body text style
        SlideTextBlock.FontFamily    = fontFamily;
        SlideTextBlock.FontSize      = _activeTheme.FontSize;
        SlideTextBlock.LineHeight    = _activeTheme.FontSize * 1.33;
        SlideTextBlock.Foreground    = fontColor;
        SlideTextBlock.TextAlignment = ParseTextAlignment(_activeTheme.TextAlignment);

        // Header / footer zone font (same family + color, smaller fixed size)
        HeaderText.FontFamily = fontFamily;
        HeaderText.Foreground = fontColor;
        FooterText.FontFamily = fontFamily;
        FooterText.Foreground = fontColor;

        // Background color
        ThemeBackground.Fill = HexToBrush(_activeTheme.BackgroundColor);

        // Background video (highest priority -- overrides image and color)
        if (!string.IsNullOrWhiteSpace(_activeTheme.BackgroundVideoPath)
            && File.Exists(_activeTheme.BackgroundVideoPath))
        {
            try
            {
                ThemeBackgroundImage.Source     = null;
                ThemeBackgroundImage.Visibility = Visibility.Collapsed;

                ThemeBackgroundVideo.Source     = new Uri(_activeTheme.BackgroundVideoPath, UriKind.Absolute);
                ThemeBackgroundVideo.Visibility = Visibility.Visible;
                ThemeBackgroundVideo.Play();
            }
            catch (Exception ex)
            {
                // Log filename only -- full path stays out of support logs (S3)
                _logger.LogWarning(ex, "Could not load theme background video '{FileName}'",
                    Path.GetFileName(_activeTheme.BackgroundVideoPath));
                StopThemeVideo();
                ApplyThemeImage();
            }

            return; // video loaded -- skip image layer
        }

        StopThemeVideo();
        ApplyThemeImage();
    }

    private void ApplyThemeImage()
    {
        if (_activeTheme is null) return;

        if (!string.IsNullOrWhiteSpace(_activeTheme.BackgroundImagePath)
            && File.Exists(_activeTheme.BackgroundImagePath))
        {
            try
            {
                // Decode at most 1920 px wide -- caps memory use on high-res source images (P5)
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource       = new Uri(_activeTheme.BackgroundImagePath, UriKind.Absolute);
                bitmap.CacheOption     = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 1920;
                bitmap.EndInit();
                bitmap.Freeze(); // safe for cross-thread access

                ThemeBackgroundImage.Source     = bitmap;
                ThemeBackgroundImage.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Log filename only -- full path stays out of support logs (S3)
                _logger.LogWarning(ex, "Could not load theme background image '{FileName}'",
                    Path.GetFileName(_activeTheme.BackgroundImagePath));
                ThemeBackgroundImage.Source     = null;
                ThemeBackgroundImage.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ThemeBackgroundImage.Source     = null;
            ThemeBackgroundImage.Visibility = Visibility.Collapsed;
        }
    }

    private void StopThemeVideo()
    {
        ThemeBackgroundVideo.Stop();
        ThemeBackgroundVideo.Source     = null;
        ThemeBackgroundVideo.Visibility = Visibility.Collapsed;
    }

    private void StopContentVideo()
    {
        ContentVideo.Stop();
        ContentVideo.Source     = null;
        ContentVideo.Visibility = Visibility.Collapsed;
    }

    // Loops the video background by seeking back to the start when it finishes.
    private void OnThemeVideoEnded(object sender, RoutedEventArgs e)
    {
        ThemeBackgroundVideo.Position = TimeSpan.Zero;
        ThemeBackgroundVideo.Play();
    }

    private static System.Windows.TextAlignment ParseTextAlignment(string? s) => s switch
    {
        "Left"  => System.Windows.TextAlignment.Left,
        "Right" => System.Windows.TextAlignment.Right,
        _       => System.Windows.TextAlignment.Center
    };

    private static SolidColorBrush HexToBrush(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(System.Windows.Media.Colors.White);
        }
    }

    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".m4v" };

    // -- Rendering -------------------------------------------------------------

    private void RenderSlide(Slide? slide)
    {
        if (slide is null)
        {
            ClearDisplay();
            return;
        }

        ApplyTheme();
        UpdateCornerLabel(slide);

        switch (slide.Type)
        {
            case SlideType.Song:
            case SlideType.Bible:
                ShowText(slide.Content, slide.Context);
                break;

            case SlideType.Media:
                ShowMedia(slide.MediaPath);
                break;

            case SlideType.Blank:
                ShowBlankOverlay();
                break;

            default:
                _logger.LogWarning("Unknown SlideType {Type} -- clearing display", slide.Type);
                ClearDisplay();
                break;
        }

        PlayTransition();
    }

    // Fades the foreground content in on each slide change. Theme background stays static.
    private void PlayTransition()
    {
        var ms = _appSettings.Current.SlideTransitionMilliseconds;
        if (ms <= 0)
        {
            ContentLayers.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
            ContentLayers.Opacity = 1;
            return;
        }

        var fade = new System.Windows.Media.Animation.DoubleAnimation
        {
            From     = 0,
            To       = 1,
            Duration = TimeSpan.FromMilliseconds(ms)
        };
        ContentLayers.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
    }

    private void ShowText(string content, SlideContext context)
    {
        HideAllLayers();
        SlideTextBlock.Text = content;

        // Resolve and show header zone when the theme defines a template.
        // Collapse when the resolved text contains no letters or digits — this handles
        // pure-token templates (e.g. "[BibleBookName] [BibleChapterID]:[BibleVerseID]")
        // that are irrelevant on the current slide type (song, blank, etc.).
        // Zones with static text (e.g. "Community Church") always show.
        var headerTemplate = _activeTheme?.HeaderTemplate;
        if (!string.IsNullOrEmpty(headerTemplate))
        {
            var resolved = _tokenResolver.Resolve(headerTemplate, context);
            if (resolved.Any(char.IsLetterOrDigit))
            {
                HeaderText.Text        = resolved;
                HeaderText.Visibility  = Visibility.Visible;
                CornerLabel.Visibility = Visibility.Collapsed;
            }
        }

        // Resolve and show footer zone when the theme defines a template.
        var footerTemplate = _activeTheme?.FooterTemplate;
        if (!string.IsNullOrEmpty(footerTemplate))
        {
            var resolved = _tokenResolver.Resolve(footerTemplate, context);
            if (resolved.Any(char.IsLetterOrDigit))
            {
                FooterText.Text       = resolved;
                FooterText.Visibility = Visibility.Visible;
            }
        }

        TextZoneGrid.Visibility = Visibility.Visible;
    }

    private void ShowMedia(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("Media slide file missing ('{FileName}') -- showing blank",
                string.IsNullOrWhiteSpace(path) ? "(empty)" : Path.GetFileName(path));
            ShowBlankOverlay();
            return;
        }

        if (VideoExtensions.Contains(Path.GetExtension(path)))
            ShowVideoMedia(path);
        else
            ShowImageMedia(path);
    }

    private void ShowVideoMedia(string path)
    {
        try
        {
            HideAllLayers();
            ContentVideo.Source     = new Uri(path, UriKind.Absolute);
            ContentVideo.Visibility = Visibility.Visible;
            ContentVideo.Play();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media video '{FileName}' -- showing blank",
                Path.GetFileName(path));
            ShowBlankOverlay();
        }
    }

    private void ShowImageMedia(string path)
    {
        try
        {
            // Decode at most 1920 px wide -- caps memory for high-res source images (P5)
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource        = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption      = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 1920;
            bitmap.EndInit();
            bitmap.Freeze();

            HideAllLayers();
            BackgroundImage.Source     = bitmap;
            BackgroundImage.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media image '{FileName}' -- showing blank",
                Path.GetFileName(path));
            ShowBlankOverlay();
        }
    }

    private void ShowBlankOverlay()
    {
        HideAllLayers();
        BlankOverlay.Visibility = Visibility.Visible;
    }

    private void StopAndHide()
    {
        // Drop any in-flight fade so the next session starts fully opaque.
        ContentLayers.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
        ContentLayers.Opacity = 1;

        StopThemeVideo();
        StopContentVideo();
        // Clear per-session caches so the next session picks up any theme edits
        // the operator made between services.
        _activeTheme  = null;
        _defaultTheme = null;
        _themeCache.Clear();
        ClearDisplay();
        Hide();
    }

    private void ClearDisplay()
    {
        HideAllLayers();
        CornerLabel.Visibility = Visibility.Collapsed;
    }

    private void HideAllLayers()
    {
        TextZoneGrid.Visibility    = Visibility.Collapsed;
        HeaderText.Visibility      = Visibility.Collapsed;
        FooterText.Visibility      = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Collapsed;
        BlankOverlay.Visibility    = Visibility.Collapsed;
        SlideTextBlock.Text        = string.Empty;
        HeaderText.Text            = string.Empty;
        FooterText.Text            = string.Empty;
        BackgroundImage.Source     = null;
        StopContentVideo();
    }

    private void UpdateCornerLabel(Slide slide)
    {
        // Corner label is the fallback: suppress it when the header zone is active.
        if (!string.IsNullOrEmpty(_activeTheme?.HeaderTemplate))
        {
            CornerLabel.Visibility = Visibility.Collapsed;
            return;
        }

        var label = _projectionService.ContextLabel;

        if (string.IsNullOrWhiteSpace(label))
        {
            CornerLabel.Visibility = Visibility.Collapsed;
            return;
        }

        CornerSongTitle.Text    = label;
        CornerSectionLabel.Text = slide.Label;
        CornerLabel.Visibility  = Visibility.Visible;
    }

    // -- Lifecycle -------------------------------------------------------------

    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            if (_projectionService.IsProjecting)
                _projectionService.Stop(); // StopAndHide() called via ProjectionStateChanged event
            else
                Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _projectionService.SlideChanged           -= OnSlideChanged;
        _projectionService.ProjectionStateChanged -= OnProjectionStateChanged;
        _projectionService.ThemeChanged           -= OnThemeChanged;
        _projectionService.AnnouncementChanged    -= OnAnnouncementChanged;
        base.OnClosed(e);
    }
}
