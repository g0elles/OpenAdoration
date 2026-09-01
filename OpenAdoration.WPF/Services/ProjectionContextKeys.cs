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

    /// <summary>
    /// Key for a song projected as the current item of a live service schedule. Carries the
    /// schedule item's own id (after the song id) so Stage View can offer a "this occurrence"
    /// style scope in addition to "this song" — see <see cref="TryGetServiceScheduleItemId"/>.
    /// </summary>
    public static string ServiceSong(int songId, int scheduleItemId) => $"{ServiceSongPrefix}{songId}:{scheduleItemId}";

    /// <summary>
    /// Extracts the song id from a contextKey produced by <see cref="Song"/> or <see cref="ServiceSong"/>.
    /// Null for keys from other content types (Bible, media), or null/malformed input — lets callers
    /// gate song-only Stage View features (F7: quick style fix) off a single check.
    /// </summary>
    public static int? TryGetSongId(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey)) return null;

        if (contextKey.StartsWith(ServiceSongPrefix, StringComparison.Ordinal))
        {
            var rest = contextKey.AsSpan(ServiceSongPrefix.Length);
            var sep = rest.IndexOf(':');
            var idSpan = sep >= 0 ? rest[..sep] : rest;
            return int.TryParse(idSpan, out var serviceSongId) ? serviceSongId : null;
        }

        if (contextKey.StartsWith(SongPrefix, StringComparison.Ordinal))
            return int.TryParse(contextKey.AsSpan(SongPrefix.Length), out var songId) ? songId : null;

        return null;
    }

    /// <summary>
    /// Extracts the schedule item id from a <see cref="ServiceSong"/> contextKey. Null for a
    /// standalone <see cref="Song"/> key (no schedule item exists) or any other content type —
    /// gates Stage View's "this occurrence" scope off a single check.
    /// </summary>
    public static int? TryGetServiceScheduleItemId(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey) || !contextKey.StartsWith(ServiceSongPrefix, StringComparison.Ordinal))
            return null;

        var rest = contextKey.AsSpan(ServiceSongPrefix.Length);
        var sep = rest.IndexOf(':');
        return sep >= 0 && int.TryParse(rest[(sep + 1)..], out var itemId) ? itemId : null;
    }
}
