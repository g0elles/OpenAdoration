using System.Collections.Specialized;
using System.Windows.Threading;
using OpenAdoration.Domain.Common;
using OpenAdoration.WPF.ViewModels;

namespace OpenAdoration.WPF.Views;

public partial class AddEditSongView : System.Windows.Controls.UserControl
{
    public AddEditSongView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AddEditSongViewModel old)
            old.Sections.CollectionChanged -= OnSectionsChanged;

        if (e.NewValue is AddEditSongViewModel next)
            next.Sections.CollectionChanged += OnSectionsChanged;
    }

    private void OnSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // Wait for the new card to be rendered before scrolling
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FormScroller.ScrollToBottom());
    }

    // F8: Ctrl+B toggles **bold** markers around the lyrics selection.
    private void OnLyricsPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
