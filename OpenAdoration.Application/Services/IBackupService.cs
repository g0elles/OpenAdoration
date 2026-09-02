using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

/// <summary>
/// Exports the whole library (DB + media + settings) to one portable <c>.oabak</c> file
/// and restores it. Restore stages the database for swap-in on next startup (the live DB
/// can't be overwritten in place), so the caller must prompt an app restart on success.
/// </summary>
public interface IBackupService
{
    /// <summary>Optionally encrypts the backup with <paramref name="password"/> (null/empty = plain zip, as before).</summary>
    Task CreateAsync(string destinationPath, string? password = null, CancellationToken ct = default);

    /// <summary>
    /// Restores from <paramref name="sourcePath"/>. If the backup is encrypted and
    /// <paramref name="password"/> is null/empty, returns <see cref="RestoreOutcome.PasswordRequired"/>
    /// without touching any files — call again with the password. A wrong password returns
    /// <see cref="RestoreOutcome.WrongPassword"/>.
    /// </summary>
    Task<RestoreResult> RestoreAsync(string sourcePath, string? password = null, CancellationToken ct = default);
}
