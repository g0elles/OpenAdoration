namespace OpenAdoration.Application.Common;

/// <summary>A transport command the operator issues against the projected video.</summary>
public enum MediaCommand
{
    Play,
    Pause,
    TogglePlayPause,
    Restart
}

/// <summary>
/// Immutable snapshot of the projected video's transport state, published by the projection
/// window and consumed by the operator UI. Swapped wholesale so a single change notification
/// refreshes every bound value.
/// </summary>
public readonly record struct MediaTransportState(bool IsPlaying, TimeSpan Position, TimeSpan Duration)
{
    public static readonly MediaTransportState Empty = new(false, TimeSpan.Zero, TimeSpan.Zero);
}
