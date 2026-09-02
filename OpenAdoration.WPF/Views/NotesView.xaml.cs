using System.Windows;

namespace OpenAdoration.WPF.Views;

public partial class NotesView : System.Windows.Controls.UserControl
{
    public NotesView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.NotesViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
