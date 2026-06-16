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
        _vm = e.NewValue as StageViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StageViewModel.CurrentPreview) or nameof(StageViewModel.NextPreview))
            Dispatcher.InvokeAsync(SyncVideoSources);
    }

    private void SyncVideoSources()
    {
        if (_vm is null) return;
        SyncVideo(CurrentVideoMedia, _vm.CurrentPreview);
        SyncVideo(NextVideoMedia, _vm.NextPreview);
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

    private async void OnCurrentVideoEnded(object? sender, EventArgs e) => await LoopAsync(CurrentVideoMedia);

    private async void OnNextVideoEnded(object? sender, EventArgs e) => await LoopAsync(NextVideoMedia);

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
