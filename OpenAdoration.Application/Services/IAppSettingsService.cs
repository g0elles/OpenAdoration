using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

/// <summary>
/// Reads and persists application-wide settings. Singleton — <see cref="Current"/>
/// is loaded once at construction and replaced on each successful save.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>The current settings snapshot. Never null.</summary>
    AppSettings Current { get; }

    /// <summary>Persists the supplied settings and updates <see cref="Current"/>.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Awaits any save still in flight so an abandoned fire-and-forget write (e.g. Settings →
    /// navigate away → immediate exit) completes before shutdown. No-op when nothing is pending.
    /// </summary>
    Task FlushAsync();
}
