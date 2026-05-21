using System.Windows;

namespace OpenAdoration.WPF.Views;

public partial class ServiceScheduleView : System.Windows.Controls.UserControl
{
    public ServiceScheduleView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ServiceScheduleViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
