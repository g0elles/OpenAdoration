using Microsoft.Win32;
using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class AddEditThemeView : System.Windows.Controls.UserControl
{
    public AddEditThemeView() => InitializeComponent();

    private void OnInsertHeaderToken(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
            InsertToken(HeaderTemplateBox, btn.Tag as string ?? string.Empty);
    }

    private void OnInsertFooterToken(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
            InsertToken(FooterTemplateBox, btn.Tag as string ?? string.Empty);
    }

    private static void InsertToken(System.Windows.Controls.TextBox box, string token)
    {
        var idx  = box.CaretIndex;
        box.Text = box.Text.Insert(idx, token);
        box.CaretIndex = idx + token.Length;
        box.Focus();
    }

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
