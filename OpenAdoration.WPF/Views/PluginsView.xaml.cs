using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class PluginsView : System.Windows.Controls.UserControl
{
    public PluginsView() => InitializeComponent();

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PluginsViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
