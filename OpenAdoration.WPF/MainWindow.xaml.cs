using Microsoft.Extensions.Logging;
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

        viewModel.NavigateToSongsCommand.Execute(null);
    }

    // "Open Screen" button — shows the projection window without starting projection
    private void OnOpenScreenClick(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Operator opened projection screen manually");
        _projectionWindow.EnsureShown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("MainWindow closing — shutting down projection window");
        _projectionWindow.Close();
        base.OnClosed(e);
    }
}
