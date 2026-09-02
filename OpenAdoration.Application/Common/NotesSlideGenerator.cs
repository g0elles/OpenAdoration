using System.Text.RegularExpressions;

namespace OpenAdoration.Application.Common;

/// <summary>
/// Splits plain-text Notes/Sermon content into slides, one per blank-line-separated paragraph —
/// same rule as <c>PlainTextParser.BlankLineRegex</c> uses for plain-text song import.
/// </summary>
public static partial class NotesSlideGenerator
{
    public static IReadOnlyList<Slide> GenerateSlides(string content, int? themeId = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];

        var paragraphs = BlankLineRegex().Split(content)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        return paragraphs
            .Select((p, i) => new Slide(p, SlideType.Notes, (i + 1).ToString(), themeId: themeId))
            .ToList();
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLineRegex();
}
