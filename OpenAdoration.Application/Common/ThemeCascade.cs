namespace OpenAdoration.Application.Common;

/// <summary>
/// Resolves the effective theme id for a slide, in cascade order. A <c>null</c> result means
/// "no explicit theme chosen" — the projection layer then falls back to the app-wide default
/// (<c>Theme.IsDefault</c>), so that final rung is intentionally not encoded here.
/// </summary>
public static class ThemeCascade
{
    /// <summary>Song: schedule-item theme → song's own theme → song content-type default.</summary>
    public static int? ForSong(int? scheduleItemThemeId, int? songThemeId, AppSettings settings)
        => scheduleItemThemeId ?? songThemeId ?? settings.DefaultSongThemeId;

    /// <summary>Scripture: schedule-item theme → scripture content-type default.</summary>
    public static int? ForScripture(int? scheduleItemThemeId, AppSettings settings)
        => scheduleItemThemeId ?? settings.DefaultScriptureThemeId;

    /// <summary>Media: schedule-item theme → media content-type default.</summary>
    public static int? ForMedia(int? scheduleItemThemeId, AppSettings settings)
        => scheduleItemThemeId ?? settings.DefaultMediaThemeId;
}
