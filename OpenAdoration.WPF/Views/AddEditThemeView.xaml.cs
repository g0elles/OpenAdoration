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
