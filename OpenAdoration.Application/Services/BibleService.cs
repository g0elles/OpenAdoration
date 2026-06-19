using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using System.Text;

namespace OpenAdoration.Application.Services;

public sealed class BibleService : IBibleService
{
    private readonly IBibleRepository _repository;
    private readonly ILogger<BibleService> _logger;

    public BibleService(IBibleRepository repository, ILogger<BibleService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all Bible versions");

        var versions = await _repository.GetVersionsAsync(ct);

        _logger.LogDebug("Returned {Count} Bible version(s)", versions.Count);

        return versions;
    }

    public async Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching books for version {VersionId}", versionId);

        return await _repository.GetBooksAsync(versionId, ct);
    }

    public async Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        int versionId, string book, int chapter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(book);

        _logger.LogDebug("Fetching verses for {Book} {Chapter} (version {VersionId})", book, chapter, versionId);

        var verses = await _repository.GetVersesAsync(versionId, book, chapter, ct);

        if (verses.Count == 0)
            _logger.LogWarning("No verses found for {Book} {Chapter} in version {VersionId}", book, chapter, versionId);

        return verses;
    }

    public async Task<BibleVerse?> GetVerseAsync(
        int versionId, string book, int chapter, int verse, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(book);

        _logger.LogDebug("Fetching {Book} {Chapter}:{Verse} (version {VersionId})", book, chapter, verse, versionId);

        var result = await _repository.GetVerseAsync(versionId, book, chapter, verse, ct);

        if (result is null)
            _logger.LogWarning("Verse {Book} {Chapter}:{Verse} not found in version {VersionId}", book, chapter, verse, versionId);

        return result;
    }

    public async Task<IReadOnlyList<BibleVerse>> SearchAsync(
        int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        // Search term is intentionally not logged -- it may contain user-entered
        // scripture phrases that should not appear in support logs (L2).
        _logger.LogDebug("Searching Bible version {VersionId} in {Mode} mode (max {MaxResults})", versionId, mode, maxResults);

        var results = await _repository.SearchAsync(versionId, term, mode, maxResults, ct);

        _logger.LogDebug("Bible search returned {Count} result(s) for version {VersionId}", results.Count, versionId);

        return results;
    }

    /// <summary>
    /// Logs a warning for any book whose <see cref="BibleBook.Name"/> has no verse with a
    /// matching <see cref="BibleVerse.Book"/>. Such a book is listed in the browser but its
    /// verses can never be looked up (the query keys on the book name) — the symptom of a
    /// parser storing the book row and its verses under different names (G21).
    /// </summary>
    private void WarnOnBooksWithoutVerses(
        string abbreviation, IReadOnlyList<BibleBook> books, IReadOnlyList<BibleVerse> verses)
    {
        var booksWithVerses = verses.Select(v => v.Book).ToHashSet();
        var orphans = books
            .Where(b => !booksWithVerses.Contains(b.Name))
            .Select(b => b.Name)
            .ToList();

        if (orphans.Count > 0)
            _logger.LogWarning(
                "Bible import {Abbreviation}: {Count} book(s) have no matching verses and will be unreadable: {Books}",
                abbreviation, orphans.Count, string.Join(", ", orphans));
    }

    public async Task UpsertVersionVersesAsync(
        BibleVersion version,
        IReadOnlyList<BibleBook> books,
        IReadOnlyList<BibleVerse> verses,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        _logger.LogInformation(
            "Upserting Bible version {Abbreviation}: {Books} book(s), {Verses} verse(s) offered",
            version.Abbreviation, books.Count, verses.Count);

        WarnOnBooksWithoutVerses(version.Abbreviation, books, verses);

        try
        {
            await _repository.UpsertVersionVersesAsync(version, books, verses, progress, ct);
            _logger.LogInformation("Bible upsert completed: {Abbreviation}", version.Abbreviation);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Bible upsert cancelled: {Abbreviation}", version.Abbreviation);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible upsert failed: {Abbreviation}", version.Abbreviation);
            throw;
        }
    }

    public async Task DeleteVersionAsync(int versionId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting Bible version {VersionId}", versionId);

        try
        {
            await _repository.DeleteVersionAsync(versionId, ct);
            _logger.LogInformation("Bible version {VersionId} deleted", versionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Bible version {VersionId}", versionId);
            throw;
        }
    }

    public Slide GenerateSlide(IReadOnlyList<BibleVerse> verses, int? themeId = null, BibleVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(verses);

        if (verses.Count == 0)
            throw new ArgumentException("At least one verse is required to generate a slide.", nameof(verses));

        var content = new StringBuilder();
        foreach (var verse in verses)
            content.AppendLine($"{verse.Verse} {verse.Text}");

        string label = verses.Count == 1
            ? verses[0].Reference
            : $"{verses[0].Book} {verses[0].Chapter}:{verses[0].Verse}-{verses[^1].Verse}";

        var context = new SlideContext
        {
            BibleBookName    = verses[0].Book,
            BibleChapterId   = verses[0].Chapter.ToString(),
            BibleVerseId     = verses.Count == 1
                                   ? verses[0].Verse.ToString()
                                   : $"{verses[0].Verse}-{verses[^1].Verse}",
            BibleReference   = label,
            BibleDescription = version?.Name
        };

        return new Slide(content.ToString().TrimEnd(), SlideType.Bible, label, themeId: themeId, context: context);
    }

    public IReadOnlyList<Slide> GenerateSlides(IReadOnlyList<BibleVerse> verses, int versesPerSlide, int? themeId = null,
                               BibleVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(verses);

        if (verses.Count == 0)
            throw new ArgumentException("At least one verse is required to generate slides.", nameof(verses));

        var chunkSize = Math.Max(1, versesPerSlide);

        var slides = new List<Slide>();
        for (var i = 0; i < verses.Count; i += chunkSize)
        {
            var chunk = verses.Skip(i).Take(chunkSize).ToList();
            slides.Add(GenerateSlide(chunk, themeId, version));
        }

        return slides;
    }
}
