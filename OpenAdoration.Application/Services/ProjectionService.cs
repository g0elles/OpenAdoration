using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

/// <summary>
/// Manages the projection state machine. Registered as singleton — one instance for the lifetime of the app.
/// All members are called from the UI thread; no locking is needed.
/// </summary>
public sealed class ProjectionService : IProjectionService
{
    private readonly ILogger<ProjectionService> _logger;

    private List<Slide> _slides = [];
    private int _currentIndex = -1;
    private bool _isProjecting;
    private string _contextLabel = string.Empty;

    public ProjectionService(ILogger<ProjectionService> logger)
    {
        _logger = logger;
    }

    public Slide? CurrentSlide => _currentIndex >= 0 && _currentIndex < _slides.Count
        ? _slides[_currentIndex]
        : null;

    public IReadOnlyList<Slide> CurrentSlides => _slides;
    public int CurrentSlideIndex => _currentIndex;
    public bool IsProjecting => _isProjecting;
    public string ContextLabel => _contextLabel;

    private bool  _isServiceScheduleActive;
    private Slide? _nextScheduleItemPreviewSlide;
    private string? _currentAnnouncement;

    public bool   IsServiceScheduleActive      => _isServiceScheduleActive;
    public Slide? NextScheduleItemPreviewSlide => _nextScheduleItemPreviewSlide;
    public string? CurrentAnnouncement         => _currentAnnouncement;
    public bool   IsAnnouncementActive         => _currentAnnouncement is not null;

    private bool                _isVideoSlideActive;
    private MediaTransportState _mediaTransport = MediaTransportState.Empty;

    public bool                IsVideoSlideActive => _isVideoSlideActive;
    public MediaTransportState MediaTransport     => _mediaTransport;

    public event EventHandler<Slide?>? SlideChanged;
    public event EventHandler<bool>?   ProjectionStateChanged;
    public event EventHandler?         ThemeChanged;
    public event EventHandler?         NextScheduleItemRequested;
    public event EventHandler?         PreviousScheduleItemRequested;
    public event EventHandler?         ServiceScheduleActiveChanged;
    public event EventHandler?         NextScheduleItemPreviewChanged;
    public event EventHandler?         AnnouncementChanged;
    public event EventHandler?              VideoSlideActiveChanged;
    public event EventHandler?              MediaTransportChanged;
    public event EventHandler<MediaCommand>? MediaCommandRequested;
    public event EventHandler<TimeSpan>?     MediaSeekRequested;

    public void LoadSlides(IReadOnlyList<Slide> slides, string contextLabel)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextLabel);

        if (slides.Count == 0)
        {
            _logger.LogWarning("LoadSlides called with an empty slide list for '{Context}' — nothing to project", contextLabel);
            return;
        }

        _logger.LogInformation("Loading {Count} slide(s) for '{Context}'", slides.Count, contextLabel);

        _slides = [.. slides];
        _currentIndex = 0;
        _contextLabel = contextLabel;

        if (!_isProjecting)
        {
            _isProjecting = true;
            _logger.LogInformation("Projection started");
            RaiseProjectionStateChanged(true);
        }

        RaiseSlideChanged(CurrentSlide);
    }

    public void Next()
    {
        if (!_isProjecting || _slides.Count == 0)
        {
            _logger.LogWarning("Next() called while not projecting — ignored");
            return;
        }

        if (_currentIndex >= _slides.Count - 1)
        {
            _logger.LogDebug("Already on the last slide ({Index}/{Total}) — Next() ignored", _currentIndex + 1, _slides.Count);
            return;
        }

        _currentIndex++;
        _logger.LogDebug("Slide advanced to {Index}/{Total}: {Label}", _currentIndex + 1, _slides.Count, CurrentSlide?.Label);
        RaiseSlideChanged(CurrentSlide);
    }

    public void Previous()
    {
        if (!_isProjecting || _slides.Count == 0)
        {
            _logger.LogWarning("Previous() called while not projecting — ignored");
            return;
        }

        if (_currentIndex <= 0)
        {
            _logger.LogDebug("Already on the first slide — Previous() ignored");
            return;
        }

        _currentIndex--;
        _logger.LogDebug("Slide moved back to {Index}/{Total}: {Label}", _currentIndex + 1, _slides.Count, CurrentSlide?.Label);
        RaiseSlideChanged(CurrentSlide);
    }

    public void GoTo(int index)
    {
        if (!_isProjecting || _slides.Count == 0)
        {
            _logger.LogWarning("GoTo({Index}) called while not projecting — ignored", index);
            return;
        }

        if (index < 0 || index >= _slides.Count)
        {
            _logger.LogError("GoTo({Index}) is out of range (0–{Max}) — ignored", index, _slides.Count - 1);
            return;
        }

        _currentIndex = index;
        _logger.LogDebug("Jumped to slide {Index}/{Total}: {Label}", _currentIndex + 1, _slides.Count, CurrentSlide?.Label);
        RaiseSlideChanged(CurrentSlide);
    }

    public void ShowBlank()
    {
        if (!_isProjecting)
        {
            _logger.LogWarning("ShowBlank() called while not projecting — ignored");
            return;
        }

        _logger.LogInformation("Showing blank slide");

        // Insert a blank at the current position without losing the surrounding slides
        var blank = Slide.Blank();
        RaiseSlideChanged(blank);
    }

    public void ShowAnnouncement(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("ShowAnnouncement called with empty text — ignored");
            return;
        }

        if (!_isProjecting)
        {
            _logger.LogWarning("ShowAnnouncement called while not projecting — ignored");
            return;
        }

        _logger.LogInformation("Showing announcement banner");
        _currentAnnouncement = text;
        RaiseSafe(AnnouncementChanged);
    }

    public void ClearAnnouncement()
    {
        if (_currentAnnouncement is null) return;

        _logger.LogInformation("Clearing announcement banner");
        _currentAnnouncement = null;
        RaiseSafe(AnnouncementChanged);
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping projection (was on slide {Index}/{Total})", _currentIndex + 1, _slides.Count);

        _slides = [];
        _currentIndex = -1;
        _isProjecting = false;
        _contextLabel = string.Empty;

        var wasScheduleActive           = _isServiceScheduleActive;
        var wasAnnouncementActive       = _currentAnnouncement is not null;
        _isServiceScheduleActive        = false;
        _nextScheduleItemPreviewSlide   = null;
        _currentAnnouncement            = null;

        RaiseSlideChanged(null);
        RaiseProjectionStateChanged(false);
        if (wasScheduleActive) RaiseSafe(ServiceScheduleActiveChanged);
        if (wasAnnouncementActive) RaiseSafe(AnnouncementChanged);
        RaiseSafe(NextScheduleItemPreviewChanged);

        _logger.LogInformation("Projection stopped");
    }

    public void NotifyThemeChanged()
    {
        var handlers = ThemeChanged?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in ThemeChanged subscriber — projection continues");
            }
        }
    }

    public void RefreshCurrentSlide()
    {
        if (!_isProjecting) return;
        RaiseSlideChanged(CurrentSlide);
    }

    public void RequestNextScheduleItem()
    {
        if (!_isProjecting) return;
        RaiseSafe(NextScheduleItemRequested);
    }

    public void RequestPreviousScheduleItem()
    {
        if (!_isProjecting) return;
        RaiseSafe(PreviousScheduleItemRequested);
    }

    public void SetServiceScheduleActive(bool active)
    {
        if (_isServiceScheduleActive == active) return;
        _isServiceScheduleActive = active;
        RaiseSafe(ServiceScheduleActiveChanged);
    }

    public void SetNextScheduleItemPreview(Slide? slide)
    {
        _nextScheduleItemPreviewSlide = slide;
        RaiseSafe(NextScheduleItemPreviewChanged);
    }

    public void RequestMediaCommand(MediaCommand command)
    {
        if (!_isVideoSlideActive) return;
        RaiseMediaCommand(command);
    }

    public void RequestMediaSeek(TimeSpan delta)
    {
        if (!_isVideoSlideActive) return;
        RaiseMediaSeek(delta);
    }

    public void ReportMediaTransport(MediaTransportState state)
    {
        if (_mediaTransport.Equals(state)) return;
        _mediaTransport = state;
        RaiseSafe(MediaTransportChanged);
    }

    /// <summary>
    /// Recomputes <see cref="IsVideoSlideActive"/> for the slide about to be shown and resets the
    /// transport snapshot. The projection window republishes live values once the video opens.
    /// </summary>
    private void UpdateMediaForSlide(Slide? slide)
    {
        var active = slide is { Type: SlideType.Media } && MediaFormats.IsVideo(slide.MediaPath);
        if (active != _isVideoSlideActive)
        {
            _isVideoSlideActive = active;
            RaiseSafe(VideoSlideActiveChanged);
        }

        if (!_mediaTransport.Equals(MediaTransportState.Empty))
        {
            _mediaTransport = MediaTransportState.Empty;
            RaiseSafe(MediaTransportChanged);
        }
    }

    private void RaiseMediaCommand(MediaCommand command)
    {
        var handlers = MediaCommandRequested?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try { ((EventHandler<MediaCommand>)handler)(this, command); }
            catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in MediaCommandRequested subscriber"); }
        }
    }

    private void RaiseMediaSeek(TimeSpan delta)
    {
        var handlers = MediaSeekRequested?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try { ((EventHandler<TimeSpan>)handler)(this, delta); }
            catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in MediaSeekRequested subscriber"); }
        }
    }

    private void RaiseSafe(EventHandler? @event)
    {
        var handlers = @event?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try { ((EventHandler)handler)(this, EventArgs.Empty); }
            catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in {Event} subscriber", @event); }
        }
    }

    private void RaiseSlideChanged(Slide? slide)
    {
        // Refresh media-transport state before notifying so subscribers see a consistent view.
        UpdateMediaForSlide(slide);

        // Iterate the invocation list individually so one throwing subscriber
        // cannot prevent later subscribers from receiving the event.
        var handlers = SlideChanged?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler<Slide?>)handler)(this, slide);
            }
            catch (Exception ex)
            {
                // A subscriber crash must never bring down the projection engine
                _logger.LogError(ex, "Unhandled exception in SlideChanged subscriber — projection continues");
            }
        }
    }

    private void RaiseProjectionStateChanged(bool isProjecting)
    {
        var handlers = ProjectionStateChanged?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler<bool>)handler)(this, isProjecting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in ProjectionStateChanged subscriber — projection continues");
            }
        }
    }
}
