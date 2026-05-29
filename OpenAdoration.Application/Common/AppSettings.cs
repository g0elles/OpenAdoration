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
}
