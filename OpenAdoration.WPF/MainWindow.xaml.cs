using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.ViewModels;
using System.Windows;

namespace OpenAdoration.WPF;

public partial class MainWindow : Window
{
    private readonly ProjectionWindow    _projectionWindow;
    private readonly IProjectionService  _projectionService;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        MainViewModel viewModel,
        ProjectionWindow projectionWindow,
        IProjectionService projectionService,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();

        _projectionWindow  = projectionWindow;
        _projectionService = projectionService;
        _logger            = logger;

        DataContext = viewModel;

        _projectionWindow.IsVisibleChanged += (_, _) =>
            viewModel.IsScreenOpen = _projectionWindow.IsVisible;

        viewModel.NavigateToSongsCommand.Execute(null);
    }

    private void OnToggleScreenClick(object sender, RoutedEventArgs e)
    {
        if (_projectionWindow.IsVisible)
        {
            _logger.LogInformation("Operator closed projection screen");
            if (_projectionService.IsProjecting)
                _projectionService.Stop(); // Stop fires StopAndHide() via event — no manual Hide() needed
            else
                _projectionWindow.Hide();
        }
        else
        {
            _logger.LogInformation("Operator opened projection screen manually");
            _projectionWindow.EnsureShown();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("MainWindow closing — shutting down projection window");
        _projectionWindow.CloseForReal();
        base.OnClosed(e);
    }
}
