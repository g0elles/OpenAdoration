using System.IO;
using System.Xml.Linq;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Parses the OpenSong song format (no XML namespace; root &lt;song&gt;).
/// Lyrics use [tag] section headers; chord lines start with '.', comments with ';'.
/// A leading single digit on a lyric line distributes lines across stacked verses
/// sharing one [tag] block (e.g. all lines under a single [V] header).
/// </summary>
public static class OpenSongParser
{
    public static Song Parse(string filePath)
    {
        var doc  = XDocument.Load(filePath);
        var root = doc.Root ?? throw new FormatException("Empty OpenSong document.");

        string? Element(string name) => Blank(root.Element(name)?.Value);

        var title = Element("title") ?? Path.GetFileNameWithoutExtension(filePath);

        return new Song
        {
            Title      = title,
            Author     = Element("author"),
            Copyright  = Element("copyright"),
            CcliNumber = Element("ccli"),
            VerseOrder = SongSectionTokens.NormalizeOrder(root.Element("presentation")?.Value),
            Sections   = ParseLyrics(root.Element("lyrics")?.Value ?? string.Empty)
        };
    }

    private static List<SongSection> ParseLyrics(string body)
    {
        var sections = new List<SongSection>();
        if (string.IsNullOrWhiteSpace(body)) return sections;

        var acc = new Accumulator();
        foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
        {
            if (IsHeader(line, out var tag))
            {
                acc.Flush(sections);
                SongSectionTokens.TryParse(tag, out var type, out var number);
                acc.Type      = type;
                acc.TagNumber = number;
            }
            else if (!IsChordOrComment(line))
            {
                var (sub, lyric) = SplitNumberedLine(line, acc.TagNumber);
                acc.Add(sub, lyric);
            }
        }
        acc.Flush(sections);
        return sections;
    }

    private static bool IsHeader(string line, out string tag)
    {
        tag = string.Empty;
        var t = line.Trim();
        if (t.Length < 2 || t[0] != '[' || t[^1] != ']') return false;
        tag = t[1..^1].Trim();
        return tag.Length > 0;
    }

    private static bool IsChordOrComment(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith('.') || t.StartsWith(';');
    }

    // Leading digit 1-9 → that sub-verse; leading space → tag's verse number (space marker stripped).
    private static (int sub, string lyric) SplitNumberedLine(string line, int tagNumber)
    {
        if (line.Length > 0 && line[0] is >= '1' and <= '9')
        {
            var rest = line[1..];
            return (line[0] - '0', rest.StartsWith(' ') ? rest[1..] : rest);
        }
        return (tagNumber, line.StartsWith(' ') ? line[1..] : line);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Groups lyric lines by sub-verse number within the current [tag] block.</summary>
    private sealed class Accumulator
    {
        private readonly Dictionary<int, List<string>> _groups = new();
        private readonly List<int> _order = new();

        public SectionType Type { get; set; } = SectionType.Verse;
        public int TagNumber { get; set; } = 1;

        public void Add(int sub, string lyric)
        {
            if (!_groups.TryGetValue(sub, out var list))
            {
                list = new List<string>();
                _groups[sub] = list;
                _order.Add(sub);
            }
            list.Add(lyric);
        }

        public void Flush(List<SongSection> target)
        {
            foreach (var num in _order)
            {
                var text = string.Join("\n", _groups[num]).Trim();
                if (text.Length > 0)
                    target.Add(new SongSection
                    {
                        Type          = Type,
                        SectionNumber = num,
                        Lyrics        = text,
                        Order         = target.Count
                    });
            }
            _groups.Clear();
            _order.Clear();
        }
    }
}
