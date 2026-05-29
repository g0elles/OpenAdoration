using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using System.Windows;
using System.Windows.Input; // Key enum

namespace OpenAdoration.WPF;

public partial class PresenterWindow : Window
{
    private readonly IProjectionService _projectionService;
    private bool _allowClose;

    public PresenterWindow(IProjectionService projectionService)
    {
        InitializeComponent();

        _projectionService = projectionService;
        _projectionService.SlideChanged           += OnSlideChanged;
        _projectionService.ProjectionStateChanged += OnProjectionStateChanged;

        UpdateDisplay();
    }

    // -- Public API -------------------------------------------------------

    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    // -- Event handlers ---------------------------------------------------

    private void OnSlideChanged(object? sender, Slide? slide)
        => Dispatcher.InvokeAsync(UpdateDisplay);

    private void OnProjectionStateChanged(object? sender, bool isProjecting)
        => Dispatcher.InvokeAsync(UpdateDisplay);

    // -- Display update ---------------------------------------------------

    private void UpdateDisplay()
    {
        var slides      = _projectionService.CurrentSlides;
        var idx         = _projectionService.CurrentSlideIndex;
        var current     = _projectionService.CurrentSlide;
        var isProjecting = _projectionService.IsProjecting;

        // Status badge
        LiveBadge.Visibility    = isProjecting ? Visibility.Visible   : Visibility.Collapsed;
        StoppedBadge.Visibility = isProjecting ? Visibility.Collapsed : Visibility.Visible;

        // Context label + slide position
        ContextLabelText.Text = _projectionService.ContextLabel;
        SlideCountText.Text   = isProjecting && slides.Count > 0
            ? $"{idx + 1} / {slides.Count}"
            : string.Empty;

        // Current slide
        if (current is not null)
        {
            CurrentSectionLabel.Text = current.Type == SlideType.Blank ? "BLANK" : current.Label;
            CurrentSlideText.Text    = current.Type switch
            {
                SlideType.Blank => string.Empty,
                SlideType.Media => "[Media]",
                _               => current.Content
            };
        }
        else
        {
            CurrentSectionLabel.Text = string.Empty;
            CurrentSlideText.Text    = string.Empty;
        }

        // Next slide
        var nextIdx = idx + 1;
        if (isProjecting && nextIdx < slides.Count)
        {
            var next = slides[nextIdx];
            NextSlideText.Text   = next.Type switch
            {
                SlideType.Blank => "[Blank]",
                SlideType.Media => "[Media]",
                _               => next.Content
            };
            NextSectionLabel.Text          = next.Label;
            NextEndPlaceholder.Visibility  = Visibility.Collapsed;
            NextSlideScroll.Visibility     = Visibility.Visible;
        }
        else
        {
            NextSlideText.Text            = string.Empty;
            NextSectionLabel.Text         = string.Empty;
            NextEndPlaceholder.Visibility = Visibility.Visible;
            NextSlideScroll.Visibility    = Visibility.Collapsed;
        }
    }

    // -- Keyboard passthrough: Space/arrows advance projection ------------

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!_projectionService.IsProjecting) return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.Right:
            case Key.PageDown:
                _projectionService.Next();
                e.Handled = true;
                break;

            case Key.Left:
            case Key.PageUp:
                _projectionService.Previous();
                e.Handled = true;
                break;

            case Key.B:
                _projectionService.ShowBlank();
                e.Handled = true;
                break;

            case Key.Escape:
                _projectionService.Stop();
                e.Handled = true;
                break;
        }
    }

    // -- Lifecycle --------------------------------------------------------

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
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
