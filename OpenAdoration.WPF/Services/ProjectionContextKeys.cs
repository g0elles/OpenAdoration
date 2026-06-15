namespace OpenAdoration.WPF.Services;

/// <summary>
/// Builds the opaque <c>contextKey</c> strings passed to
/// <see cref="OpenAdoration.Application.Services.IProjectionService.LoadSlides"/> so the
/// owner of a projection can later live-update it via <c>TryUpdateSlides</c>.
/// Standalone and service-driven song projection use <b>distinct</b> keys so a plain
/// standalone re-render never clobbers a service's themed/verse-ordered slides (and vice versa).
/// </summary>
internal static class ProjectionContextKeys
{
    /// <summary>Key for a song projected standalone from the Songs page.</summary>
    public static string Song(int songId) => $"song:{songId}";

    /// <summary>Key for a song projected as the current item of a live service schedule.</summary>
    public static string ServiceSong(int songId) => $"service-song:{songId}";
}
