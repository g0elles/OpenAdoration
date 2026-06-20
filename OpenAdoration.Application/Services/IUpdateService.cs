namespace OpenAdoration.Application.Services;

/// <summary>A newer release available on GitHub.</summary>
public sealed record UpdateInfo(Version Version, string ReleaseNotesUrl, string MsiUrl, long MsiSizeBytes);

/// <summary>
/// Opt-in, update-only network feature: checks GitHub releases for a newer version and, on the
/// operator's confirmation, downloads the MSI and hands off to the Windows installer. Sends no data.
/// </summary>
public interface IUpdateService
{
    /// <summary>Newer release if one exists, else null. Returns null on any network/parse failure (offline-safe).</summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the MSI to a temp path and launches msiexec (which prompts for UAC elevation).
    /// Returns true if the installer was started — the caller should then exit the app. Returns
    /// false if the operator cancelled the elevation prompt (UAC) so the caller stays running.
    /// </summary>
    Task<bool> DownloadAndApplyAsync(UpdateInfo info, CancellationToken ct = default);
}
