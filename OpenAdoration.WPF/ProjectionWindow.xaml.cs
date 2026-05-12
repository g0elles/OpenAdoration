using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace OpenAdoration.WPF;

public partial class ProjectionWindow : Window
{
    private readonly IProjectionService _projectionService;
    private readonly ILogger<ProjectionWindow> _logger;

    // True only when MainWindow explicitly calls CloseForReal() on shutdown.
    // Prevents the operator's X click from destroying this Singleton window.
    private bool _allowClose;

    public ProjectionWindow(IProjectionService projectionService, ILogger<ProjectionWindow> logger)
    {
        InitializeComponent();

        _projectionService = projectionService;
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
            // Dual-screen: fullscreen on the secondary monitor
            WindowStyle = WindowStyle.None;
            ResizeMode  = ResizeMode.NoResize;
            ShowOnSecondaryScreen(secondary);
        }
        else
        {
            // Single-screen: floating, resizable, positioned bottom-right
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

    /// <summary>Legacy helper — positions fullscreen on the given screen.</summary>
    private void ShowOnSecondaryScreen(System.Windows.Forms.Screen screen)
    {
        _logger.LogInformation("Projecting on: {Screen}", screen.DeviceName);
        ScreenHelper.PositionOnScreen(this, screen);
        WindowState = WindowState.Maximized;
        Show();
    }

    // ── Projection service callbacks ─────────────────────────────────────────

    private void OnSlideChanged(object? sender, Slide? slide)
    {
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

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void RenderSlide(Slide? slide)
    {
        if (slide is null)
        {
            ClearDisplay();
            return;
        }

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
        var songTitle = _projectionService.ContextLabel;

        if (string.IsNullOrWhiteSpace(songTitle))
        {
            CornerLabel.Visibility = Visibility.Collapsed;
            return;
        }

        CornerSongTitle.Text    = songTitle;
        CornerSectionLabel.Text = slide.Label;
        CornerLabel.Visibility  = Visibility.Visible;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MainWindow on app shutdown — the only path that actually closes the window.
    /// </summary>
    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            // Operator clicked X — hide instead of destroying the Singleton window
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
