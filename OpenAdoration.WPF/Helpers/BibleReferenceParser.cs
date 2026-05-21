using System.Text.RegularExpressions;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Parses human-typed Bible references into a structured form.
/// Supports: "John 3:16", "John 3:16-18", "1 Cor 2:12-15", "Romans 3", "Jn 3:16"
/// </summary>
public static class BibleReferenceParser
{
    public sealed record ParsedReference(string BookName, int Chapter, int VerseStart, int VerseEnd)
    {
        public bool IsFullChapter => VerseStart == 0;
    }

    // Accepts separators: colon, period, letter v/V, or space between chapter and verse
    private static readonly Regex ChapterVersePattern =
        new(@"^(\d+)(?:[:.vV\s](\d+)(?:-(\d+))?)?$", RegexOptions.Compiled);

    public static ParsedReference? TryParse(string input, IEnumerable<BibleBook> availableBooks)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var text = input.Trim();
        var books = availableBooks.OrderBy(b => b.BookNumber).ToList();
        if (books.Count == 0) return null;

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Try matching 1, 2, or 3 leading tokens as the book name (greedy: longest first)
        BibleBook? matched = null;
        int bookTokenCount = 0;

        for (int count = Math.Min(3, tokens.Length); count >= 1; count--)
        {
            var candidate = string.Join(" ", tokens.Take(count));
            var book = FindBook(candidate, books);
            if (book is not null)
            {
                matched = book;
                bookTokenCount = count;
                break;
            }
        }

        if (matched is null) return null;

        // Everything after the book tokens is the chapter:verse portion
        var rest = string.Join(" ", tokens.Skip(bookTokenCount)).Trim();
        if (string.IsNullOrWhiteSpace(rest)) return null; // need at least a chapter

        var m = ChapterVersePattern.Match(rest);
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out int chapter) || chapter <= 0)
            return null;

        int verseStart = 0, verseEnd = 0;

        if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out int vs) && vs > 0)
        {
            verseStart = vs;
            verseEnd = m.Groups[3].Success
                       && int.TryParse(m.Groups[3].Value, out int ve)
                       && ve >= vs
                ? ve
                : vs;
        }

        return new ParsedReference(matched.Name, chapter, verseStart, verseEnd);
    }

    /// <summary>Returns up to <paramref name="max"/> books whose name or abbreviation
    /// starts with <paramref name="partial"/> (case-insensitive), ordered by book number.</summary>
    public static IEnumerable<BibleBook> GetSuggestions(
        string partial, IEnumerable<BibleBook> books, int max = 6)
    {
        if (string.IsNullOrWhiteSpace(partial)) return [];

        partial = partial.Trim();
        return books
            .Where(b => b.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
                     || b.Abbreviation.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.BookNumber)
            .Take(max);
    }

    private static BibleBook? FindBook(string query, List<BibleBook> books)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        // 1. Exact name or abbreviation match
        var exact = books.FirstOrDefault(b =>
            string.Equals(b.Name,         query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(b.Abbreviation, query, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // 2. Prefix on abbreviation (more specific match first)
        var abbrev = books.FirstOrDefault(b =>
            b.Abbreviation.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        if (abbrev is not null) return abbrev;

        // 3. Prefix on full name
        return books.FirstOrDefault(b =>
            b.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase));
    }
}
