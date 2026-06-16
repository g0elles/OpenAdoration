using System.IO;
using OpenAdoration.WPF.Helpers.BibleImport;
using OpenAdoration.WPF.Helpers.VideoPsalmMigration;
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
            Filter = BibleFormatDispatcher.FileDialogFilter + "|VideoPsalm Bible (*.vpc)|*.vpc"
        };

        if (dialog.ShowDialog() != true || DataContext is not BibleViewModel vm) return;

        // VideoPsalm .vpc Bibles are AES-encrypted (DRM) — detect and refuse plainly; never crack.
        if (Path.GetExtension(dialog.FileName).Equals(".vpc", StringComparison.OrdinalIgnoreCase)
            && VideoPsalmBibleDetector.IsDrmProtected(dialog.FileName))
        {
            System.Windows.MessageBox.Show(
                "This VideoPsalm Bible is encrypted (DRM) and can't be imported.\n\n" +
                "Obtain the version from a legal source — a public-domain or CC-licensed download, " +
                "or your own licensed copy — then import it here.",
                "Protected Bible", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        vm.ImportVersionCommand.Execute(dialog.FileName);
    }
}
