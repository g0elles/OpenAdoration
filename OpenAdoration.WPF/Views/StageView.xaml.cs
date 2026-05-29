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

    private static void SyncVideo(System.Windows.Controls.MediaElement element, SlidePreview preview)
    {
        if (preview.IsVideoMedia && !string.IsNullOrEmpty(preview.MediaPath))
        {
            var uri = new Uri(preview.MediaPath, UriKind.Absolute);
            if (element.Source != uri)
            {
                element.Source = uri;
                element.Play();
            }
        }
        else if (element.Source is not null)
        {
            element.Stop();
            element.Source = null;
        }
    }

    private void OnCurrentVideoEnded(object sender, RoutedEventArgs e)
    {
        CurrentVideoMedia.Position = TimeSpan.Zero;
        CurrentVideoMedia.Play();
    }

    private void OnNextVideoEnded(object sender, RoutedEventArgs e)
    {
        NextVideoMedia.Position = TimeSpan.Zero;
        NextVideoMedia.Play();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StageViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
