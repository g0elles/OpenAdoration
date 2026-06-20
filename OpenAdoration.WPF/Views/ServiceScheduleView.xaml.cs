using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OpenAdoration.WPF.ViewModels;

// UseWindowsForms=true makes these names ambiguous (G1) — pin them to WPF.
using Point = System.Windows.Point;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace OpenAdoration.WPF.Views;

public partial class ServiceScheduleView : System.Windows.Controls.UserControl
{
    public ServiceScheduleView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServiceScheduleViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }

    // ── Drag-and-drop reorder of schedule items (the ▲▼ arrows still work) ──
    // Hand-rolled so we add no dependency. A drag starts from a row's empty area; presses that
    // land on a Button/TextBox fall through so the existing controls keep working.
    private Point _dragStart;
    private ScheduleItemViewModel? _dragItem;

    private void OnScheduleRowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StartsOnInteractiveControl(e.OriginalSource as DependencyObject))
        {
            _dragItem = null;
            return;
        }
        _dragStart = e.GetPosition(null);
        _dragItem = (sender as FrameworkElement)?.DataContext as ScheduleItemViewModel;
    }

    private void OnScheduleRowMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _dragItem;
        _dragItem = null; // one drag per gesture
        DragDrop.DoDragDrop((DependencyObject)sender, item, DragDropEffects.Move);
    }

    private void OnScheduleRowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ScheduleItemViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnScheduleRowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ScheduleItemViewModel)) is not ScheduleItemViewModel source) return;
        if ((sender as FrameworkElement)?.DataContext is not ScheduleItemViewModel target) return;
        if (DataContext is not ServiceScheduleViewModel vm) return;

        e.Handled = true;
        await vm.MoveItemAsync(source, target);
    }

    private static bool StartsOnInteractiveControl(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase
                     or System.Windows.Controls.Primitives.TextBoxBase)
                return true;
        }
        return false;
    }
}
