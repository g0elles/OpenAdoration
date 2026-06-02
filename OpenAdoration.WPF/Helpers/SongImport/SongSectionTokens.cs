using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Shared mapping between section labels/tags (e.g. "V1", "Verse 1", "Chorus", "c")
/// and the domain <see cref="SectionType"/> + repetition number, plus normalization
/// of a free-form order string to OpenAdoration VerseOrder tokens ("V1 C B").
/// </summary>
internal static class SongSectionTokens
{
    /// <summary>
    /// Resolves a label/tag to a section type and number. Always succeeds for
    /// non-blank input (unknown prefixes fall back to <see cref="SectionType.Verse"/>);
    /// returns false only for blank input.
    /// </summary>
    public static bool TryParse(string label, out SectionType type, out int number)
    {
        type   = SectionType.Verse;
        number = 1;
        if (string.IsNullOrWhiteSpace(label)) return false;

        var letters = new string(label.Trim().TakeWhile(char.IsLetter).ToArray()).ToLowerInvariant();
        var digits  = new string(label.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var n) && n > 0) number = n;

        type = letters switch
        {
            "v" or "verse"                    => SectionType.Verse,
            "c" or "chorus"                   => SectionType.Chorus,
            "p" or "pre"                      => SectionType.PreChorus,
            "b" or "bridge"                   => SectionType.Bridge,
            "i" or "intro"                    => SectionType.Intro,
            "o" or "e" or "outro" or "ending" => SectionType.Outro,
            "t" or "tag" or "coda"            => SectionType.Tag,
            _                                 => SectionType.Verse
        };
        return true;
    }

    /// <summary>
    /// Normalizes a free-form order string ("v1 c, B") to OA tokens ("V1 C B"), or null when empty.
    /// The source digit presence is preserved (so "c" → "C", "v1" → "V1"), matching OpenLyrics import.
    /// Intro/Outro/Tag are single-letter tokens that never carry a number.
    /// </summary>
    public static string? NormalizeOrder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var tokens = raw
            .Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken)
            .Where(t => t is not null);

        var joined = string.Join(" ", tokens);
        return joined.Length == 0 ? null : joined;
    }

    private static string? NormalizeToken(string token)
    {
        if (!TryParse(token, out var type, out _)) return null;

        var prefix = type switch
        {
            SectionType.Verse     => "V",
            SectionType.Chorus    => "C",
            SectionType.PreChorus => "P",
            SectionType.Bridge    => "B",
            SectionType.Intro     => "I",
            SectionType.Outro     => "O",
            _                     => "T"
        };

        if (type is SectionType.Intro or SectionType.Outro or SectionType.Tag)
            return prefix;

        return prefix + new string(token.Where(char.IsDigit).ToArray());
    }
}
