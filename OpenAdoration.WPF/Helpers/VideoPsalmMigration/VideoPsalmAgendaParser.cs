using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using OpenAdoration.WPF.Helpers.SongImport.VideoPsalm;
using static OpenAdoration.WPF.Helpers.VideoPsalmMigration.VpRead;

namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>
/// Parses a VideoPsalm agenda (<c>.vpagd</c> ZIP) into an ordered <see cref="VpAgenda"/>.
/// Agenda order is the ZIP central-directory order of each item's <i>anchor</i> entry
/// (<c>Song_n</c>, <c>BibleVerses_n</c>, <c>Image_n</c>, <c>Video_n</c>); the i-th anchor
/// maps to <c>AgendaItemProperties.Items[i]</c>. Full format spec: VIDEOPSALM_REFERENCE.md §8b.
/// </summary>
public static partial class VideoPsalmAgendaParser
{
    public static VpAgenda Parse(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);

        var properties = ParseProperties(archive);
        var mediaByBasename = BuildMediaIndex(archive);
        var styles = ParseStyleBases(archive);

        var items = new List<VpAgendaItem>();
        foreach (var entry in archive.Entries)
        {
            var item = TryBuildItem(archive, entry, items.Count, properties, mediaByBasename, styles);
            if (item is not null) items.Add(item);
        }

        if (items.Count == 0)
            throw new InvalidDataException("The VideoPsalm agenda contains no recognizable items.");

        return new VpAgenda(items) { RootStyle = styles.Root };
    }

    private static VpAgendaItem? TryBuildItem(
        ZipArchive archive, ZipArchiveEntry entry, int index,
        IReadOnlyList<VpItemProperties> properties, IReadOnlyDictionary<string, string> media,
        VpStyleBases styles)
    {
        var match = AnchorRegex().Match(entry.Name);
        if (!match.Success) return null;

        var props = index < properties.Count ? properties[index] : VpItemProperties.None;
        var n = match.Groups["n"].Value;

        return match.Groups["kind"].Value switch
        {
            "Song"        => BuildSong(entry, index, props, styles.SongBase),
            "BibleVerses" => BuildScripture(archive, n, index, props, styles.ScriptureBase),
            "Image"       => BuildMedia(entry, VpItemType.Image, index, props, media),
            "Video"       => BuildMedia(entry, VpItemType.Video, index, props, media),
            _             => null
        };
    }

    private static VpAgendaItem BuildSong(ZipArchiveEntry entry, int index, VpItemProperties props, VpStyle songBase)
    {
        var root = ReadDict(entry);
        var song = root is null ? null : VideoPsalmParser.MapSong(root);
        var itemStyle = VpStyleReader.Read(root is null ? null : AsDict(root.GetValueOrDefault("Style")));
        return new VpAgendaItem
        {
            Index = index, Type = VpItemType.Song, Properties = props,
            Song = song, Style = songBase.Merge(itemStyle)
        };
    }

    private static VpAgendaItem BuildScripture(ZipArchive archive, string n, int index, VpItemProperties props, VpStyle scriptureBase)
    {
        var book = ReadDict(archive.GetEntry($"BibleBook_{n}.json"));
        var chapter = ReadDict(archive.GetEntry($"BibleChapter_{n}.json"));
        var bible = ReadDict(archive.GetEntry($"Bible_{n}.json"));
        var verses = ReadDict(archive.GetEntry($"BibleVerses_{n}.json"));

        var (start, end) = VerseRange(verses);
        var bookId = book is null ? 0 : GetInt(book, "ID") ?? 0;

        var scripture = new VpScriptureRef(
            VersionAbbreviation: (bible is null ? null : GetString(bible, "Abbreviation")) ?? string.Empty,
            VersionName:         (bible is null ? null : GetString(bible, "Text")) ?? string.Empty,
            Language:            (bible is null ? null : GetString(bible, "Language")) ?? string.Empty,
            BookNumber:          bookId + 1,
            BookName:            (book is null ? null : GetString(book, "Text")) ?? $"Book {bookId + 1}",
            Chapter:             chapter is null ? 0 : GetInt(chapter, "ID") ?? 0,
            VerseStart:          start,
            VerseEnd:            end);

        return new VpAgendaItem
        {
            Index = index, Type = VpItemType.Scripture, Properties = props,
            Scripture = scripture, Style = scriptureBase
        };
    }

    private static VpAgendaItem BuildMedia(
        ZipArchiveEntry entry, VpItemType type, int index, VpItemProperties props,
        IReadOnlyDictionary<string, string> media)
    {
        var root = ReadDict(entry);
        var fileName = root is null ? null : GetString(root, "FileName");
        var basename = string.IsNullOrEmpty(fileName) ? null : Path.GetFileName(fileName);
        var entryName = basename is not null && media.TryGetValue(basename, out var full) ? full : null;

        return new VpAgendaItem
        {
            Index = index,
            Type = type,
            Properties = props,
            MediaEntryName = entryName,
            MediaCaption = root is null ? null : NullIfBlank(GetString(root, "Text"))
        };
    }

    /// <summary>First/last verse numbers; the first verse omits its <c>ID</c> (=1), rest run on.</summary>
    private static (int Start, int End) VerseRange(IReadOnlyDictionary<string, object?>? verses)
    {
        if (verses is null) return (1, 1);

        int start = 0, end = 0, prev = 0;
        foreach (var v in GetArray(verses, "Verses").OfType<Dictionary<string, object?>>())
        {
            var num = GetInt(v, "ID") ?? prev + 1;
            if (start == 0) start = num;
            end = prev = num;
        }
        return start == 0 ? (1, 1) : (start, end);
    }

    /// <summary>
    /// Pre-merged style bases per content type: <c>RootStyle ← &lt;Type&gt;Style</c>. A per-item
    /// <c>Style</c> (songs only) merges on top later. See VIDEOPSALM_REFERENCE.md §8b.
    /// </summary>
    private sealed record VpStyleBases(VpStyle Root, VpStyle SongBase, VpStyle ScriptureBase);

    private static VpStyleBases ParseStyleBases(ZipArchive archive)
    {
        var root = VpStyleReader.Read(ReadDict(archive.GetEntry("RootStyle.json")));
        var songType = VpStyleReader.Read(ReadDict(archive.GetEntry("SongBookStyle.json")));
        var bibleType = VpStyleReader.Read(ReadDict(archive.GetEntry("BibleStyle.json")));
        return new VpStyleBases(root, root.Merge(songType), root.Merge(bibleType));
    }

    private static IReadOnlyList<VpItemProperties> ParseProperties(ZipArchive archive)
    {
        var root = ReadDict(archive.GetEntry("AgendaItemProperties.json"));
        if (root is null) return [];

        return GetArray(root, "Items")
            .OfType<Dictionary<string, object?>>()
            .Select(MapProperties)
            .ToList();
    }

    private static VpItemProperties MapProperties(IReadOnlyDictionary<string, object?> item)
    {
        var autoAdvance = (GetInt(item, "AutoAdvance") ?? 0) == 1;
        var intervalSeconds = (GetInt(item, "Interval") ?? 0) / 1000;
        var hidden = GetArray(item, "HiddenSlides")
            .OfType<double>().Select(d => (int)d).ToList();

        return new VpItemProperties(
            AutoAdvanceSeconds: autoAdvance && intervalSeconds > 0 ? intervalSeconds : null,
            VerseOrderIndex: GetInt(item, "VerseOrderIndex") ?? -1,
            HiddenSlides: hidden);
    }

    // basename -> full entry name, for resolving an item's foreign FileName path to its bytes.
    private static IReadOnlyDictionary<string, string> BuildMediaIndex(ZipArchive archive) =>
        archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().FullName, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, object?>? ReadDict(ZipArchiveEntry? entry)
    {
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open());
        return AsDict(VpJsonReader.Parse(reader.ReadToEnd()));
    }

    [GeneratedRegex(@"^(?<kind>Song|BibleVerses|Image|Video)_(?<n>\d+)\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorRegex();
}
