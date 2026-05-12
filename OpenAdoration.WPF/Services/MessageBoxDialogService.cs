using System.Windows;

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
}
