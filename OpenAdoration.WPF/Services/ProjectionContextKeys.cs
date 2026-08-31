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
    private const string SongPrefix = "song:";
    private const string ServiceSongPrefix = "service-song:";

    /// <summary>Key for a song projected standalone from the Songs page.</summary>
    public static string Song(int songId) => $"{SongPrefix}{songId}";

    /// <summary>Key for a song projected as the current item of a live service schedule.</summary>
    public static string ServiceSong(int songId) => $"{ServiceSongPrefix}{songId}";

    /// <summary>
    /// Extracts the song id from a contextKey produced by <see cref="Song"/> or <see cref="ServiceSong"/>.
    /// Null for keys from other content types (Bible, media), or null/malformed input — lets callers
    /// gate song-only Stage View features (F7: quick style fix) off a single check.
    /// </summary>
    public static int? TryGetSongId(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey)) return null;

        var prefix = contextKey.StartsWith(ServiceSongPrefix, StringComparison.Ordinal) ? ServiceSongPrefix
            : contextKey.StartsWith(SongPrefix, StringComparison.Ordinal) ? SongPrefix
            : null;

        return prefix is not null && int.TryParse(contextKey.AsSpan(prefix.Length), out var id) ? id : null;
    }
}
