namespace OpenAdoration.Application.Common;

/// <summary>
/// Metadata stored inside a <c>.oabak</c> backup. The migration id gates restore:
/// a backup whose schema this app doesn't know is from a newer version and is refused.
/// </summary>
public sealed record BackupManifest
{
    public required string AppVersion { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required string MigrationId { get; init; }
}

public enum RestoreOutcome
{
    /// <summary>Schema is known to this app; files were staged for restore.</summary>
    Compatible,

    /// <summary>Backup was made by a newer app version (unknown migration) — not restored.</summary>
    NeedsNewerApp,

    /// <summary>Archive or manifest was missing/unreadable — not restored.</summary>
    Corrupt
}

public sealed record RestoreResult(RestoreOutcome Outcome, string Message);
