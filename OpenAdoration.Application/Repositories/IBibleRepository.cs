using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface IBibleRepository
{
    Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default);
    Task<BibleVersion?> GetVersionByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default);
    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int versionId, string book, int chapter, CancellationToken ct = default);
    Task<BibleVerse?> GetVerseAsync(int versionId, string book, int chapter, int verse, CancellationToken ct = default);
    Task<IReadOnlyList<BibleVerse>> SearchAsync(int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default);

    /// <summary>
    /// Idempotent find-or-create version by abbreviation, ensure book rows, and insert only
    /// the verses not already present (by Book/Chapter/Verse). The single sink every legal
    /// scripture source feeds; re-running with a fuller Bible enriches the same version.
    /// </summary>
    Task UpsertVersionVersesAsync(BibleVersion version, IReadOnlyList<BibleBook> books, IReadOnlyList<BibleVerse> verses, IProgress<int>? progress = null, CancellationToken ct = default);

    Task DeleteVersionAsync(int versionId, CancellationToken ct = default);
}
