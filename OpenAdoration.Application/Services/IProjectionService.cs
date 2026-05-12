using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

public interface IProjectionService
{
    /// <summary>The slide currently shown on the projector. Null when not projecting.</summary>
    Slide? CurrentSlide { get; }

    /// <summary>All slides loaded for the current schedule item.</summary>
    IReadOnlyList<Slide> CurrentSlides { get; }

    int CurrentSlideIndex { get; }
    bool IsProjecting { get; }

    /// <summary>Human-readable label for the item being projected (e.g. the song title). Empty when not projecting.</summary>
    string ContextLabel { get; }

    /// <summary>Fires whenever the displayed slide changes (including when projection stops).</summary>
    event EventHandler<Slide?> SlideChanged;

    /// <summary>Fires when projection starts or stops.</summary>
    event EventHandler<bool> ProjectionStateChanged;

    /// <summary>Loads a set of slides and immediately shows the first one.</summary>
    void LoadSlides(IReadOnlyList<Slide> slides, string contextLabel);

    void Next();
    void Previous();
    void GoTo(int index);

    /// <summary>Shows a blank (black) slide without stopping projection.</summary>
    void ShowBlank();

    /// <summary>Stops projection and clears state.</summary>
    void Stop();
}
