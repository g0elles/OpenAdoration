using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class BibleRepository : IBibleRepository
{
    private const int VerseBatchSize = 1_000;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public BibleRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.BibleVersions
            .AsNoTracking()
            .OrderBy(bv => bv.Name)
            .ToListAsync(ct);
    }

    public async Task<BibleVersion?> GetVersionByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.BibleVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(bv => bv.Id == id, ct);
    }

    public async Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.BibleBooks
            .AsNoTracking()
            .Where(bb => bb.BibleVersionId == versionId)
            .OrderBy(bb => bb.BookNumber)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        int versionId, string book, int chapter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(book);

        if (chapter < 1)
            throw new ArgumentOutOfRangeException(nameof(chapter), "Chapter must be 1 or greater.");

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.BibleVerses
            .AsNoTracking()
            .Where(bv => bv.BibleVersionId == versionId
                      && bv.Book == book
                      && bv.Chapter == chapter)
            .OrderBy(bv => bv.Verse)
            .ToListAsync(ct);
    }

    public async Task<BibleVerse?> GetVerseAsync(
        int versionId, string book, int chapter, int verse, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(book);

        if (chapter < 1)
            throw new ArgumentOutOfRangeException(nameof(chapter), "Chapter must be 1 or greater.");

        if (verse < 1)
            throw new ArgumentOutOfRangeException(nameof(verse), "Verse must be 1 or greater.");

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.BibleVerses
            .AsNoTracking()
            .FirstOrDefaultAsync(bv => bv.BibleVersionId == versionId
                                    && bv.Book == book
                                    && bv.Chapter == chapter
                                    && bv.Verse == verse, ct);
    }

    public async Task<IReadOnlyList<BibleVerse>> SearchAsync(
        int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        if (maxResults < 1)
            throw new ArgumentOutOfRangeException(nameof(maxResults), "maxResults must be at least 1.");

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Phase 1 — FTS5 index lookup: returns matching verse IDs in O(log n + results).
        var matchedIds = await FtsSearchAsync(context, versionId, BuildFtsTerm(term, mode), maxResults, ct);

        if (matchedIds.Count == 0) return [];

        // Phase 2 — PK fetch: one indexed lookup per matched ID.
        return await context.BibleVerses
            .AsNoTracking()
            .Where(bv => matchedIds.Contains(bv.Id))
            .OrderBy(bv => bv.Book)
            .ThenBy(bv => bv.Chapter)
            .ThenBy(bv => bv.Verse)
            .ToListAsync(ct);
    }

    private static async Task<List<int>> FtsSearchAsync(
        AppDbContext context, int versionId, string term, int maxResults, CancellationToken ct)
    {
        var conn = (SqliteConnection)context.Database.GetDbConnection();

        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT rowid
            FROM   BibleVersesFts
            WHERE  BibleVersesFts MATCH @term
              AND  BibleVersionId = @versionId
            LIMIT  @maxResults
            """;

        cmd.Parameters.AddWithValue("@term",       term);
        cmd.Parameters.AddWithValue("@versionId",  versionId);
        cmd.Parameters.AddWithValue("@maxResults", maxResults);

        var ids = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetInt32(0));

        return ids;
    }

    /// <summary>
    /// Builds the FTS5 MATCH expression for the requested search mode.
    /// <list type="bullet">
    ///   <item><b>Phrase</b> — the whole term wrapped in FTS5 phrase quotes (literal sequence,
    ///   equivalent in intent to LIKE '%term%'); internal quotes doubled per the FTS5 spec.</item>
    ///   <item><b>Keyword</b> — each word quoted with a trailing <c>*</c> for prefix matching,
    ///   joined by space (implicit AND), so "love mercy" finds verses containing both words
    ///   in any order. Empty input falls back to a harmless phrase query.</item>
    /// </list>
    /// </summary>
    private static string BuildFtsTerm(string raw, BibleSearchMode mode)
    {
        if (mode == BibleSearchMode.Phrase)
            return "\"" + raw.Trim().Replace("\"", "\"\"") + "\"";

        var words = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return "\"" + raw.Trim().Replace("\"", "\"\"") + "\"";

        return string.Join(" ", words.Select(w =>
            "\"" + w.Replace("\"", "\"\"") + "\"*"));
    }

    public async Task ImportVersionAsync(
        BibleVersion version,
        IReadOnlyList<BibleBook> books,
        IReadOnlyList<BibleVerse> verses,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(verses);

        if (string.IsNullOrWhiteSpace(version.Abbreviation))
            throw new ArgumentException("Bible version abbreviation is required.", nameof(version));

        if (books.Count == 0)
            throw new ArgumentException("Bible import must include at least one book.", nameof(books));

        if (verses.Count == 0)
            throw new ArgumentException("Bible import must include at least one verse.", nameof(verses));

        // Normalise to uppercase so the unique index (case-sensitive by default in SQLite)
        // enforces case-insensitive uniqueness at the DB boundary for all new imports (R5).
        // EF.Functions.Like catches any pre-existing non-normalised rows via LIKE's
        // ASCII-case-insensitive comparison.
        version.Abbreviation = version.Abbreviation.Trim().ToUpperInvariant();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var alreadyExists = await context.BibleVersions
            .AnyAsync(bv => EF.Functions.Like(bv.Abbreviation, version.Abbreviation), ct);

        if (alreadyExists)
            throw new InvalidOperationException(
                $"A Bible version with abbreviation '{version.Abbreviation}' already exists. Delete it first before re-importing.");

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        try
        {
            context.BibleVersions.Add(version);
            await context.SaveChangesAsync(ct);

            foreach (var book in books)
            {
                book.Id = 0;
                book.BibleVersionId = version.Id;
            }

            context.BibleBooks.AddRange(books);
            await context.SaveChangesAsync(ct);

            // Insert verses in batches to avoid excessive memory pressure
            int savedVerseCount = 0;
            foreach (var batch in verses.Chunk(VerseBatchSize))
            {
                ct.ThrowIfCancellationRequested();

                foreach (var v in batch)
                {
                    v.Id = 0;
                    v.BibleVersionId = version.Id;
                }

                context.BibleVerses.AddRange(batch);
                await context.SaveChangesAsync(ct);
                context.ChangeTracker.Clear(); // Prevent unbounded memory growth

                savedVerseCount += batch.Length;
                progress?.Report(savedVerseCount);
            }

            // Populate FTS index in one shot from the now-committed verse rows.
            // Runs inside the same transaction — rolled back cleanly on cancellation.
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO BibleVersesFts(rowid, Text, BibleVersionId) SELECT Id, Text, BibleVersionId FROM BibleVerses WHERE BibleVersionId = {version.Id}",
                ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteVersionAsync(int versionId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var version = await context.BibleVersions.FindAsync([versionId], ct)
            ?? throw new InvalidOperationException($"BibleVersion with ID {versionId} was not found.");

        // Remove FTS rows before EF Core's cascade delete wipes BibleVerses,
        // because the subquery below relies on BibleVerses still being present.
        await context.Database.ExecuteSqlAsync(
            $"DELETE FROM BibleVersesFts WHERE rowid IN (SELECT Id FROM BibleVerses WHERE BibleVersionId = {versionId})",
            ct);

        context.BibleVersions.Remove(version);
        await context.SaveChangesAsync(ct);
    }
}
