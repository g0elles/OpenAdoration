using System.IO;
using System.Text.Json;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Parses a JSON file into BibleVersion / BibleBook / BibleVerse domain objects
/// ready to be handed to IBibleService.ImportVersionAsync.
///
/// Expected JSON shape (all optional fields have sensible defaults):
/// {
///   "name":         "King James Version",   // required
///   "abbreviation": "KJV",
///   "language":     "English",
///   "books": [
///     {
///       "name":         "Genesis",           // required
///       "abbreviation": "Gen",
///       "testament":    "OT",               // "OT" or "NT"; inferred from number if absent
///       "number":       1,                   // canonical 1-66; positional if absent
///       "chapters": [
///         {
///           "number": 1,                     // positional if absent
///           "verses": [
///             { "number": 1, "text": "In the beginning…" }
///           ]
///         }
///       ]
///     }
///   ]
/// }
/// </summary>
public static class BibleJsonImporter
{
    public sealed record ImportResult(
        BibleVersion          Version,
        List<BibleBook>       Books,
        List<BibleVerse>      Verses);

    // ── Public API ────────────────────────────────────────────────────────────

    public static ImportResult Import(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var doc    = JsonDocument.Parse(stream,
            new JsonDocumentOptions { AllowTrailingCommas = true });

        var root = doc.RootElement;

        var version = new BibleVersion
        {
            Name         = GetString(root, "name", "translation", "version_name", "title")
                           ?? throw new InvalidDataException("JSON must contain a 'name' field."),
            Abbreviation = GetString(root, "abbreviation", "version", "abbrev", "code", "short") ?? "—",
            Language     = GetString(root, "language", "lang") ?? "Unknown"
        };

        if (!root.TryGetProperty("books", out var booksEl))
            throw new InvalidDataException("JSON must contain a 'books' array.");

        var books  = new List<BibleBook>();
        var verses = new List<BibleVerse>();
        var pos    = 0;

        foreach (var bookEl in booksEl.EnumerateArray())
        {
            pos++;
            var bookName   = GetString(bookEl, "name") ?? $"Book {pos}";
            var bookAbbr   = GetString(bookEl, "abbreviation", "abbrev", "short_name", "short")
                             ?? bookName[..Math.Min(3, bookName.Length)];
            int bookNumber = TryGetInt(bookEl, "number") ?? pos;

            var testament = bookEl.TryGetProperty("testament", out var testEl)
                ? ParseTestament(testEl.GetString())
                : (bookNumber >= 40 ? Testament.New : Testament.Old);

            if (!bookEl.TryGetProperty("chapters", out var chaptersEl))
                throw new InvalidDataException($"Book '{bookName}' has no 'chapters' array.");

            int chapterCount = 0;
            int chapPos = 0;
            foreach (var chapEl in chaptersEl.EnumerateArray())
            {
                chapPos++;
                int chapterNum = TryGetInt(chapEl, "number") ?? chapPos;
                chapterCount   = Math.Max(chapterCount, chapterNum);

                if (!chapEl.TryGetProperty("verses", out var versesEl)) continue;

                int versePos = 0;
                foreach (var verseEl in versesEl.EnumerateArray())
                {
                    versePos++;
                    int verseNum = TryGetInt(verseEl, "number") ?? versePos;
                    var text     = GetString(verseEl, "text") ?? string.Empty;

                    verses.Add(new BibleVerse
                    {
                        Book    = bookName,
                        Chapter = chapterNum,
                        Verse   = verseNum,
                        Text    = text.Trim()
                    });
                }
            }

            books.Add(new BibleBook
            {
                Name         = bookName,
                Abbreviation = bookAbbr,
                Testament    = testament,
                BookNumber   = bookNumber,
                ChapterCount = chapterCount
            });
        }

        if (books.Count == 0)
            throw new InvalidDataException("The JSON file contains no books.");

        return new ImportResult(version, books, verses);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Testament ParseTestament(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Testament.Old;
        return s.StartsWith("N", StringComparison.OrdinalIgnoreCase)
            ? Testament.New
            : Testament.Old;
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
            if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }

    private static int? TryGetInt(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!el.TryGetProperty(key, out var p)) continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
        }
        return null;
    }
}
