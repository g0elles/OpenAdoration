using System;
using System.ComponentModel;
using Microsoft.Win32;
using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class AddEditThemeView : System.Windows.Controls.UserControl
{
    private AddEditThemeViewModel? _vm;
    private string? _openedVideoPath;

    public AddEditThemeView()
    {
        InitializeComponent();
        Loaded             += (_, _) => SyncPreviewVideo();
        Unloaded           += (_, _) => { _openedVideoPath = null; _ = PreviewVideo.Close(); };
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as AddEditThemeViewModel;
        if (_vm != null) _vm.PropertyChanged += OnVmPropertyChanged;
        SyncPreviewVideo();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AddEditThemeViewModel.BackgroundVideoPath)
                           or nameof(AddEditThemeViewModel.HasBackgroundVideo))
            SyncPreviewVideo();

        if (e.PropertyName is nameof(AddEditThemeViewModel.SelectedTransition))
            PlayTransitionPreview();
    }

    // Replays the picked transition on the sample lyrics so the operator sees it before Sunday.
    // Mirrors ProjectionWindow's kinds; only the text animates (theme background stays static there too).
    // ponytail: fixed 400 ms preview — the real duration is the global Settings value.
    private void PlayTransitionPreview()
    {
        var kind = _vm?.SelectedTransition?.Kind;
        if (kind is null || !IsLoaded) return;

        var duration = TimeSpan.FromMilliseconds(400);
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };

        PreviewLyrics.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
        PreviewLyrics.Opacity = 1;
        PreviewLyrics.RenderTransform = System.Windows.Media.Transform.Identity;

        switch (kind)
        {
            case OpenAdoration.Domain.Common.SlideTransitionKind.Slide:
                var translate = new System.Windows.Media.TranslateTransform();
                PreviewLyrics.RenderTransform = translate;
                translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(1920, 0, duration) { EasingFunction = ease });
                break;

            case OpenAdoration.Domain.Common.SlideTransitionKind.Zoom:
                PreviewLyrics.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                var scale = new System.Windows.Media.ScaleTransform(0.85, 0.85);
                PreviewLyrics.RenderTransform = scale;
                scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0.85, 1, duration) { EasingFunction = ease });
                scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0.85, 1, duration) { EasingFunction = ease });
                PreviewLyrics.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration));
                break;

            default: // Fade
                PreviewLyrics.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration));
                break;
        }
    }

    // Open the chosen video for muted looped preview, or release it when there's none.
    private void SyncPreviewVideo()
    {
        if (!IsLoaded) return;
        var path = _vm is { HasBackgroundVideo: true } ? _vm.BackgroundVideoPath : null;
        if (path == _openedVideoPath) return;
        _openedVideoPath = path;
        if (!string.IsNullOrWhiteSpace(path))
            _ = PreviewVideo.Open(new Uri(path, UriKind.Absolute));
        else
            _ = PreviewVideo.Close();
    }

    private async void OnPreviewVideoEnded(object? sender, EventArgs e)
    {
        await PreviewVideo.Seek(TimeSpan.Zero);
        await PreviewVideo.Play();
    }

    private void OnInsertHeaderToken(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
            InsertToken(HeaderTemplateBox, btn.Tag as string ?? string.Empty);
    }

    private void OnInsertFooterToken(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
            InsertToken(FooterTemplateBox, btn.Tag as string ?? string.Empty);
    }

    private static void InsertToken(System.Windows.Controls.TextBox box, string token)
    {
        var idx  = box.CaretIndex;
        box.Text = box.Text.Insert(idx, token);
        box.CaretIndex = idx + token.Length;
        box.Focus();
    }

    private async void OnBrowseImageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Background Image",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is AddEditThemeViewModel vm)
            await vm.ImportBackgroundFileAsync(dialog.FileName, isVideo: false);
    }

    private void OnClearImageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AddEditThemeViewModel vm)
            vm.BackgroundImagePath = null;
    }

    private async void OnBrowseVideoClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Background Video",
            Filter = "Video files|*.mp4;*.wmv;*.avi;*.mov;*.mkv|All files|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is AddEditThemeViewModel vm)
            await vm.ImportBackgroundFileAsync(dialog.FileName, isVideo: true);
    }

    private void OnClearVideoClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AddEditThemeViewModel vm)
            vm.BackgroundVideoPath = null;
    }
}
