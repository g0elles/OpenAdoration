using OpenAdoration.Domain.Common;

namespace OpenAdoration.WPF.Views;

public partial class AddEditNoteView : System.Windows.Controls.UserControl
{
    public AddEditNoteView()
    {
        InitializeComponent();
    }

    // F8: Ctrl+B toggles **bold** markers around the content selection (mirrors AddEditSongView).
    private void OnContentPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.B
            || System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control) return;
        if (sender is not System.Windows.Controls.TextBox box) return;

        var (text, start, length) = BoldMarkup.ToggleSelection(box.Text, box.SelectionStart, box.SelectionLength);
        box.Text = text;
        box.Select(start, length);
        e.Handled = true;
    }
}
