using OpenAdoration.WPF.ViewModels;
using System.Windows;

namespace OpenAdoration.WPF.Views;

public partial class SongsView : System.Windows.Controls.UserControl
{
    public SongsView()
    {
        InitializeComponent();
    }

    // Triggers the initial data load when the view is first rendered.
    // Using code-behind instead of Behaviors to avoid an extra package dependency.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SongsViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
