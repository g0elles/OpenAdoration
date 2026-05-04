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
        CurrentView = _services.GetRequiredService<SongsViewModel>();
    }

    [RelayCommand]
    private void NavigateToBible()
    {
        _logger.LogDebug("Navigating to Bible");
        CurrentView = _services.GetRequiredService<BibleViewModel>();
    }

    [RelayCommand]
    private void NavigateToSchedule()
    {
        _logger.LogDebug("Navigating to Service Schedule");
        CurrentView = _services.GetRequiredService<ServiceScheduleViewModel>();
    }

    [RelayCommand]
    private void NavigateToMedia()
    {
        _logger.LogDebug("Navigating to Media");
        CurrentView = _services.GetRequiredService<MediaViewModel>();
    }

    [RelayCommand]
    private void NavigateToThemes()
    {
        _logger.LogDebug("Navigating to Themes");
        CurrentView = _services.GetRequiredService<ThemeViewModel>();
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
