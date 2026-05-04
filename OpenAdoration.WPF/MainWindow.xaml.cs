using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.ViewModels;
using System.Windows;

namespace OpenAdoration.WPF;

public partial class MainWindow : Window
{
    private readonly ProjectionWindow _projectionWindow;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        MainViewModel viewModel,
        ProjectionWindow projectionWindow,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();

        _projectionWindow = projectionWindow;
        _logger           = logger;

        DataContext = viewModel;

        // Navigate to Songs on startup
        viewModel.NavigateToSongsCommand.Execute(null);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        _projectionWindow.ShowOnSecondaryScreen();
        _logger.LogInformation("MainWindow rendered, projection window launched");
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("MainWindow closing — shutting down projection window");
        _projectionWindow.Close();
        base.OnClosed(e);
    }
}
