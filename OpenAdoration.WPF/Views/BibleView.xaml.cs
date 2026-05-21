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

        // Release the singleton ProjectionService subscription when the view leaves
        // the visual tree, preventing an event-leak on the transient BibleViewModel (R2)
        Unloaded += (_, _) =>
        {
            if (DataContext is BibleViewModel vm)
                vm.Dispose();
        };
    }

    private void OnImportClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Import Bible",
            Filter = BibleFormatDispatcher.FileDialogFilter
        };

        if (dialog.ShowDialog() == true && DataContext is BibleViewModel vm)
            vm.ImportVersionCommand.Execute(dialog.FileName);
    }
}
