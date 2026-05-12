using System.Windows;

namespace OpenAdoration.WPF.Views;

public partial class SongsView : System.Windows.Controls.UserControl
{
    public SongsView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SongsViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
