using Microsoft.Win32;
using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class AddEditThemeView : System.Windows.Controls.UserControl
{
    public AddEditThemeView() => InitializeComponent();

    private void OnBrowseImageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Background Image",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is AddEditThemeViewModel vm)
            vm.BackgroundImagePath = dialog.FileName;
    }

    private void OnClearImageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AddEditThemeViewModel vm)
            vm.BackgroundImagePath = null;
    }

    private void OnBrowseVideoClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Background Video",
            Filter = "Video files|*.mp4;*.wmv;*.avi;*.mov;*.mkv|All files|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is AddEditThemeViewModel vm)
            vm.BackgroundVideoPath = dialog.FileName;
    }

    private void OnClearVideoClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AddEditThemeViewModel vm)
            vm.BackgroundVideoPath = null;
    }
}
