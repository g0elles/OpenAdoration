using OpenAdoration.WPF.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace OpenAdoration.WPF.Views;

public partial class StageView : System.Windows.Controls.UserControl
{
    private StageViewModel? _vm;

    public StageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        StopLowerThirdTicker();
        _vm = e.NewValue as StageViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopLowerThirdTicker();

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StageViewModel.CurrentPreview) or nameof(StageViewModel.NextPreview))
            Dispatcher.InvokeAsync(SyncVideoSources);
        else if (e.PropertyName is nameof(StageViewModel.IsPreviewVideoPlaying))
            Dispatcher.InvokeAsync(SyncVideoPlayback);
        else if (e.PropertyName is nameof(StageViewModel.LowerThirdText))
            Dispatcher.InvokeAsync(SyncLowerThirdTicker);
    }

    // Mirrors ProjectionWindow's ticker (continuous right-to-left marquee, constant speed)
    // so the stage monitor shows the same scrolling behaviour the projector is running.
    private void SyncLowerThirdTicker()
    {
        if (_vm is null) return;
        StopLowerThirdTicker();
        if (_vm.HasLowerThird && _vm.LowerThirdScrollEnabled) StartLowerThirdTicker();
    }

    private void StartLowerThirdTicker()
    {
        LowerThirdTextBlock.TextWrapping        = TextWrapping.NoWrap;
        LowerThirdTextBlock.TextAlignment       = TextAlignment.Left;
        LowerThirdTextBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;

        LowerThirdBar.UpdateLayout();
        var textWidth = LowerThirdTextBlock.ActualWidth;
        var barWidth  = LowerThirdBar.ActualWidth;
        if (textWidth <= 0 || barWidth <= 0 || _vm is null) return;

        var speed    = Math.Max(10, _vm.LowerThirdScrollSpeed);
        var duration = TimeSpan.FromSeconds((barWidth + textWidth) / speed);

        var translate = new System.Windows.Media.TranslateTransform();
        LowerThirdTextBlock.RenderTransform = translate;
        translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(barWidth, -textWidth, duration)
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            });
    }

    private void StopLowerThirdTicker()
    {
        if (LowerThirdTextBlock.RenderTransform is System.Windows.Media.TranslateTransform t)
            t.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        LowerThirdTextBlock.RenderTransform = System.Windows.Media.Transform.Identity;

        LowerThirdTextBlock.TextWrapping        = TextWrapping.Wrap;
        LowerThirdTextBlock.TextAlignment       = TextAlignment.Center;
        LowerThirdTextBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
    }

    // Mirror the projector's play/pause onto the current-slide preview (UP NEXT keeps looping).
    private async void SyncVideoPlayback()
    {
        if (_vm is null) return;
        try
        {
            if (_vm.IsPreviewVideoPlaying) await CurrentVideoMedia.Play();
            else await CurrentVideoMedia.Pause();
        }
        catch { /* preview transport is best-effort */ }
    }

    private void SyncVideoSources()
    {
        if (_vm is null) return;
        SyncVideo(CurrentVideoMedia, _vm.CurrentPreview);
        SyncVideo(NextVideoMedia, _vm.NextPreview);
        SyncBgVideo(CurrentBgVideo, _vm.CurrentPreview);
        SyncBgVideo(NextBgVideo, _vm.NextPreview);
    }

    // FFME opens + plays via LoadedBehavior="Play"; Open/Close are async and set/clear Source.
    private static async void SyncVideo(Unosquare.FFME.MediaElement element, SlidePreview preview)
    {
        try
        {
            if (preview.IsVideoMedia && !string.IsNullOrEmpty(preview.MediaPath))
            {
                var uri = new Uri(preview.MediaPath, UriKind.Absolute);
                if (element.Source != uri) await element.Open(uri);
            }
            else if (element.Source is not null)
            {
                await element.Close();
            }
        }
        catch
        {
            // Preview is non-critical; an open/decode failure must not crash the stage view
            // (the projector path logs its own failures).
        }
    }

    // Theme background video: opens BgVideoPath so the preview mirrors the projector's ambient loop.
    private static async void SyncBgVideo(Unosquare.FFME.MediaElement element, SlidePreview preview)
    {
        try
        {
            if (preview.HasBgVideo && !string.IsNullOrEmpty(preview.BgVideoPath))
            {
                var uri = new Uri(preview.BgVideoPath, UriKind.Absolute);
                if (element.Source != uri) await element.Open(uri);
            }
            else if (element.Source is not null)
            {
                await element.Close();
            }
        }
        catch
        {
            // Preview is non-critical; a decode failure must not crash the stage view.
        }
    }

    private async void OnCurrentVideoEnded(object? sender, EventArgs e) => await LoopAsync(CurrentVideoMedia);

    private async void OnNextVideoEnded(object? sender, EventArgs e) => await LoopAsync(NextVideoMedia);

    private async void OnCurrentBgVideoEnded(object? sender, EventArgs e) => await LoopAsync(CurrentBgVideo);

    private async void OnNextBgVideoEnded(object? sender, EventArgs e) => await LoopAsync(NextBgVideo);

    private static async Task LoopAsync(Unosquare.FFME.MediaElement element)
    {
        try
        {
            await element.Seek(TimeSpan.Zero);
            await element.Play();
        }
        catch { /* preview loop is best-effort */ }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StageViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
