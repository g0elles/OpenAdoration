using System.Windows.Threading;
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
    private readonly IAppSettingsService _appSettings;
    private readonly ILogger<MainViewModel> _logger;

    private DispatcherTimer? _announcementTimer;

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

    // Live announcement overlay
    [ObservableProperty] private string _announcementText = string.Empty;
    [ObservableProperty] private bool   _isAnnouncementActive;

    // Persistent lower-third overlay (M10.2) — stays until cleared, no auto-dismiss
    [ObservableProperty] private string _lowerThirdText = string.Empty;
    [ObservableProperty] private bool   _isLowerThirdActive;

    // Video transport bar — visible only when the current slide is a video (M10.5)
    [ObservableProperty] private bool   _isVideoSlideActive;
    [ObservableProperty] private bool   _isMediaPlaying;
    [ObservableProperty] private string _mediaPlayPauseGlyph = PlayGlyph;
    [ObservableProperty] private double _mediaDurationSeconds;
    [ObservableProperty] private double _mediaPositionSeconds;
    [ObservableProperty] private string _mediaTimeText = "0:00 / 0:00";

    private const string PlayGlyph  = "▶";
    private const string PauseGlyph = "⏸";
    private static readonly TimeSpan SeekStep = TimeSpan.FromSeconds(10);

    public MainViewModel(
        IServiceProvider services,
        IProjectionService projectionService,
        IAppSettingsService appSettings,
        ILogger<MainViewModel> logger)
    {
        _services          = services;
        _projectionService = projectionService;
        _appSettings       = appSettings;
        _logger            = logger;

        _projectionService.ProjectionStateChanged += OnProjectionStateChanged;
        _projectionService.SlideChanged           += OnSlideChanged;
        _projectionService.AnnouncementChanged    += OnAnnouncementChanged;
        _projectionService.LowerThirdChanged      += OnLowerThirdChanged;
        _projectionService.VideoSlideActiveChanged += OnVideoSlideActiveChanged;
        _projectionService.MediaTransportChanged   += OnMediaTransportChanged;
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

    [RelayCommand]
    private void NavigateToSettings()
    {
        _logger.LogDebug("Navigating to Settings");
        NavigateTo<SettingsViewModel>();
    }

    private void NavigateTo<T>() where T : BaseViewModel
    {
        if (CurrentView is T)
            return;

        // Prompt to save/discard pending Settings edits before tearing down its scope.
        (CurrentView as SettingsViewModel)?.OnLeaving();

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

    // ── Video transport (M10.5) ───────────────────────────────────────────────

    [RelayCommand]
    private void MediaTogglePlayPause() => _projectionService.RequestMediaCommand(MediaCommand.TogglePlayPause);

    [RelayCommand]
    private void MediaRestart() => _projectionService.RequestMediaCommand(MediaCommand.Restart);

    [RelayCommand]
    private void MediaBack() => _projectionService.RequestMediaSeek(-SeekStep);

    [RelayCommand]
    private void MediaForward() => _projectionService.RequestMediaSeek(SeekStep);

    private void OnVideoSlideActiveChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            IsVideoSlideActive = _projectionService.IsVideoSlideActive);
    }

    private void OnMediaTransportChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var t = _projectionService.MediaTransport;
            IsMediaPlaying        = t.IsPlaying;
            MediaPlayPauseGlyph   = t.IsPlaying ? PauseGlyph : PlayGlyph;
            MediaDurationSeconds  = t.Duration.TotalSeconds;
            MediaPositionSeconds  = t.Position.TotalSeconds;
            MediaTimeText         = $"{Format(t.Position)} / {Format(t.Duration)}";
        });
    }

    private static string Format(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    // ── Live announcement ─────────────────────────────────────────────────────

    [RelayCommand]
    private void ShowAnnouncement()
    {
        if (string.IsNullOrWhiteSpace(AnnouncementText)) return;
        _projectionService.ShowAnnouncement(AnnouncementText);
        if (_projectionService.IsAnnouncementActive)
        {
            StartAnnouncementTimer();
            _logger.LogInformation("Announcement shown by operator");
        }
    }

    [RelayCommand]
    private void ClearAnnouncement()
    {
        StopAnnouncementTimer();
        _projectionService.ClearAnnouncement();
        AnnouncementText = string.Empty;
    }

    [RelayCommand]
    private void ShowLowerThird()
    {
        if (string.IsNullOrWhiteSpace(LowerThirdText)) return;
        _projectionService.ShowLowerThird(LowerThirdText);
    }

    [RelayCommand]
    private void ClearLowerThird()
    {
        _projectionService.ClearLowerThird();
        LowerThirdText = string.Empty;
    }

    private void StartAnnouncementTimer()
    {
        StopAnnouncementTimer();
        var seconds = Math.Max(1, _appSettings.Current.AnnouncementDurationSeconds);
        _announcementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _announcementTimer.Tick += OnAnnouncementTick;
        _announcementTimer.Start();
    }

    private void StopAnnouncementTimer()
    {
        if (_announcementTimer is null) return;
        _announcementTimer.Stop();
        _announcementTimer.Tick -= OnAnnouncementTick;
        _announcementTimer = null;
    }

    private void OnAnnouncementTick(object? sender, EventArgs e)
    {
        StopAnnouncementTimer();
        _projectionService.ClearAnnouncement();
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

    private void OnAnnouncementChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsAnnouncementActive = _projectionService.IsAnnouncementActive;
            // Cleared by any path (manual, auto-dismiss, projection stop) → ensure the timer is gone.
            if (!IsAnnouncementActive) StopAnnouncementTimer();
        });
    }

    private void OnLowerThirdChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            IsLowerThirdActive = _projectionService.IsLowerThirdActive);
    }

    public void Dispose()
    {
        StopAnnouncementTimer();
        _projectionService.ProjectionStateChanged -= OnProjectionStateChanged;
        _projectionService.SlideChanged           -= OnSlideChanged;
        _projectionService.AnnouncementChanged    -= OnAnnouncementChanged;
        _projectionService.LowerThirdChanged      -= OnLowerThirdChanged;
        _projectionService.VideoSlideActiveChanged -= OnVideoSlideActiveChanged;
        _projectionService.MediaTransportChanged   -= OnMediaTransportChanged;
        _currentScope?.Dispose();
        _liveServiceScope?.Dispose();
        _currentScope     = null;
        _liveServiceScope = null;
        _liveServiceVm    = null;
    }
}
