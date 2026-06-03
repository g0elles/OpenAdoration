using System.IO;
using Microsoft.Data.Sqlite;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the BibleSuperSearch SQLite export format.
/// Opens its own read-only <see cref="SqliteConnection"/> — does not use
/// <c>AppDbContext</c> or any DI service.
/// <para>Schema:</para>
/// <list type="bullet">
///   <item><c>meta (field TEXT, value TEXT)</c> — key/value version metadata</item>
///   <item><c>verses (id, book, chapter, verse, text)</c> — book is integer 1–66</item>
/// </list>
/// Book canonical names come from <see cref="OsisBookCatalog.GetByNumber"/>.
/// </summary>
internal static class BibleSuperSearchSqliteParser
{
    public static BibleImportResult Parse(string filePath)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode       = SqliteOpenMode.ReadOnly
        }.ToString();

        using var conn = new SqliteConnection(cs);
        conn.Open();

        var version          = ReadVersion(conn, filePath);
        var chapterMaxByBook = ReadChapterMaxByBook(conn);

        if (chapterMaxByBook.Count == 0)
            throw new InvalidDataException("No verses found in BibleSuperSearch SQLite file.");

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

        var verses = ReadVerses(conn);

        return new BibleImportResult(version, books, verses);
    }

    // ── Version ───────────────────────────────────────────────────────────────

    private static BibleVersion ReadVersion(SqliteConnection conn, string filePath)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var cmd    = conn.CreateCommand();
        cmd.CommandText  = "SELECT field, value FROM meta";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var field = reader.IsDBNull(0) ? null : reader.GetString(0);
            var value = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (field is not null && !string.IsNullOrWhiteSpace(value))
                meta[field] = value;
        }

        return new BibleVersion
        {
            Name         = meta.GetValueOrDefault("name")
                           ?? Path.GetFileNameWithoutExtension(filePath),
            Abbreviation = meta.GetValueOrDefault("shortname")
                           ?? meta.GetValueOrDefault("module")
                           ?? "--",
            Language     = meta.GetValueOrDefault("lang_short")
                           ?? meta.GetValueOrDefault("lang")
                           ?? "Unknown"
        };
    }

    // ── Chapter counts ────────────────────────────────────────────────────────

    private static Dictionary<int, int> ReadChapterMaxByBook(SqliteConnection conn)
    {
        var result = new Dictionary<int, int>();

        using var cmd    = conn.CreateCommand();
        cmd.CommandText  = "SELECT book, MAX(chapter) FROM verses GROUP BY book";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            int bookNum = reader.GetInt32(0);
            int maxChap = reader.GetInt32(1);
            result[bookNum] = maxChap;
        }

        return result;
    }

    // ── Verses ────────────────────────────────────────────────────────────────

    private static List<BibleVerse> ReadVerses(SqliteConnection conn)
    {
        var merger = new VerseMerger();

        using var cmd    = conn.CreateCommand();
        cmd.CommandText  = "SELECT book, chapter, verse, text FROM verses ORDER BY book, chapter, verse";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            int    bookNum = reader.GetInt32(0);
            int    chapter = reader.GetInt32(1);
            int    verse   = reader.GetInt32(2);
            string text    = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

            var bookInfo = OsisBookCatalog.GetByNumber(bookNum);

            merger.Add(bookInfo.Name, chapter, verse, text.Trim());
        }

        return merger.Verses;
    }
}
