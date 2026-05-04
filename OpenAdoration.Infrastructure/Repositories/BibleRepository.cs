using Microsoft.EntityFrameworkCore;
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
        int versionId, string term, int maxResults = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        if (maxResults < 1)
            throw new ArgumentOutOfRangeException(nameof(maxResults), "maxResults must be at least 1.");

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.BibleVerses
            .AsNoTracking()
            .Where(bv => bv.BibleVersionId == versionId && bv.Text.Contains(term))
            .OrderBy(bv => bv.Book)
            .ThenBy(bv => bv.Chapter)
            .ThenBy(bv => bv.Verse)
            .Take(maxResults)
            .ToListAsync(ct);
    }

    public async Task ImportVersionAsync(
        BibleVersion version,
        IReadOnlyList<BibleBook> books,
        IReadOnlyList<BibleVerse> verses,
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

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var alreadyExists = await context.BibleVersions
            .AnyAsync(bv => bv.Abbreviation == version.Abbreviation, ct);

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
            }

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

        context.BibleVersions.Remove(version);
        await context.SaveChangesAsync(ct);
    }
}
