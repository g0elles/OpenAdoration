namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>Outcome of importing one VideoPsalm <c>.vpagd</c> agenda.</summary>
public sealed record VpImportSummary
{
    public required string ServiceName { get; init; }

    /// <summary>True when an agenda with this identity was already imported — nothing was added.</summary>
    public bool AlreadyImported { get; init; }

    public int SongsImported { get; init; }
    public int SongsReused { get; init; }
    public int ScriptureReferences { get; init; }
    public int MediaImported { get; init; }
    public int MediaReused { get; init; }
    public int MediaMissing { get; init; }
    public int ItemsSkipped { get; init; }
    public int ThemesCreated { get; init; }
    public int TotalItems { get; init; }
}
