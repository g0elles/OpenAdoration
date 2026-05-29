namespace OpenAdoration.Application.Common;

/// <summary>
/// Persistent application-wide settings, stored as JSON in
/// %LOCALAPPDATA%\OpenAdoration\settings.json (not the database).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Church/organisation name — resolves the [ChurchName] token.</summary>
    public string? ChurchName { get; set; }

    /// <summary>Church CCLI/site licence number — resolves the [SiteLicense] token.</summary>
    public string? ChurchCcliNumber { get; set; }

    /// <summary>
    /// Seconds applied as the auto-advance interval when a new schedule item is added.
    /// 0 = new items start in manual mode.
    /// </summary>
    public int DefaultAutoAdvanceSeconds { get; set; }

    /// <summary>
    /// How many Bible verses to place on a single projected slide. Minimum 1 (the default).
    /// </summary>
    public int DefaultBibleVersesPerSlide { get; set; } = 1;

    /// <summary>
    /// Seconds an announcement banner stays on screen before it auto-dismisses. Minimum 1; default 25.
    /// </summary>
    public int AnnouncementDurationSeconds { get; set; } = 25;

    /// <summary>
    /// Fade duration (milliseconds) when the projected slide changes. 0 disables the transition.
    /// Default 300.
    /// </summary>
    public int SlideTransitionMilliseconds { get; set; } = 300;
}
