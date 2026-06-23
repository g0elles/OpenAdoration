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

    // PasswordBox.Password isn't bindable (by design), so seed it on load and push edits back manually.
    private void OnSecretLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox pb && pb.DataContext is SettingRow row)
            pb.Password = row.Value;
    }

    private void OnSecretChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox pb && pb.DataContext is SettingRow row)
            row.Value = pb.Password;
    }
}
