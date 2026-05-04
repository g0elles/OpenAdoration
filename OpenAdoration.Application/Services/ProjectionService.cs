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

    public event EventHandler<Slide?>? SlideChanged;
    public event EventHandler<bool>? ProjectionStateChanged;

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

    public void Stop()
    {
        _logger.LogInformation("Stopping projection (was on slide {Index}/{Total})", _currentIndex + 1, _slides.Count);

        _slides = [];
        _currentIndex = -1;
        _isProjecting = false;

        RaiseSlideChanged(null);
        RaiseProjectionStateChanged(false);

        _logger.LogInformation("Projection stopped");
    }

    private void RaiseSlideChanged(Slide? slide)
    {
        try
        {
            SlideChanged?.Invoke(this, slide);
        }
        catch (Exception ex)
        {
            // A subscriber crash must never bring down the projection engine
            _logger.LogError(ex, "Unhandled exception in SlideChanged subscriber — projection continues");
        }
    }

    private void RaiseProjectionStateChanged(bool isProjecting)
    {
        try
        {
            ProjectionStateChanged?.Invoke(this, isProjecting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ProjectionStateChanged subscriber — projection continues");
        }
    }
}
