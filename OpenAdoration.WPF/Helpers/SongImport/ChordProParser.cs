using System.IO;
using System.Text.RegularExpressions;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Parses ChordPro / SongPro text (<c>.cho</c>, <c>.crd</c>, <c>.chopro</c>, <c>.chordpro</c>).
/// Metadata comes from <c>{title}</c>/<c>{artist}</c>/<c>{copyright}</c>/<c>{ccli}</c> directives;
/// <c>{start_of_chorus}</c>/<c>{soc}</c> &amp; friends delimit sections (blank-line blocks become
/// sequential verses otherwise); inline <c>[chord]</c> markers are stripped to leave plain lyrics.
/// </summary>
public static partial class ChordProParser
{
    public static Song Parse(string filePath)
    {
        var lines = File.ReadAllText(filePath).Replace("\r\n", "\n").Split('\n');
        var song  = new Song { Title = Path.GetFileNameWithoutExtension(filePath) };
        var acc   = new Accumulator();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (TryDirective(line, out var name, out var value))
            {
                ApplyDirective(name, value, song, acc);
                continue;
            }
            if (line.TrimStart().StartsWith('#')) continue; // ChordPro comment line

            var lyric = ChordRegex().Replace(line, string.Empty);
            if (lyric.Trim().Length == 0 && !acc.ExplicitSection)
                acc.Flush(song.Sections); // blank line = block boundary in free-form mode
            else
                acc.Add(lyric);
        }
        acc.Flush(song.Sections);
        return song;
    }

    private static void ApplyDirective(string name, string value, Song song, Accumulator acc)
    {
        switch (name)
        {
            case "title" or "t":                       song.Title = value; break;
            case "subtitle" or "st" or "artist"
                 or "a" or "composer":                 song.Author ??= NullIfBlank(value); break;
            case "copyright":                          song.Copyright = NullIfBlank(value); break;
            case "ccli" or "ccli_no" or "ccli_number": song.CcliNumber = NullIfBlank(value); break;

            case "start_of_chorus" or "soc":           acc.Begin(song.Sections, SectionType.Chorus); break;
            case "start_of_verse" or "sov":            acc.Begin(song.Sections, SectionType.Verse); break;
            case "start_of_bridge" or "sob":           acc.Begin(song.Sections, SectionType.Bridge); break;
            case "end_of_chorus" or "eoc"
                 or "end_of_verse" or "eov"
                 or "end_of_bridge" or "eob":           acc.End(song.Sections); break;

            // A comment is often a section label ("Verse 2", "Bridge").
            case "comment" or "c" or "comment_italic" or "ci"
                when SongSectionTokens.TryParse(value, out var type, out var number):
                acc.Label(song.Sections, type, number);
                break;
        }
    }

    private static bool TryDirective(string line, out string name, out string value)
    {
        name = value = string.Empty;
        var trimmed = line.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}') return false;

        var body = trimmed[1..^1];
        var colon = body.IndexOf(':');
        name  = (colon < 0 ? body : body[..colon]).Trim().ToLowerInvariant();
        value = colon < 0 ? string.Empty : body[(colon + 1)..].Trim();
        return name.Length > 0;
    }

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex ChordRegex();

    private sealed class Accumulator
    {
        private readonly List<string> _buffer = new();
        private readonly Dictionary<SectionType, int> _counts = new();
        private SectionType _type = SectionType.Verse;
        private int? _explicitNumber;

        /// <summary>True while a {start_of_*} block is open — blank lines stay inside it.</summary>
        public bool ExplicitSection { get; private set; }

        public void Add(string line) => _buffer.Add(line);

        public void Begin(List<SongSection> target, SectionType type)
        {
            Flush(target);
            _type = type;
            ExplicitSection = true;
        }

        public void End(List<SongSection> target)
        {
            Flush(target);
            ExplicitSection = false;
        }

        public void Label(List<SongSection> target, SectionType type, int number)
        {
            Flush(target);
            _type = type;
            _explicitNumber = number;
        }

        public void Flush(List<SongSection> target)
        {
            var lyrics = string.Join("\n", _buffer).Trim();
            _buffer.Clear();
            var explicitNumber = _explicitNumber;
            _explicitNumber = null;
            var type = _type;
            _type = SectionType.Verse; // free-form blocks that follow default to verses
            if (lyrics.Length == 0) return;

            target.Add(new SongSection
            {
                Type          = type,
                SectionNumber = explicitNumber ?? NextNumber(type),
                Lyrics        = lyrics,
                Order         = target.Count
            });
        }

        private int NextNumber(SectionType type)
        {
            _counts.TryGetValue(type, out var n);
            _counts[type] = n + 1;
            return n + 1;
        }
    }
}
