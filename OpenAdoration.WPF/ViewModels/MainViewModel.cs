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

    /// <summary>
    /// The scope that owns the current navigation target's scoped dependencies
    /// (repositories, services, DbContext factories). Created fresh on every
    /// navigation and disposed when the user navigates away, so nothing leaks
    /// into the root container.
    /// </summary>
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a fresh DI scope, resolves <typeparamref name="TViewModel"/> from it,
    /// assigns it to <see cref="CurrentView"/>, then disposes the previous scope.
    /// <para>
    /// Using a scope per navigation ensures that scoped services (ISongService,
    /// IDbContextFactory, etc.) are never captured by the root container, preventing
    /// DbContext reuse across unrelated operations and memory leaks on long sessions.
    /// </para>
    /// </summary>
    private void NavigateTo<TViewModel>() where TViewModel : BaseViewModel
    {
        // Create the new scope first — if this throws nothing is broken yet.
        var newScope = _services.CreateScope();
        BaseViewModel vm;

        try
        {
            vm = newScope.ServiceProvider.GetRequiredService<TViewModel>();
        }
        catch
        {
            newScope.Dispose();
            throw;
        }

        // Swap scope and view atomically (single-threaded UI thread).
        CurrentView = vm;

        var oldScope  = _currentScope;
        _currentScope = newScope;

        // Disposing the old scope tears down all scoped services the previous VM owned.
        oldScope?.Dispose();
    }
}
