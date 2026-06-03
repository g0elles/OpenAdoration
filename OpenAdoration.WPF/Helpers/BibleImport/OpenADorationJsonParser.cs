using System.IO;
using System.Text.Json;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the OpenAdoration JSON Bible format:
/// <code>
/// {
///   "name": "King James Version",
///   "abbreviation": "KJV",
///   "language": "English",
///   "books": [
///     {
///       "name": "Genesis", "abbreviation": "Gen",
///       "testament": "OT", "number": 1,
///       "chapters": [
///         { "number": 1, "verses": [{ "number": 1, "text": "…" }] }
///       ]
///     }
///   ]
/// }
/// </code>
/// </summary>
internal static class OpenADorationJsonParser
{
    /// <summary>
    /// Parses from a file path.  Opens and parses the file once.
    /// Use <see cref="ParseElement"/> when the caller already holds an open
    /// <see cref="JsonDocument"/> to avoid a second full-file read (P2-2).
    /// </summary>
    public static BibleImportResult Parse(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var doc    = JsonDocument.Parse(stream,
            new JsonDocumentOptions { AllowTrailingCommas = true });
        return ParseElement(doc.RootElement);
    }

    /// <summary>
    /// Parses from an already-loaded <see cref="JsonElement"/> root.
    /// The caller must keep the owning <see cref="JsonDocument"/> alive until
    /// this method returns.
    /// </summary>
    internal static BibleImportResult ParseElement(JsonElement root)
    {
        var version = new BibleVersion
        {
            Name         = GetString(root, "name", "translation", "version_name", "title")
                           ?? throw new InvalidDataException("JSON must have a 'name' field."),
            Abbreviation = GetString(root, "abbreviation", "version", "abbrev", "code") ?? "--",
            Language     = GetString(root, "language", "lang") ?? "Unknown"
        };

        if (!root.TryGetProperty("books", out var booksEl))
            throw new InvalidDataException("JSON must have a 'books' array.");

        var books  = new List<BibleBook>();
        var merger = new VerseMerger();
        int pos    = 0;

        foreach (var bookEl in booksEl.EnumerateArray())
        {
            pos++;
            var bookName   = GetString(bookEl, "name") ?? $"Book {pos}";
            var bookAbbr   = GetString(bookEl, "abbreviation", "abbrev", "short") ?? bookName[..Math.Min(3, bookName.Length)];
            int bookNumber = TryGetInt(bookEl, "number") ?? pos;

            var testament = bookEl.TryGetProperty("testament", out var testEl)
                ? (testEl.GetString()?.StartsWith("N", StringComparison.OrdinalIgnoreCase) == true
                    ? Testament.New : Testament.Old)
                : (bookNumber >= 40 ? Testament.New : Testament.Old);

            if (!bookEl.TryGetProperty("chapters", out var chaptersEl))
                throw new InvalidDataException($"Book '{bookName}' has no 'chapters' array.");

            int chapCount = 0, chapPos = 0;
            foreach (var chapEl in chaptersEl.EnumerateArray())
            {
                chapPos++;
                int chapNum  = TryGetInt(chapEl, "number") ?? chapPos;
                chapCount    = Math.Max(chapCount, chapNum);

                if (!chapEl.TryGetProperty("verses", out var versesEl)) continue;

                int versePos = 0;
                foreach (var verseEl in versesEl.EnumerateArray())
                {
                    versePos++;
                    int verseNum = TryGetInt(verseEl, "number") ?? versePos;
                    var text     = GetString(verseEl, "text") ?? string.Empty;

                    merger.Add(bookName, chapNum, verseNum, text.Trim());
                }
            }

            books.Add(new BibleBook
            {
                Name         = bookName,
                Abbreviation = bookAbbr,
                Testament    = testament,
                BookNumber   = bookNumber,
                ChapterCount = chapCount
            });
        }

        if (books.Count == 0)
            throw new InvalidDataException("The JSON file contains no books.");

        return new BibleImportResult(version, books, merger.Verses);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }

    private static int? TryGetInt(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var p) && p.TryGetInt32(out var i))
                return i;
        return null;
    }
}
