using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<MainViewModel> _logger;

    // One scope per page — disposes scoped services when the user navigates away
    private IServiceScope? _currentScope;

    [ObservableProperty]
    private BaseViewModel? _currentView;

    [ObservableProperty]
    private bool _isProjecting;

    [ObservableProperty]
    private string _currentSlideLabel = string.Empty;

    public MainViewModel(
        IServiceProvider services,
        IProjectionService projectionService,
        ILogger<MainViewModel> logger)
    {
        _services          = services;
        _projectionService = projectionService;
        _logger            = logger;

        _projectionService.ProjectionStateChanged += OnProjectionStateChanged;
        _projectionService.SlideChanged           += OnSlideChanged;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void NavigateToSongs()
    {
        _logger.LogDebug("Navigating to Songs");
        NavigateTo<SongsViewModel>();
    }

    [RelayCommand]
    private void NavigateToBible()
    {
        _logger.LogDebug("Navigating to Bible");
        NavigateTo<BibleViewModel>();
    }

    [RelayCommand]
    private void NavigateToSchedule()
    {
        _logger.LogDebug("Navigating to Service Schedule");
        NavigateTo<ServiceScheduleViewModel>();
    }

    [RelayCommand]
    private void NavigateToMedia()
    {
        _logger.LogDebug("Navigating to Media");
        NavigateTo<MediaViewModel>();
    }

    [RelayCommand]
    private void NavigateToThemes()
    {
        _logger.LogDebug("Navigating to Themes");
        NavigateTo<ThemeViewModel>();
    }

    private void NavigateTo<T>() where T : BaseViewModel
    {
        var oldScope   = _currentScope;
        _currentScope  = _services.CreateScope();
        CurrentView    = _currentScope.ServiceProvider.GetRequiredService<T>();
        oldScope?.Dispose();
    }

    // ── Projection controls ───────────────────────────────────────────────────

    [RelayCommand]
    private void ProjectionNext() => _projectionService.Next();

    [RelayCommand]
    private void ProjectionPrevious() => _projectionService.Previous();

    [RelayCommand]
    private void ProjectionBlank() => _projectionService.ShowBlank();

    [RelayCommand]
    private void ProjectionStop()
    {
        _projectionService.Stop();
        _logger.LogInformation("Projection stopped by operator");
    }

    // ── Projection state sync ─────────────────────────────────────────────────

    private void OnProjectionStateChanged(object? sender, bool isProjecting)
    {
        IsProjecting = isProjecting;
    }

    private void OnSlideChanged(object? sender, Application.Common.Slide? slide)
    {
        CurrentSlideLabel = slide?.Label ?? string.Empty;
    }
}
