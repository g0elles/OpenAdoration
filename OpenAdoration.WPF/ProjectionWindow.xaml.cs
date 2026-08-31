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
using Unosquare.FFME.Common;

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

    // F7: ad-hoc, non-persisted per-slide override (font size / colours) set by Stage View's
    // quick style fix. Mirrors the current slide's StyleOverride — see ApplyTheme.
    private SlideStyleOverride? _activeStyleOverride;

    // Monotonic counter incremented on every SlideChanged event.
    // After each async suspension point the handler checks whether a newer event
    // has already taken over, and abandons the render if so (P1-2: stale slide guard).
    private int _renderSequence;

    // Monotonic counter for crossfade snapshots (UI thread only): a superseded transition's
    // Completed callback must not hide the snapshot a newer transition is animating.
    private int _transitionToken;

    // Per-session theme resolution cache.  Both fields are written from
    // thread-pool continuations (inside async void OnSlideChanged), so
    // _themeCache uses ConcurrentDictionary for safe concurrent puts.
    // _defaultTheme is a reference assignment (atomic on all supported .NET
    // platforms) -- a benign double-write from two racing events is fine
    // because both would write the same Theme object fetched from the DB.
    private Theme?                                   _defaultTheme;
    private readonly ConcurrentDictionary<int, Theme> _themeCache = new();

    /// <summary>
    /// Floating preview window size used by <see cref="EnsureShown"/> when no secondary
    /// (non-primary) monitor is connected. Public so <see cref="ViewModels.StageViewModel"/>
    /// can mirror the same real on-screen proportions in that fallback mode — this window
    /// renders its lower-third/announcement/header/footer text at literal pixel size with no
    /// Viewbox, so their apparent size depends on the actual window size, not a fixed canvas.
    /// </summary>
    public const int FallbackPreviewWidth  = 800;
    public const int FallbackPreviewHeight = 450;

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
        _projectionService.LowerThirdChanged      += OnLowerThirdChanged;
        _projectionService.MediaCommandRequested  += OnMediaCommandRequested;
        _projectionService.MediaSeekRequested     += OnMediaSeekRequested;
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

        var screens = System.Windows.Forms.Screen.AllScreens;
        _logger.LogInformation("EnsureShown: {Count} screen(s) detected -- {Screens}",
            screens.Length,
            string.Join(", ", screens.Select(s => $"{s.DeviceName} {s.Bounds.Width}x{s.Bounds.Height}{(s.Primary ? " (primary)" : string.Empty)}")));

        var secondary = ScreenHelper.GetSecondaryScreen();

        if (secondary is not null)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode  = ResizeMode.NoResize;
            ShowOnSecondaryScreen(secondary);
        }
        else
        {
            _logger.LogWarning(
                "No secondary (non-primary) screen detected -- opening projection as a floating " +
                "preview window. If a projector is connected, set Windows display mode to " +
                "\"Extend\" (not \"Duplicate\").");
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode  = ResizeMode.CanResize;
            Title       = "Projection Preview";

            var primary = System.Windows.Forms.Screen.PrimaryScreen!;
            Width  = FallbackPreviewWidth;
            Height = FallbackPreviewHeight;
            Left   = primary.WorkingArea.Right  - Width  - 20;
            Top    = primary.WorkingArea.Bottom - Height - 20;
            WindowState = WindowState.Normal;
            Show();
        }
    }

    private void ShowOnSecondaryScreen(System.Windows.Forms.Screen screen)
    {
        _logger.LogInformation("Projecting on: {Screen} ({Width}x{Height})",
            screen.DeviceName, screen.Bounds.Width, screen.Bounds.Height);

        // Show the window FIRST so it has a realized HWND, then drive its position with
        // physical-pixel SetWindowPos. Setting WindowState.Maximized before Show() (the
        // previous approach) maximizes onto the PRIMARY monitor regardless of Left/Top, and
        // device-pixel Left/Top are wrong under display scaling — both put the projection on
        // the operator's screen. A borderless window sized to the exact monitor bounds is
        // full-screen without needing Maximized.
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState           = WindowState.Normal;
        Show();
        ScreenHelper.PositionOnScreen(this, screen);
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
                _activeTheme         = resolvedTheme; // shared write only after freshness is confirmed
                _activeStyleOverride = slide?.StyleOverride;
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

    // Persistent overlay — stays across slide changes until the operator clears it.
    private void OnLowerThirdChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            var text = _projectionService.CurrentLowerThird;
            if (string.IsNullOrWhiteSpace(text))
            {
                StopLowerThirdTicker();
                LowerThirdBar.Visibility = Visibility.Collapsed;
                LowerThirdText.Text = string.Empty;
            }
            else
            {
                ApplyLowerThirdStyle();
                LowerThirdText.Text = text;
                LowerThirdBar.Visibility = Visibility.Visible;
                if (_appSettings.Current.LowerThirdScroll) StartLowerThirdTicker();
                else StopLowerThirdTicker();
            }
        });
    }

    // Band styling comes from settings at show-time (set up once in Settings → General).
    private void ApplyLowerThirdStyle()
    {
        var s = _appSettings.Current;
        LowerThirdBar.Background   = HexToBrush(s.LowerThirdBandColor);
        LowerThirdText.Foreground  = HexToBrush(s.LowerThirdTextColor);
        LowerThirdText.FontSize    = Math.Max(12, s.LowerThirdFontSize);
    }

    /// <summary>
    /// Continuous right-to-left marquee: the text enters from the right edge, exits fully
    /// left, and repeats until cleared. Constant speed (no easing — tickers must not pulse).
    /// </summary>
    private void StartLowerThirdTicker()
    {
        // Ticker layout: single line, left-anchored, free to overflow the clipped band.
        LowerThirdText.TextWrapping        = TextWrapping.NoWrap;
        LowerThirdText.TextAlignment       = TextAlignment.Left;
        LowerThirdText.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;

        // Measure after the pending Text/style changes so widths are current.
        LowerThirdBar.UpdateLayout();
        var textWidth = LowerThirdText.ActualWidth;
        var barWidth  = LowerThirdBar.ActualWidth;
        if (textWidth <= 0 || barWidth <= 0) return;

        var speed    = Math.Max(10, _appSettings.Current.LowerThirdScrollSpeed);
        var duration = TimeSpan.FromSeconds((barWidth + textWidth) / speed);

        var translate = new System.Windows.Media.TranslateTransform();
        LowerThirdText.RenderTransform = translate;
        translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(barWidth, -textWidth, duration)
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            });
    }

    private void StopLowerThirdTicker()
    {
        if (LowerThirdText.RenderTransform is System.Windows.Media.TranslateTransform t)
            t.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        LowerThirdText.RenderTransform = System.Windows.Media.Transform.Identity;

        // Restore the static-band layout.
        LowerThirdText.TextWrapping        = TextWrapping.Wrap;
        LowerThirdText.TextAlignment       = TextAlignment.Center;
        LowerThirdText.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
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

        // F7 quick style override wins over the resolved theme when set (per-field, ad-hoc, not persisted).
        var fontFamily = new System.Windows.Media.FontFamily(_activeTheme.FontFamily);
        var fontSize   = _activeStyleOverride?.FontSize ?? _activeTheme.FontSize;
        var fontColor  = HexToBrush(_activeStyleOverride?.FontColor ?? _activeTheme.FontColor);

        // Body text style
        SlideTextBlock.FontFamily    = fontFamily;
        SlideTextBlock.FontSize      = fontSize;
        SlideTextBlock.LineHeight    = fontSize * 1.33;
        SlideTextBlock.Foreground    = fontColor;
        SlideTextBlock.TextAlignment = ParseTextAlignment(_activeTheme.TextAlignment);

        // Header / footer zone font (same family + color, smaller fixed size)
        HeaderText.FontFamily = fontFamily;
        HeaderText.Foreground = fontColor;
        FooterText.FontFamily = fontFamily;
        FooterText.Foreground = fontColor;

        // Background color
        ThemeBackground.Fill = HexToBrush(_activeStyleOverride?.BackgroundColor ?? _activeTheme.BackgroundColor);

        // Background video (highest priority -- overrides image and color)
        if (!string.IsNullOrWhiteSpace(_activeTheme.BackgroundVideoPath)
            && File.Exists(_activeTheme.BackgroundVideoPath))
        {
            try
            {
                ThemeBackgroundImage.Source     = null;
                ThemeBackgroundImage.Visibility = Visibility.Collapsed;

                // FFME opens + plays automatically because LoadedBehavior="Play".
                _ = ThemeBackgroundVideo.Open(new Uri(_activeTheme.BackgroundVideoPath, UriKind.Absolute));
                ThemeBackgroundVideo.Visibility = Visibility.Visible;
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
        _ = ThemeBackgroundVideo.Close();
        ThemeBackgroundVideo.Visibility = Visibility.Collapsed;
    }

    private void StopContentVideo()
    {
        _ = ContentVideo.Close();
        ContentVideo.Visibility = Visibility.Collapsed;
    }

    // -- Media transport (M10.5, FFME) -----------------------------------------

    private async void OnMediaCommandRequested(object? sender, MediaCommand command)
    {
        if (ContentVideo.Source is null) return;
        switch (command)
        {
            case MediaCommand.Play:            await ContentVideo.Play();  break;
            case MediaCommand.Pause:           await ContentVideo.Pause(); break;
            case MediaCommand.TogglePlayPause:
                if (ContentVideo.IsPlaying) await ContentVideo.Pause(); else await ContentVideo.Play();
                break;
            case MediaCommand.Restart:
                await ContentVideo.Seek(TimeSpan.Zero);
                await ContentVideo.Play();
                break;
        }
        ReportMediaTransport();
    }

    private async void OnMediaSeekRequested(object? sender, TimeSpan delta)
    {
        if (ContentVideo.Source is null) return;

        var duration = ContentVideo.NaturalDuration ?? TimeSpan.Zero;
        var target   = ContentVideo.Position + delta;
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (duration > TimeSpan.Zero && target > duration) target = duration;

        await ContentVideo.Seek(target);
        ReportMediaTransport();
    }

    // Loops the projected content video when it reaches the end.
    private async void OnContentVideoEnded(object? sender, EventArgs e)
    {
        await ContentVideo.Seek(TimeSpan.Zero);
        await ContentVideo.Play();
    }

    private void OnContentVideoOpened(object? sender, MediaOpenedEventArgs e) => ReportMediaTransport();
    private void OnContentVideoPositionChanged(object? sender, PositionChangedEventArgs e) => ReportMediaTransport();
    private void OnContentVideoStateChanged(object? sender, MediaStateChangedEventArgs e) => ReportMediaTransport();

    private void ReportMediaTransport()
    {
        var duration = ContentVideo.NaturalDuration ?? TimeSpan.Zero;
        _projectionService.ReportMediaTransport(
            new MediaTransportState(ContentVideo.IsPlaying, ContentVideo.Position, duration));
    }

    // Loops the video background by seeking back to the start when it finishes.
    private async void OnThemeVideoEnded(object? sender, EventArgs e)
    {
        await ThemeBackgroundVideo.Seek(TimeSpan.Zero);
        await ThemeBackgroundVideo.Play();
    }

    // WPF MediaElement decodes via Windows Media Foundation; an unsupported codec/container
    // fails here (otherwise silently). Logging e.ErrorException captures the real cause
    // (e.g. HRESULT 0xC00D5212 "no decoder"), and we degrade gracefully instead of going black.
    private void OnContentVideoFailed(object? sender, MediaFailedEventArgs e)
    {
        _logger.LogError(e.ErrorException,
            "Projection video failed to play -- decode error. Showing blank. File: '{FileName}'",
            VideoSourceName(ContentVideo));
        ShowBlankOverlay();
    }

    private void OnThemeVideoFailed(object? sender, MediaFailedEventArgs e)
    {
        _logger.LogWarning(e.ErrorException,
            "Theme background video failed to play -- decode error. Falling back to image/color. File: '{FileName}'",
            VideoSourceName(ThemeBackgroundVideo));
        StopThemeVideo();
        ApplyThemeImage();
    }

    private static string VideoSourceName(Unosquare.FFME.MediaElement element) =>
        element.Source is null ? "(none)" : Path.GetFileName(element.Source.LocalPath);

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

    // -- Rendering -------------------------------------------------------------

    private void RenderSlide(Slide? slide)
    {
        if (slide is null)
        {
            ClearDisplay();
            return;
        }

        // Capture the outgoing content BEFORE the layers mutate, so old and new can overlap.
        var snapshot = CaptureContentSnapshot();

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

        PlayTransition(snapshot);
    }

    /// <summary>
    /// Renders the current <see cref="ContentLayers"/> to a frozen still for the crossfade.
    /// Null when there is nothing to crossfade from (first slide, hidden window, Cut) or
    /// when the capture fails — the transition then runs incoming-only, as before.
    /// </summary>
    private BitmapSource? CaptureContentSnapshot()
    {
        if (_appSettings.Current.SlideTransitionMilliseconds <= 0) return null;

        var width  = (int)Math.Round(ContentLayers.ActualWidth);
        var height = (int)Math.Round(ContentLayers.ActualHeight);
        if (width <= 0 || height <= 0 || !HasVisibleContent()) return null;

        try
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(ContentLayers);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Outgoing-slide snapshot failed -- transitioning without crossfade");
            return null;
        }
    }

    private bool HasVisibleContent() =>
        TextZoneGrid.Visibility   == Visibility.Visible ||
        BackgroundImage.Visibility == Visibility.Visible ||
        ContentVideo.Visibility   == Visibility.Visible ||
        BlankOverlay.Visibility   == Visibility.Visible;

    // Animates the slide change (Fade/Slide/Zoom): the outgoing snapshot exits on top while
    // the incoming content enters underneath, so the screen is never blank mid-transition.
    // Theme background stays static so it never flickers between slides.
    private void PlayTransition(BitmapSource? snapshot)
    {
        // Reset any prior animation so transitions never stack or leave residue.
        ResetTransitionState();

        var ms = _appSettings.Current.SlideTransitionMilliseconds;
        if (ms <= 0) return; // Cut

        var duration = TimeSpan.FromMilliseconds(ms);
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };

        // Per-theme transition overrides the global default; duration stays global.
        var kind = _activeTheme?.SlideTransition ?? _appSettings.Current.SlideTransition;
        if (snapshot is not null) BeginSnapshotExit(snapshot, kind, duration, ease);
        BeginContentEnter(kind, duration, ease, crossfading: snapshot is not null);
    }

    /// <summary>Shows the outgoing still above the incoming content and animates it out.</summary>
    private void BeginSnapshotExit(
        BitmapSource snapshot,
        Domain.Common.SlideTransitionKind kind,
        TimeSpan duration,
        System.Windows.Media.Animation.IEasingFunction ease)
    {
        TransitionSnapshot.Source     = snapshot;
        TransitionSnapshot.Visibility = Visibility.Visible;

        if (kind == Domain.Common.SlideTransitionKind.Slide)
        {
            // Push: the old slide exits left in step with the new one entering from the right.
            var translate = new System.Windows.Media.TranslateTransform();
            TransitionSnapshot.RenderTransform = translate;
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, -ContentLayers.ActualWidth, duration)
                { EasingFunction = ease });
        }

        // The opacity fade doubles as the cleanup trigger; a stale Completed (superseded by a
        // newer transition) must not tear down that newer transition's snapshot — hence the token.
        var token = ++_transitionToken;
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, duration);
        fadeOut.Completed += (_, _) => { if (token == _transitionToken) HideTransitionSnapshot(); };
        TransitionSnapshot.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeOut);
    }

    /// <summary>Animates the incoming <see cref="ContentLayers"/> per the transition kind.</summary>
    private void BeginContentEnter(
        Domain.Common.SlideTransitionKind kind,
        TimeSpan duration,
        System.Windows.Media.Animation.IEasingFunction ease,
        bool crossfading)
    {
        switch (kind)
        {
            case Domain.Common.SlideTransitionKind.Slide:
                var translate = new System.Windows.Media.TranslateTransform();
                ContentLayers.RenderTransform = translate;
                translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(ContentLayers.ActualWidth, 0, duration)
                    { EasingFunction = ease });
                break;

            case Domain.Common.SlideTransitionKind.Zoom:
                ContentLayers.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                var scale = new System.Windows.Media.ScaleTransform(0.85, 0.85);
                ContentLayers.RenderTransform = scale;
                scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0.85, 1, duration) { EasingFunction = ease });
                scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0.85, 1, duration) { EasingFunction = ease });
                ContentLayers.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration));
                break;

            default: // Fade — with a snapshot fading out on top this is a true crossfade;
                     // fading the incoming half too would dim the whole screen mid-transition.
                if (!crossfading)
                    ContentLayers.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration));
                break;
        }
    }

    /// <summary>Cancels in-flight transition animations and clears the snapshot overlay.</summary>
    private void ResetTransitionState()
    {
        ContentLayers.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
        ContentLayers.Opacity = 1;
        ContentLayers.RenderTransform = System.Windows.Media.Transform.Identity;
        HideTransitionSnapshot();
    }

    private void HideTransitionSnapshot()
    {
        TransitionSnapshot.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
        if (TransitionSnapshot.RenderTransform is System.Windows.Media.TranslateTransform t)
            t.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        TransitionSnapshot.RenderTransform = System.Windows.Media.Transform.Identity;
        TransitionSnapshot.Opacity    = 1;
        TransitionSnapshot.Visibility = Visibility.Collapsed;
        TransitionSnapshot.Source     = null;
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

        if (MediaFormats.IsVideo(path))
            ShowVideoMedia(path);
        else
            ShowImageMedia(path);
    }

    private void ShowVideoMedia(string path)
    {
        try
        {
            HideAllLayers();
            // FFME opens + plays automatically because LoadedBehavior="Play"; transport
            // state flows back via PositionChanged / MediaStateChanged.
            _ = ContentVideo.Open(new Uri(path, UriKind.Absolute));
            ContentVideo.Visibility = Visibility.Visible;
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
        // Drop any in-flight transition (incl. the crossfade snapshot) so the next session starts clean.
        ResetTransitionState();

        StopThemeVideo();
        StopContentVideo();
        // Clear per-session caches so the next session picks up any theme edits
        // the operator made between services.
        _activeTheme         = null;
        _activeStyleOverride = null;
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
        _projectionService.LowerThirdChanged      -= OnLowerThirdChanged;
        _projectionService.MediaCommandRequested  -= OnMediaCommandRequested;
        _projectionService.MediaSeekRequested     -= OnMediaSeekRequested;
        base.OnClosed(e);
    }
}
