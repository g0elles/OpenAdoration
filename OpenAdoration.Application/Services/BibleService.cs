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
        int versionId, string term, int maxResults = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        _logger.LogDebug("Searching Bible version {VersionId} for: {Term}", versionId, term);

        var results = await _repository.SearchAsync(versionId, term, maxResults, ct);

        _logger.LogDebug("Bible search '{Term}' returned {Count} result(s)", term, results.Count);

        return results;
    }

    public async Task ImportVersionAsync(
        BibleVersion version,
        IReadOnlyList<BibleBook> books,
        IReadOnlyList<BibleVerse> verses,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        _logger.LogInformation(
            "Starting Bible import: {Name} ({Abbreviation}) — {Books} book(s), {Verses} verse(s)",
            version.Name, version.Abbreviation, books.Count, verses.Count);

        try
        {
            await _repository.ImportVersionAsync(version, books, verses, ct);

            _logger.LogInformation(
                "Bible import completed: {Name} ({Abbreviation})",
                version.Name, version.Abbreviation);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Bible import cancelled by user: {Abbreviation}", version.Abbreviation);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible import failed: {Abbreviation}", version.Abbreviation);
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

    public Slide GenerateSlide(IReadOnlyList<BibleVerse> verses)
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

        return new Slide(content.ToString().TrimEnd(), SlideType.Bible, label);
    }
}
