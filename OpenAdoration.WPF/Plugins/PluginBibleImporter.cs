using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.Plugins.Abstractions;

namespace OpenAdoration.WPF.Plugins;

/// <summary>
/// Bridges a Bible-source plugin to the core: fetches via the plugin, maps the plugin DTOs
/// onto domain entities, and feeds the centralized enrichable Bible. This mapping is the
/// only place plugin DTOs meet the domain — the plugin contract itself stays domain-free.
/// </summary>
public sealed class PluginBibleImporter
{
    private readonly IBibleService _bible;

    public PluginBibleImporter(IBibleService bible) => _bible = bible;

    public async Task ImportAsync(
        IBibleSourcePlugin plugin, string versionId, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var data = await plugin.FetchAsync(versionId, progress, ct);
        await _bible.UpsertVersionVersesAsync(MapVersion(data.Version), MapBooks(data.Books), MapVerses(data.Verses), progress, ct);
    }

    private static BibleVersion MapVersion(PluginBibleVersionInfo v) =>
        new() { Name = v.Name, Abbreviation = v.Abbreviation, Language = v.Language };

    private static List<BibleBook> MapBooks(IReadOnlyList<PluginBibleBook> books) =>
        [.. books.Select(b => new BibleBook
        {
            Name = b.Name,
            Abbreviation = b.Abbreviation,
            BookNumber = b.Number,
            Testament = b.Testament == PluginTestament.Old ? Testament.Old : Testament.New,
            ChapterCount = b.ChapterCount
        })];

    private static List<BibleVerse> MapVerses(IReadOnlyList<PluginBibleVerse> verses) =>
        [.. verses.Select(v => new BibleVerse { Book = v.Book, Chapter = v.Chapter, Verse = v.Verse, Text = v.Text })];
}
