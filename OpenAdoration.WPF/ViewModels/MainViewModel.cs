using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<MainViewModel> _logger;

    // One scope per page — disposes scoped services when the user navigates away
    private IServiceScope? _currentScope;
    // Kept alive while the user browses other pages during a live service
    private IServiceScope?              _liveServiceScope;
    private ServiceScheduleViewModel?  _liveServiceVm;

    [ObservableProperty]
    private BaseViewModel? _currentView;

    [ObservableProperty]
    private bool _isProjecting;

    [ObservableProperty]
    private bool _isScreenOpen;

    // Current slide info — drives the bottom bar
    [ObservableProperty] private string _currentSongTitle  = string.Empty;
    [ObservableProperty] private string _currentSlideLabel = string.Empty;

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

    [RelayCommand]
    private void NavigateToStage()
    {
        _logger.LogDebug("Navigating to Stage View");
        NavigateTo<StageViewModel>();
    }

    private void NavigateTo<T>() where T : BaseViewModel
    {
        if (CurrentView is T)
            return;

        var oldScope = _currentScope;

        // If leaving a live service, park the VM + scope instead of destroying them.
        // ServiceScheduleViewModel is Transient so we must store the instance directly —
        // calling GetRequiredService again on the same scope creates a new VM.
        if (CurrentView is ServiceScheduleViewModel { IsLiveMode: true } liveVm)
        {
            _liveServiceScope = oldScope;
            _liveServiceVm    = liveVm;
            oldScope          = null;
        }

        // When returning to the service schedule reuse the parked VM and its scope
        if (typeof(T) == typeof(ServiceScheduleViewModel) && _liveServiceVm != null)
        {
            _currentScope     = _liveServiceScope!;
            _liveServiceScope = null;
            var vm            = _liveServiceVm;
            _liveServiceVm    = null;
            CurrentView       = vm;
        }
        else
        {
            _currentScope = _services.CreateScope();
            CurrentView   = _currentScope.ServiceProvider.GetRequiredService<T>();
        }

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
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsProjecting = isProjecting;
            if (!isProjecting)
            {
                CurrentSongTitle  = string.Empty;
                CurrentSlideLabel = string.Empty;

                // Projection stopped while the live scope was parked — no longer needed
                _liveServiceScope?.Dispose();
                _liveServiceScope = null;
                _liveServiceVm    = null;
            }
        });
    }

    private void OnSlideChanged(object? sender, Slide? slide)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentSongTitle  = _projectionService.ContextLabel;
            CurrentSlideLabel = slide?.Label ?? string.Empty;

        });
    }

    public void Dispose()
    {
        _projectionService.ProjectionStateChanged -= OnProjectionStateChanged;
        _projectionService.SlideChanged           -= OnSlideChanged;
        _currentScope?.Dispose();
        _liveServiceScope?.Dispose();
        _currentScope     = null;
        _liveServiceScope = null;
        _liveServiceVm    = null;
    }
}
