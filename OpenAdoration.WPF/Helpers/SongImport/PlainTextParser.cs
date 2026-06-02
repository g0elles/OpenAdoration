using System.IO;
using System.Text.RegularExpressions;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Parses a plain-text song. When the file contains section-label lines
/// (e.g. "Verse 1", "Chorus", "V1", "Bridge", "Pre-Chorus 2") each label
/// starts a new section; otherwise every blank-line-separated block becomes
/// a sequentially numbered verse. The title is taken from the file name.
/// </summary>
public static partial class PlainTextParser
{
    public static Song Parse(string filePath)
    {
        var text  = File.ReadAllText(filePath).Replace("\r\n", "\n");
        var lines = text.Split('\n');

        return new Song
        {
            Title    = Path.GetFileNameWithoutExtension(filePath),
            Sections = lines.Any(l => LabelRegex().IsMatch(l))
                ? ParseLabelled(lines)
                : ParseBlocks(text)
        };
    }

    private static List<SongSection> ParseLabelled(string[] lines)
    {
        var sections = new List<SongSection>();
        var acc = new Accumulator();
        foreach (var line in lines)
        {
            if (IsLabel(line, out var type, out var number))
            {
                acc.Flush(sections);
                acc.Type   = type;
                acc.Number = number;
            }
            else
            {
                acc.Add(line);
            }
        }
        acc.Flush(sections);
        return sections;
    }

    private static List<SongSection> ParseBlocks(string text)
    {
        var sections = new List<SongSection>();
        foreach (var block in BlankLineRegex().Split(text))
        {
            var lyrics = block.Trim();
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

    private static bool IsLabel(string line, out SectionType type, out int number)
    {
        type   = SectionType.Verse;
        number = 1;
        var match = LabelRegex().Match(line);
        if (!match.Success) return false;
        SongSectionTokens.TryParse(match.Groups[1].Value + match.Groups[2].Value, out type, out number);
        return true;
    }

    [GeneratedRegex(@"^\s*(verse|chorus|pre[-\s]?chorus|bridge|intro|outro|ending|tag|coda|[vcbptio])\s*#?\s*(\d*)\s*:?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLineRegex();

    private sealed class Accumulator
    {
        private readonly List<string> _buffer = new();

        public SectionType Type { get; set; } = SectionType.Verse;
        public int Number { get; set; } = 1;

        public void Add(string line) => _buffer.Add(line);

        public void Flush(List<SongSection> target)
        {
            var lyrics = string.Join("\n", _buffer).Trim();
            if (lyrics.Length > 0)
                target.Add(new SongSection
                {
                    Type          = Type,
                    SectionNumber = Number,
                    Lyrics        = lyrics,
                    Order         = target.Count
                });
            _buffer.Clear();
        }
    }
}
