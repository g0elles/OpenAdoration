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
    private readonly ILogger<ProjectionWindow> _logger;

    // True only when MainWindow explicitly calls CloseForReal() on shutdown.
    // Prevents the operator's X click from destroying this Singleton window.
    private bool _allowClose;

    // Cached theme for the current projection session. Cleared on Stop so
    // each new session fetches the latest saved theme from the database.
    private Theme? _activeTheme;

    public ProjectionWindow(
        IProjectionService   projectionService,
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectionWindow> logger)
    {
        InitializeComponent();

        _projectionService = projectionService;
        _scopeFactory      = scopeFactory;
        _logger            = logger;

        _projectionService.SlideChanged           += OnSlideChanged;
        _projectionService.ProjectionStateChanged += OnProjectionStateChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the window if it is not already visible.
    /// On dual-screen: fullscreen on the secondary monitor.
    /// On single-screen: small floating window (800×450) in the bottom-right corner.
    /// Safe to call multiple times — no-op if already shown.
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
            _logger.LogWarning("No secondary screen — opening projection as a floating window");
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

    // ── Projection service callbacks ─────────────────────────────────────────

    private async void OnSlideChanged(object? sender, Slide? slide)
    {
        // Load the theme on first slide of each session; cached for subsequent slides.
        if (_activeTheme is null)
            await LoadThemeAsync();

        Dispatcher.Invoke(() => RenderSlide(slide));
    }

    private void OnProjectionStateChanged(object? sender, bool isProjecting)
    {
        Dispatcher.Invoke(() =>
        {
            if (isProjecting)
                EnsureShown();
            else
                StopAndHide();
        });
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    private async Task LoadThemeAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var themeService = scope.ServiceProvider.GetRequiredService<IThemeService>();
            _activeTheme = await themeService.GetDefaultAsync();
            _logger.LogDebug("Loaded theme: {Name}", _activeTheme.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load default theme — using hardcoded fallback");
            _activeTheme = null;
        }
    }

    /// <summary>Applies the cached theme to all rendering elements.</summary>
    private void ApplyTheme()
    {
        if (_activeTheme is null) return;

        // Text style
        SlideTextBlock.FontFamily = new System.Windows.Media.FontFamily(_activeTheme.FontFamily);
        SlideTextBlock.FontSize   = _activeTheme.FontSize;
        SlideTextBlock.LineHeight = _activeTheme.FontSize * 1.33;
        SlideTextBlock.Foreground = HexToBrush(_activeTheme.FontColor);

        // Background color
        ThemeBackground.Fill = HexToBrush(_activeTheme.BackgroundColor);

        // Background video (highest priority — overrides image and color)
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
                _logger.LogWarning(ex, "Could not load theme background video '{Path}'", _activeTheme.BackgroundVideoPath);
                StopThemeVideo();
                ApplyThemeImage();
            }

            return; // video loaded — skip image layer
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
                ThemeBackgroundImage.Source     = new BitmapImage(new Uri(_activeTheme.BackgroundImagePath, UriKind.Absolute));
                ThemeBackgroundImage.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load theme background image '{Path}'", _activeTheme.BackgroundImagePath);
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

    // Loops the video background by seeking back to the start when it finishes.
    private void OnThemeVideoEnded(object sender, RoutedEventArgs e)
    {
        ThemeBackgroundVideo.Position = TimeSpan.Zero;
        ThemeBackgroundVideo.Play();
    }

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

    // ── Rendering ─────────────────────────────────────────────────────────────

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
                ShowText(slide.Content);
                break;

            case SlideType.Media:
                ShowMedia(slide.MediaPath);
                break;

            case SlideType.Blank:
                ShowBlankOverlay();
                break;

            default:
                _logger.LogWarning("Unknown SlideType {Type} — clearing display", slide.Type);
                ClearDisplay();
                break;
        }
    }

    private void ShowText(string content)
    {
        HideAllLayers();
        SlideTextBlock.Text    = content;
        TextViewbox.Visibility = Visibility.Visible;
    }

    private void ShowMedia(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("Media file not found at '{Path}' — showing blank", path);
            ShowBlankOverlay();
            return;
        }

        try
        {
            HideAllLayers();
            BackgroundImage.Source     = new BitmapImage(new Uri(path, UriKind.Absolute));
            BackgroundImage.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media file '{Path}'", path);
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
        StopThemeVideo();
        _activeTheme = null; // Force fresh theme load on next projection session
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
        TextViewbox.Visibility     = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Collapsed;
        BlankOverlay.Visibility    = Visibility.Collapsed;
        SlideTextBlock.Text        = string.Empty;
        BackgroundImage.Source     = null;
    }

    private void UpdateCornerLabel(Slide slide)
    {
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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

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
            Hide();
            _logger.LogInformation("Projection window hidden (X clicked)");
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _projectionService.SlideChanged           -= OnSlideChanged;
        _projectionService.ProjectionStateChanged -= OnProjectionStateChanged;
        base.OnClosed(e);
    }
}
