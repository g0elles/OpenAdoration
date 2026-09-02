using OpenAdoration.Domain.Common;

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

    /// <summary>
    /// Visual style of the slide-change transition (Fade / Slide / Zoom).
    /// Ignored when <see cref="SlideTransitionMilliseconds"/> is 0 (instant Cut).
    /// </summary>
    public SlideTransitionKind SlideTransition { get; set; } = SlideTransitionKind.Fade;

    /// <summary>App-chrome appearance (Light/Dark). Default Dark — projection output is unaffected.</summary>
    public AppearanceMode Appearance { get; set; } = AppearanceMode.Dark;

    /// <summary>
    /// UI language as a two-letter ISO code (e.g. "en", "es"). Null/empty = follow the
    /// operating system language when supported, otherwise English.
    /// </summary>
    public string? UiCulture { get; set; }

    /// <summary>
    /// When true, check GitHub for a newer release at startup. Opt-in (default false);
    /// the only outbound network call the app makes.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; }

    // ── Lower-third band (M15.3) ─────────────────────────────────────────────────
    // App-level "set up once" styling for the persistent lower-third overlay.

    /// <summary>When true the lower-third text scrolls continuously right-to-left (ticker) until cleared.</summary>
    public bool LowerThirdScroll { get; set; }

    /// <summary>Ticker speed in device-independent pixels per second. Default 90.</summary>
    public int LowerThirdScrollSpeed { get; set; } = 90;

    /// <summary>Band background as #AARRGGBB/#RRGGBB hex. Default is the classic translucent dark band.</summary>
    public string LowerThirdBandColor { get; set; } = "#CC101018";

    /// <summary>Band text colour as hex. Default white.</summary>
    public string LowerThirdTextColor { get; set; } = "#FFFFFF";

    /// <summary>Band font size in WPF units. Default 40.</summary>
    public int LowerThirdFontSize { get; set; } = 40;

    // ── Per-content-type default themes (M14 cascade) ────────────────────────────
    // Null = fall back to the app-wide default theme (Theme.IsDefault). These sit between
    // the content's own theme and the app default in the resolution cascade.

    /// <summary>Default theme for songs that don't carry their own <c>Song.ThemeId</c>.</summary>
    public int? DefaultSongThemeId { get; set; }

    /// <summary>Default theme for scripture slides.</summary>
    public int? DefaultScriptureThemeId { get; set; }

    /// <summary>Default theme for media slides.</summary>
    public int? DefaultMediaThemeId { get; set; }

    /// <summary>Default theme for Notes/Sermon slides.</summary>
    public int? DefaultNotesThemeId { get; set; }
}
