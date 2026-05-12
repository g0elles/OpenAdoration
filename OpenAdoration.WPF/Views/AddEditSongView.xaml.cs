using System.Collections.Specialized;
using System.Windows.Threading;
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
}
