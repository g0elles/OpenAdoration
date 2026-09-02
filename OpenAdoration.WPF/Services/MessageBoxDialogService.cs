using System.Windows;
using OpenAdoration.WPF.Views;

namespace OpenAdoration.WPF.Services;

public sealed class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string message, string title = "Confirm")
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public void Inform(string message, string title = "OpenAdoration") =>
        System.Windows.MessageBox.Show(
            message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? PromptPassword(string message, bool confirm, bool allowBlank, string title = "OpenAdoration")
    {
        var dlg = new PasswordPromptWindow(message, confirm, allowBlank)
        {
            Owner = System.Windows.Application.Current.MainWindow,
            Title = title
        };
        return dlg.ShowDialog() == true ? dlg.Password : null;
    }
}
