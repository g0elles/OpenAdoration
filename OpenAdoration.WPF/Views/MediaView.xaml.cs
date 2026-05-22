using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class MediaView : System.Windows.Controls.UserControl
{
    public MediaView() => InitializeComponent();

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MediaViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
