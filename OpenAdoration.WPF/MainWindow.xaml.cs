using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.ViewModels;
using OpenAdoration.WPF.Views;
using System.Windows;
using System.Windows.Input;

namespace OpenAdoration.WPF;

public partial class MainWindow : Window
{
    private readonly ProjectionWindow    _projectionWindow;
    private readonly PresenterWindow     _presenterWindow;
    private readonly IProjectionService  _projectionService;
    private readonly MainViewModel       _viewModel;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        MainViewModel viewModel,
        ProjectionWindow projectionWindow,
        PresenterWindow presenterWindow,
        IProjectionService projectionService,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();

        _projectionWindow  = projectionWindow;
        _presenterWindow   = presenterWindow;
        _projectionService = projectionService;
        _viewModel         = viewModel;
        _logger            = logger;

        DataContext = viewModel;

        _projectionWindow.IsVisibleChanged += (_, _) =>
            viewModel.IsScreenOpen = _projectionWindow.IsVisible;

        viewModel.NavigateToSongsCommand.Execute(null);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void OnToggleScreenClick(object sender, RoutedEventArgs e)
    {
        if (_projectionWindow.IsVisible)
        {
            _logger.LogInformation("Operator closed projection screen");
            if (_projectionService.IsProjecting)
                _projectionService.Stop();
            else
                _projectionWindow.Hide();
        }
        else
        {
            _logger.LogInformation("Operator opened projection screen manually");
            _projectionWindow.EnsureShown();
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Never steal keys from text inputs
        if (FocusManager.GetFocusedElement(this) is
            System.Windows.Controls.Primitives.TextBoxBase or
            System.Windows.Controls.TextBox or
            System.Windows.Controls.ComboBox)
            return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        // Ctrl+1–5: tab navigation (always available)
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.D1: _viewModel.NavigateToSongsCommand.Execute(null);   e.Handled = true; return;
                case Key.D2: _viewModel.NavigateToBibleCommand.Execute(null);   e.Handled = true; return;
                case Key.D3: _viewModel.NavigateToScheduleCommand.Execute(null); e.Handled = true; return;
                case Key.D4: _viewModel.NavigateToMediaCommand.Execute(null);   e.Handled = true; return;
                case Key.D5: _viewModel.NavigateToThemesCommand.Execute(null);  e.Handled = true; return;
            }
            return;
        }

        // Projection shortcuts — only active while projecting
        if (!_projectionService.IsProjecting)
            return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.Right:
            case Key.PageDown:
                _projectionService.Next();
                e.Handled = true;
                break;

            case Key.Left:
            case Key.PageUp:
                _projectionService.Previous();
                e.Handled = true;
                break;

            case Key.B:
                _projectionService.ShowBlank();
                e.Handled = true;
                break;

            case Key.Escape:
                _projectionService.Stop();
                _logger.LogInformation("Projection stopped via keyboard");
                e.Handled = true;
                break;

            case Key.D1: case Key.NumPad1: _projectionService.GoTo(0); e.Handled = true; break;
            case Key.D2: case Key.NumPad2: _projectionService.GoTo(1); e.Handled = true; break;
            case Key.D3: case Key.NumPad3: _projectionService.GoTo(2); e.Handled = true; break;
            case Key.D4: case Key.NumPad4: _projectionService.GoTo(3); e.Handled = true; break;
            case Key.D5: case Key.NumPad5: _projectionService.GoTo(4); e.Handled = true; break;
            case Key.D6: case Key.NumPad6: _projectionService.GoTo(5); e.Handled = true; break;
            case Key.D7: case Key.NumPad7: _projectionService.GoTo(6); e.Handled = true; break;
            case Key.D8: case Key.NumPad8: _projectionService.GoTo(7); e.Handled = true; break;
            case Key.D9: case Key.NumPad9: _projectionService.GoTo(8); e.Handled = true; break;
        }
    }

    private void OnTogglePresenterClick(object sender, RoutedEventArgs e)
    {
        if (_presenterWindow.IsVisible)
            _presenterWindow.Hide();
        else
            _presenterWindow.Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("MainWindow closing — shutting down projection window");
        _projectionWindow.CloseForReal();
        _presenterWindow.CloseForReal();
        base.OnClosed(e);
    }
}
