using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class ThemeView : System.Windows.Controls.UserControl
{
    public ThemeView() => InitializeComponent();

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ThemeViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
