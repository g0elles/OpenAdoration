using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the BibleSuperSearch ZIP export format.
/// The archive contains two files:
/// <list type="bullet">
///   <item><c>info.json</c> — version metadata plus column order (<c>fields</c>) and delimiter</item>
///   <item><c>verses.txt</c> — pipe-delimited verse rows; lines starting with <c>#</c> are comments</item>
/// </list>
/// Book canonical names come from <see cref="OsisBookCatalog.GetByNumber"/> because the
/// format stores integer book numbers (1–66) with no book-name string in the verse rows.
/// </summary>
internal static class BibleSuperSearchZipParser
{
    public static BibleImportResult Parse(string filePath)
    {
        using var zip = ZipFile.OpenRead(filePath);

        var (version, delim, bookCol, chapCol, verseCol, textCol) = ReadInfoJson(zip, filePath);

        var chapterMaxByBook = new Dictionary<int, int>();
        var versesList       = new List<BibleVerse>();

        ReadVersesTxt(zip, delim, bookCol, chapCol, verseCol, textCol, chapterMaxByBook, versesList);

        var books = chapterMaxByBook.Keys
            .OrderBy(n => n)
            .Select(bookNum =>
            {
                var info = OsisBookCatalog.GetByNumber(bookNum);
                return new BibleBook
                {
                    Name         = info.Name,
                    Abbreviation = info.Abbreviation,
                    Testament    = info.Testament,
                    BookNumber   = info.Number,
                    ChapterCount = chapterMaxByBook[bookNum]
                };
            })
            .ToList();

        if (books.Count == 0)
            throw new InvalidDataException("No verses found in BibleSuperSearch ZIP file.");

        return new BibleImportResult(version, books, versesList);
    }

    // ── info.json ─────────────────────────────────────────────────────────────

    private static (BibleVersion Version, char Delim, int BookCol, int ChapCol, int VerseCol, int TextCol)
        ReadInfoJson(ZipArchive zip, string filePath)
    {
        var entry = zip.GetEntry("info.json")
            ?? throw new InvalidDataException("BibleSuperSearch ZIP is missing info.json.");

        using var stream = entry.Open();
        using var doc    = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var version = new BibleVersion
        {
            Name         = GetString(root, "name")                 ?? Path.GetFileNameWithoutExtension(filePath),
            Abbreviation = GetString(root, "shortname", "module")  ?? "--",
            Language     = GetString(root, "lang_short", "lang")   ?? "Unknown"
        };

        // Delimiter — default "|"
        var delimStr = GetString(root, "delimiter") ?? "|";
        char delim   = delimStr.Length > 0 ? delimStr[0] : '|';

        // Column order from "fields" array — default to canonical BSS order
        var fields = new List<string>();
        if (root.TryGetProperty("fields", out var fieldsEl))
            foreach (var f in fieldsEl.EnumerateArray())
                if (f.ValueKind == JsonValueKind.String && f.GetString() is { } s)
                    fields.Add(s);

        if (fields.Count == 0)
            fields.AddRange(["book", "chapter", "verse", "text", "italics", "strongs"]);

        int bookCol  = fields.IndexOf("book");
        int chapCol  = fields.IndexOf("chapter");
        int verseCol = fields.IndexOf("verse");
        int textCol  = fields.IndexOf("text");

        if (bookCol < 0 || chapCol < 0 || verseCol < 0 || textCol < 0)
            throw new InvalidDataException(
                "BibleSuperSearch ZIP info.json 'fields' is missing a required column " +
                "(book, chapter, verse, or text).");

        return (version, delim, bookCol, chapCol, verseCol, textCol);
    }

    // ── verses.txt ────────────────────────────────────────────────────────────

    private static void ReadVersesTxt(
        ZipArchive zip, char delim,
        int bookCol, int chapCol, int verseCol, int textCol,
        Dictionary<int, int> chapterMaxByBook, List<BibleVerse> versesList)
    {
        var entry = zip.GetEntry("verses.txt")
            ?? throw new InvalidDataException("BibleSuperSearch ZIP is missing verses.txt.");

        int minCols = Math.Max(Math.Max(bookCol, chapCol), Math.Max(verseCol, textCol)) + 1;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#') continue;

            var cols = line.Split(delim);
            if (cols.Length < minCols) continue;

            if (!int.TryParse(cols[bookCol],  out int bookNum)) continue;
            if (!int.TryParse(cols[chapCol],  out int chapter)) continue;
            if (!int.TryParse(cols[verseCol], out int verse))   continue;

            var text     = cols[textCol].Trim();
            var bookInfo = OsisBookCatalog.GetByNumber(bookNum);

            versesList.Add(new BibleVerse
            {
                Book    = bookInfo.Name,
                Chapter = chapter,
                Verse   = verse,
                Text    = text
            });

            if (!chapterMaxByBook.TryGetValue(bookNum, out var maxChap) || chapter > maxChap)
                chapterMaxByBook[bookNum] = chapter;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
