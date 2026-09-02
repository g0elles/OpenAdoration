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
    private const string ServiceBiblePrefix = "service-bible:";
    private const string NotesPrefix = "notes:";
    private const string ServiceNotesPrefix = "service-notes:";

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

    /// <summary>
    /// Key for a Bible passage projected as the current item of a live service schedule. Unlike
    /// songs, a scripture reading has no reusable library entity of its own to scope a "this
    /// content" style edit to (only the schedule item itself carries a ThemeId,
    /// see <c>ThemeCascade.ForScripture</c>) -- the schedule item's id is the whole key.
    /// </summary>
    public static string ServiceBible(int scheduleItemId) => $"{ServiceBiblePrefix}{scheduleItemId}";

    /// <summary>Extracts the schedule item id from a <see cref="ServiceBible"/> contextKey. Null for
    /// any other content type -- gates Stage View's Bible live-style-editor support off a single check.</summary>
    public static int? TryGetServiceBibleScheduleItemId(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey) || !contextKey.StartsWith(ServiceBiblePrefix, StringComparison.Ordinal))
            return null;

        return int.TryParse(contextKey.AsSpan(ServiceBiblePrefix.Length), out var itemId) ? itemId : null;
    }

    /// <summary>
    /// Fixed key for a Bible passage projected standalone from the Biblia page (browse-and-project,
    /// not part of a service). There is exactly one standalone-Bible slot at a time and no
    /// per-passage identity to key on, unlike <see cref="Song"/> -- picking a different passage
    /// simply reloads under this same key. The only persistent target for a style edit here is the
    /// app-wide <c>AppSettings.DefaultScriptureThemeId</c> (no schedule item, no reusable "reading"
    /// entity), so Stage View re-themes it by patching that single setting.
    /// </summary>
    public const string StandaloneBible = "bible:standalone";

    public static bool IsStandaloneBible(string? contextKey) => contextKey == StandaloneBible;

    /// <summary>Key for a note projected standalone from the Notes library page. Notes is a real
    /// library entity (like Song, unlike Bible/scripture) -- shares the exact key shape as
    /// <see cref="Song"/>/<see cref="ServiceSong"/>.</summary>
    public static string Notes(int noteId) => $"{NotesPrefix}{noteId}";

    /// <summary>
    /// Key for a note projected as the current item of a live service schedule. Carries the
    /// schedule item's own id (after the note id) so Stage View can offer a "this occurrence"
    /// style scope in addition to "this note" — see <see cref="TryGetServiceNotesScheduleItemId"/>.
    /// </summary>
    public static string ServiceNotes(int noteId, int scheduleItemId) => $"{ServiceNotesPrefix}{noteId}:{scheduleItemId}";

    /// <summary>
    /// Extracts the note id from a contextKey produced by <see cref="Notes"/> or <see cref="ServiceNotes"/>.
    /// Null for keys from other content types, or null/malformed input — mirrors <see cref="TryGetSongId"/>.
    /// </summary>
    public static int? TryGetNoteId(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey)) return null;

        if (contextKey.StartsWith(ServiceNotesPrefix, StringComparison.Ordinal))
        {
            var rest = contextKey.AsSpan(ServiceNotesPrefix.Length);
            var sep = rest.IndexOf(':');
            var idSpan = sep >= 0 ? rest[..sep] : rest;
            return int.TryParse(idSpan, out var serviceNoteId) ? serviceNoteId : null;
        }

        if (contextKey.StartsWith(NotesPrefix, StringComparison.Ordinal))
            return int.TryParse(contextKey.AsSpan(NotesPrefix.Length), out var noteId) ? noteId : null;

        return null;
    }

    /// <summary>
    /// Extracts the schedule item id from a <see cref="ServiceNotes"/> contextKey. Null for a
    /// standalone <see cref="Notes"/> key (no schedule item exists) or any other content type —
    /// gates Stage View's "this occurrence" scope off a single check. Mirrors
    /// <see cref="TryGetServiceScheduleItemId"/>.
    /// </summary>
    public static int? TryGetServiceNotesScheduleItemId(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey) || !contextKey.StartsWith(ServiceNotesPrefix, StringComparison.Ordinal))
            return null;

        var rest = contextKey.AsSpan(ServiceNotesPrefix.Length);
        var sep = rest.IndexOf(':');
        return sep >= 0 && int.TryParse(rest[(sep + 1)..], out var itemId) ? itemId : null;
    }
}
