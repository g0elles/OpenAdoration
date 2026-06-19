namespace OpenAdoration.Application.Common;

/// <summary>
/// Visual style applied when the projected foreground changes. The duration is
/// <see cref="AppSettings.SlideTransitionMilliseconds"/> (0 = instant Cut, ignores this).
/// </summary>
public enum SlideTransitionKind
{
    Fade = 0,
    Slide = 1,
    Zoom = 2
}
