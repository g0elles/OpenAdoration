namespace OpenAdoration.Plugins.Abstractions;

/// <summary>
/// A plugin that fetches Bible versions from an external source under the church's own
/// licensed account. The host maps the returned DTOs onto its domain and feeds the
/// centralized enrichable Bible — no licensed text and no account keys live in the core app.
/// </summary>
public interface IBibleSourcePlugin : IPlugin
{
    /// <summary>Versions the configured account can provide, for the operator to pick from.</summary>
    Task<IReadOnlyList<PluginBibleVersionInfo>> GetAvailableVersionsAsync(CancellationToken ct = default);

    /// <summary>Fetches one version's books + verses; <paramref name="progress"/> reports verses fetched.</summary>
    Task<PluginBibleData> FetchAsync(string versionId, IProgress<int>? progress = null, CancellationToken ct = default);
}
