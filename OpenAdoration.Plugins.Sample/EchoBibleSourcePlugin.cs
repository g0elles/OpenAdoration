using OpenAdoration.Plugins.Abstractions;

namespace OpenAdoration.Plugins.Sample;

/// <summary>
/// Minimal reference plugin. Returns one fake version with a single verse — enough to
/// exercise the loader, the DTO→domain mapping, and the enrichable-Bible sink end-to-end
/// (M13.2) without the real api.bible repo.
/// </summary>
public sealed class EchoBibleSourcePlugin : IBibleSourcePlugin
{
    private IPluginHost? _host;

    public string Id => "sample.echo";
    public string Name => "Sample Echo Bible Source";
    public Version Version => new(1, 0, 0);

    public void Initialize(IPluginHost host) => _host = host;

    public Task<IReadOnlyList<PluginBibleVersionInfo>> GetAvailableVersionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PluginBibleVersionInfo> versions = [new("echo", "Echo Version", "ECHO", "en")];
        return Task.FromResult(versions);
    }

    public Task<PluginBibleData> FetchAsync(string versionId, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var version = new PluginBibleVersionInfo(versionId, "Echo Version", "ECHO", "en");
        IReadOnlyList<PluginBibleBook> books = [new("Genesis", "Gen", 1, PluginTestament.Old, 50)];
        IReadOnlyList<PluginBibleVerse> verses = [new("Genesis", 1, 1, "In the beginning God created the heavens and the earth.")];
        progress?.Report(verses.Count);
        return Task.FromResult(new PluginBibleData(version, books, verses));
    }
}
