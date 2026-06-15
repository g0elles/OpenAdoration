using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport.VideoPsalm;

/// <summary>
/// Extracts songs from a VideoPsalm agenda (<c>.vpagd</c>) — a ZIP archive whose
/// <c>Song_{n}.json</c> entries each describe one song in VideoPsalm's relaxed JSON
/// (see <see cref="VpJsonReader"/>). A song's title is its <c>Text</c> field and each
/// <c>Verses[].Text</c> block becomes a sequentially numbered <see cref="SectionType.Verse"/>;
/// VideoPsalm has no section typing, so chorus/bridge cannot be inferred.
/// </summary>
public static partial class VideoPsalmParser
{
    public static IReadOnlyList<Song> Parse(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);

        var songs = archive.Entries
            .Where(e => SongEntryRegex().IsMatch(e.Name))
            .OrderBy(EntryIndex)
            .Select(ReadSong)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        if (songs.Count == 0)
            throw new InvalidDataException("The VideoPsalm file contains no songs to import.");

        return songs;
    }

    private static Song? ReadSong(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var root = VpJsonReader.Parse(reader.ReadToEnd()) as Dictionary<string, object?>;
        if (root is null) return null;

        var sections = BuildSections(GetArray(root, "Verses"));
        if (sections.Count == 0) return null;

        return new Song
        {
            Title      = ResolveTitle(GetString(root, "Text")),
            Author     = NullIfBlank(GetString(root, "Author")),
            Copyright  = NullIfBlank(GetString(root, "Copyright")),
            CcliNumber = NullIfBlank(GetString(root, "CCLINo")),
            Sections   = sections
        };
    }

    private static List<SongSection> BuildSections(IEnumerable<object?> verses)
    {
        var sections = new List<SongSection>();
        foreach (var verse in verses.OfType<Dictionary<string, object?>>())
        {
            var lyrics = NormalizeLyrics(GetString(verse, "Text"));
            if (lyrics.Length == 0) continue;
            sections.Add(new SongSection
            {
                Type          = SectionType.Verse,
                SectionNumber = sections.Count + 1,
                Lyrics        = lyrics,
                Order         = sections.Count
            });
        }
        return sections;
    }

    private static string ResolveTitle(string? text)
    {
        var title = text?.Trim();
        return string.IsNullOrEmpty(title) ? "Untitled" : title;
    }

    private static string NormalizeLyrics(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Trim();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<object?> GetArray(IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) && value is List<object?> list ? list : Enumerable.Empty<object?>();

    private static string? GetString(IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) ? value as string : null;

    private static int EntryIndex(ZipArchiveEntry entry) =>
        int.TryParse(SongEntryRegex().Match(entry.Name).Groups[1].Value, out var index) ? index : 0;

    [GeneratedRegex(@"^Song_(\d+)\.json$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SongEntryRegex();
}
