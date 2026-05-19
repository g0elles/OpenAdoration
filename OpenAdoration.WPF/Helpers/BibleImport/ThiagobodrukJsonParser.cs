using System.IO;
using System.Text.Json;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the array-indexed JSON format popularised by the
/// <c>thiagobodruk/bible</c> GitHub repository and many language-specific forks.
///
/// Root is a JSON array; chapters are arrays of verse strings (0-indexed):
/// <code>
/// [
///   { "abbrev": "gn", "book": "Genesis",
///     "chapters": [
///       ["In the beginning…", "And the earth was…"],
///       ["Now the serpent…"]
///     ]
///   }
/// ]
/// </code>
/// Both <c>"book"</c> and <c>"name"</c> are accepted as the book-name field.
/// Version metadata is absent in this format — the filename is used as the version name.
/// </summary>
internal static class ThiagobodrukJsonParser
{
    public static BibleImportResult Parse(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var doc    = JsonDocument.Parse(stream,
            new JsonDocumentOptions { AllowTrailingCommas = true });

        var root = doc.RootElement;

        // The root may be an array directly, or an object with a "books" wrapper
        // whose chapters contain arrays of strings (thiagobodruk variant).
        JsonElement booksArray = root.ValueKind == JsonValueKind.Array
            ? root
            : root.GetProperty("books"); // some wrappers use {"books":[...]}

        // Version: derive from filename; user can rename in the app later
        var rawName = Path.GetFileNameWithoutExtension(filePath);
        var version = new BibleVersion
        {
            Name         = rawName,
            Abbreviation = rawName.Length <= 10 ? rawName : rawName[..10],
            Language     = "Unknown"
        };

        var books  = new List<BibleBook>();
        var verses = new List<BibleVerse>();
        int bookPos = 0;

        foreach (var bookEl in booksArray.EnumerateArray())
        {
            bookPos++;

            // Accept "book", "name", or positional fallback
            var bookName = GetString(bookEl, "book", "name") ?? $"Book {bookPos}";
            var abbrev   = GetString(bookEl, "abbrev", "abbreviation") ?? bookName[..Math.Min(3, bookName.Length)];

            // Books appear in canonical order → position = canonical number
            int bookNumber = bookPos;
            var testament  = bookNumber >= 40 ? Testament.New : Testament.Old;

            if (!bookEl.TryGetProperty("chapters", out var chaptersEl))
                continue;

            int chapCount = 0, chapPos = 0;
            foreach (var chapEl in chaptersEl.EnumerateArray())
            {
                chapPos++;
                chapCount = chapPos; // chapters appear in order
                int versePos = 0;

                foreach (var verseEl in chapEl.EnumerateArray())
                {
                    versePos++;
                    var text = verseEl.ValueKind == JsonValueKind.String
                        ? verseEl.GetString() ?? string.Empty
                        : verseEl.ToString();

                    verses.Add(new BibleVerse
                    {
                        Book    = bookName,
                        Chapter = chapPos,
                        Verse   = versePos,
                        Text    = text.Trim()
                    });
                }
            }

            books.Add(new BibleBook
            {
                Name         = bookName,
                Abbreviation = abbrev,
                Testament    = testament,
                BookNumber   = bookNumber,
                ChapterCount = chapCount
            });
        }

        if (books.Count == 0)
            throw new InvalidDataException("No books found in the array-format JSON file.");

        return new BibleImportResult(version, books, verses);
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }
}
