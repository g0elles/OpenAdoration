using System.IO;
using System.Text.Json;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the BibleSuperSearch flat-JSON export format:
/// <code>
/// {
///   "metadata": { "name": "...", "shortname": "...", "lang_short": "en", ... },
///   "verses": [
///     { "book_name": "Genesis", "book": 1, "chapter": 1, "verse": 1, "text": "..." },
///     ...
///   ]
/// }
/// </code>
/// Book names use the <c>book_name</c> field when present (localized); falls back to
/// <see cref="OsisBookCatalog.GetByNumber"/> for canonical English when the field is absent.
/// </summary>
internal static class BibleSuperSearchJsonParser
{
    /// <summary>
    /// Parses from an already-loaded <see cref="JsonElement"/> root.
    /// The caller must keep the owning <see cref="JsonDocument"/> alive until
    /// this method returns.
    /// </summary>
    internal static BibleImportResult ParseElement(JsonElement root)
    {
        var metaEl = root.GetProperty("metadata");

        var version = new BibleVersion
        {
            Name         = GetString(metaEl, "name")                    ?? "Unknown",
            Abbreviation = GetString(metaEl, "shortname", "module")     ?? "--",
            Language     = GetString(metaEl, "lang_short", "lang")      ?? "Unknown"
        };

        var versesEl = root.GetProperty("verses");

        var bookNames        = new Dictionary<int, string>(); // localized names from book_name field
        var chapterMaxByBook = new Dictionary<int, int>();
        var versesList       = new List<BibleVerse>();

        foreach (var v in versesEl.EnumerateArray())
        {
            if (!v.TryGetProperty("book",    out var bookProp)    || !bookProp.TryGetInt32(out int bookNum))   continue;
            if (!v.TryGetProperty("chapter", out var chapProp)    || !chapProp.TryGetInt32(out int chapter))   continue;
            if (!v.TryGetProperty("verse",   out var verseProp)   || !verseProp.TryGetInt32(out int verseNum)) continue;

            // Collect the localized book_name from the first verse that carries it.
            if (!bookNames.ContainsKey(bookNum) && v.TryGetProperty("book_name", out var bnProp))
            {
                var bn = bnProp.GetString();
                if (!string.IsNullOrWhiteSpace(bn))
                    bookNames[bookNum] = bn;
            }

            var text     = v.TryGetProperty("text", out var textProp) ? (textProp.GetString() ?? string.Empty) : string.Empty;
            var bookInfo = OsisBookCatalog.GetByNumber(bookNum);
            var bookName = bookNames.TryGetValue(bookNum, out var localName) ? localName : bookInfo.Name;

            versesList.Add(new BibleVerse
            {
                Book    = bookName,
                Chapter = chapter,
                Verse   = verseNum,
                Text    = text.Trim()
            });

            if (!chapterMaxByBook.TryGetValue(bookNum, out var maxChap) || chapter > maxChap)
                chapterMaxByBook[bookNum] = chapter;
        }

        var books = chapterMaxByBook.Keys
            .OrderBy(n => n)
            .Select(bookNum =>
            {
                var info = OsisBookCatalog.GetByNumber(bookNum);
                return new BibleBook
                {
                    Name         = bookNames.TryGetValue(bookNum, out var localName) ? localName : info.Name,
                    Abbreviation = info.Abbreviation,
                    Testament    = info.Testament,
                    BookNumber   = info.Number,
                    ChapterCount = chapterMaxByBook[bookNum]
                };
            })
            .ToList();

        if (books.Count == 0)
            throw new InvalidDataException("No verses found in BibleSuperSearch JSON file.");

        return new BibleImportResult(version, books, versesList);
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var v = p.GetString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        return null;
    }
}
