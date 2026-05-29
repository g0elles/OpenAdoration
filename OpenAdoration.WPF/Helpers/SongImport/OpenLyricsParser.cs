using System.IO;
using System.Xml.Linq;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Parses OpenLyrics XML format (openlyrics.info/namespace/2009/song).
/// Supported section names: v{n}, c{n}, p{n}, b{n}, i, e, t.
/// </summary>
public static class OpenLyricsParser
{
    private static readonly XNamespace Ns = "http://openlyrics.info/namespace/2009/song";

    public static Song Parse(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Root ?? throw new FormatException("Empty OpenLyrics document.");

        var props = root.Element(Ns + "properties")
            ?? throw new FormatException("Missing <properties> element.");

        var title = props.Descendants(Ns + "title").FirstOrDefault()?.Value.Trim()
            ?? Path.GetFileNameWithoutExtension(filePath);

        var author        = props.Descendants(Ns + "author").FirstOrDefault()?.Value.Trim();
        var copyright     = props.Element(Ns + "copyright")?.Value.Trim();
        var ccliNo        = props.Element(Ns + "ccliNo")?.Value.Trim();
        var verseOrderRaw = props.Element(Ns + "verseOrder")?.Value.Trim();

        var lyrics = root.Element(Ns + "lyrics")
            ?? throw new FormatException("Missing <lyrics> element.");

        var sections = lyrics
            .Elements(Ns + "verse")
            .Select((el, idx) => ParseVerse(el, idx))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        return new Song
        {
            Title      = title,
            Author     = string.IsNullOrWhiteSpace(author)    ? null : author,
            Copyright  = string.IsNullOrWhiteSpace(copyright) ? null : copyright,
            CcliNumber = string.IsNullOrWhiteSpace(ccliNo)    ? null : ccliNo,
            VerseOrder = NormalizeVerseOrder(verseOrderRaw),
            Sections   = sections
        };
    }

    private static SongSection? ParseVerse(XElement verse, int order)
    {
        var name = verse.Attribute("name")?.Value ?? string.Empty;
        if (!TryParseVerseName(name, out var type, out var number))
            return null;

        // Collect all line text, joining <br/> as newlines.
        var lines = verse
            .Descendants(Ns + "lines")
            .Select(l => string.Join("\n",
                l.Nodes().Select(n => n switch
                {
                    XText xt      => xt.Value,
                    XElement xe when xe.Name.LocalName == "br" => "\n",
                    _ => string.Empty
                })))
            .Where(t => !string.IsNullOrWhiteSpace(t));

        var lyrics = string.Join("\n", lines).Trim();
        if (string.IsNullOrWhiteSpace(lyrics)) return null;

        return new SongSection
        {
            Type          = type,
            SectionNumber = number,
            Lyrics        = lyrics,
            Order         = order
        };
    }

    private static bool TryParseVerseName(string name, out SectionType type, out int number)
    {
        type   = SectionType.Verse;
        number = 1;

        if (string.IsNullOrEmpty(name)) return false;

        var prefix = new string(name.TakeWhile(char.IsLetter).ToArray()).ToLowerInvariant();
        var numPart = new string(name.SkipWhile(char.IsLetter).ToArray());
        number = int.TryParse(numPart, out var n) ? n : 1;

        type = prefix switch
        {
            "v" => SectionType.Verse,
            "c" => SectionType.Chorus,
            "p" => SectionType.PreChorus,
            "b" => SectionType.Bridge,
            "i" => SectionType.Intro,
            "e" => SectionType.Outro,
            "t" => SectionType.Tag,
            _   => SectionType.Verse
        };

        return true;
    }

    // Converts OpenLyrics verseOrder ("v1 c v2 c") to OA token format ("V1 C V2 C").
    private static string? NormalizeVerseOrder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t =>
            {
                var prefix = new string(t.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
                var num    = new string(t.SkipWhile(char.IsLetter).ToArray());
                return prefix + num;
            });

        return string.Join(" ", tokens);
    }
}
