namespace OpenAdoration.Domain.Common;

/// <summary>
/// Visual style applied when the projected foreground changes. The duration is a
/// global setting (SlideTransitionMilliseconds); 0 = instant Cut, which ignores this.
/// </summary>
public enum SlideTransitionKind
{
    Fade = 0,
    Slide = 1,
    Zoom = 2
}
