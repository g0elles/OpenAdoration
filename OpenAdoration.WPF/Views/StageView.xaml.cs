using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class StageView : System.Windows.Controls.UserControl
{
    public StageView() => InitializeComponent();

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StageViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
