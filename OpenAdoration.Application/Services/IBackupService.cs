using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

/// <summary>
/// Exports the whole library (DB + media + settings) to one portable <c>.oabak</c> file
/// and restores it. Restore stages the database for swap-in on next startup (the live DB
/// can't be overwritten in place), so the caller must prompt an app restart on success.
/// </summary>
public interface IBackupService
{
    Task CreateAsync(string destinationPath, CancellationToken ct = default);
    Task<RestoreResult> RestoreAsync(string sourcePath, CancellationToken ct = default);
}
