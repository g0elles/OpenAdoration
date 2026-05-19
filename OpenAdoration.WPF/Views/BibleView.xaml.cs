using OpenAdoration.WPF.Helpers.BibleImport;
using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class BibleView : System.Windows.Controls.UserControl
{
    public BibleView()
    {
        InitializeComponent();

        // Load versions whenever the view is first displayed
        Loaded += async (_, _) =>
        {
            if (DataContext is BibleViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private async void OnImportClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Import Bible",
            Filter = BibleFormatDispatcher.FileDialogFilter
        };

        if (dialog.ShowDialog() == true && DataContext is BibleViewModel vm)
            await vm.ImportVersionCommand.ExecuteAsync(dialog.FileName);
    }
}
