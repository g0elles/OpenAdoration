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

    public ProjectionWindow(IProjectionService projectionService, ILogger<ProjectionWindow> logger)
    {
        InitializeComponent();

        _projectionService = projectionService;
        _logger = logger;

        _projectionService.SlideChanged          += OnSlideChanged;
        _projectionService.ProjectionStateChanged += OnProjectionStateChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Positions the window on the secondary monitor and makes it visible.
    /// If no secondary monitor is found, positions it on the primary as a fallback.
    /// </summary>
    public void ShowOnSecondaryScreen()
    {
        var secondary = ScreenHelper.GetSecondaryScreen();

        if (secondary is null)
        {
            _logger.LogWarning("No secondary screen detected — projection window will open on the primary screen");
        }
        else
        {
            _logger.LogInformation("Projecting on: {Screen}", secondary.DeviceName);
            ScreenHelper.PositionOnScreen(this, secondary);
        }

        WindowState = WindowState.Maximized;
        Show();
    }

    // ── Projection service callbacks ─────────────────────────────────────────

    private void OnSlideChanged(object? sender, Slide? slide)
    {
        // Always marshal back to the UI thread
        Dispatcher.Invoke(() => RenderSlide(slide));
    }

    private void OnProjectionStateChanged(object? sender, bool isProjecting)
    {
        Dispatcher.Invoke(() =>
        {
            if (!isProjecting)
                ClearDisplay();
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
        SlideTextBlock.Text = content;
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
            BackgroundImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
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

    private void ClearDisplay()
    {
        HideAllLayers();
    }

    private void HideAllLayers()
    {
        TextViewbox.Visibility     = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Collapsed;
        BlankOverlay.Visibility    = Visibility.Collapsed;
        SlideTextBlock.Text        = string.Empty;
        BackgroundImage.Source     = null;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _projectionService.SlideChanged          -= OnSlideChanged;
        _projectionService.ProjectionStateChanged -= OnProjectionStateChanged;
        base.OnClosed(e);
    }
}
