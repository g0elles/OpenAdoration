using System.Windows;
using OpenAdoration.WPF.Localization;

namespace OpenAdoration.WPF.Views;

/// <summary>
/// Modal password prompt used by backup create (optional password, with confirmation) and
/// restore (required password, no confirmation). <see cref="Password"/> is set only when the
/// user clicks OK; null means cancelled, "" means submitted blank (only when
/// <c>allowBlank</c> was true).
/// </summary>
public partial class PasswordPromptWindow : Window
{
    private readonly bool _confirm;
    private readonly bool _allowBlank;

    public string? Password { get; private set; }

    public PasswordPromptWindow(string message, bool confirm, bool allowBlank)
    {
        InitializeComponent();
        _confirm    = confirm;
        _allowBlank = allowBlank;

        MessageText.Text = message;
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var pw1 = PasswordBox1.Password;

        if (pw1.Length == 0)
        {
            if (!_allowBlank)
            {
                ShowError("Backup_PasswordRequired");
                return;
            }
            Password = string.Empty;
            DialogResult = true;
            return;
        }

        if (_confirm && pw1 != PasswordBox2.Password)
        {
            ShowError("Backup_PasswordMismatch");
            return;
        }

        Password = pw1;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string key)
    {
        ErrorText.Text = TranslationSource.Instance[key];
        ErrorText.Visibility = Visibility.Visible;
    }
}
