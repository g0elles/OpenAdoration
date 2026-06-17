using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IBibleService
{
    Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default);
    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int versionId, string book, int chapter, CancellationToken ct = default);
    Task<BibleVerse?> GetVerseAsync(int versionId, string book, int chapter, int verse, CancellationToken ct = default);
    Task<IReadOnlyList<BibleVerse>> SearchAsync(int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default);

    /// <summary>
    /// Idempotent enrichment sink: find-or-create the version by abbreviation, ensure its
    /// book rows, and insert only verses not already stored. Lets a centralized version
    /// (e.g. NVI-S) grow as fuller legal sources are imported, with no rework for content
    /// that already references it.
    /// </summary>
    Task UpsertVersionVersesAsync(BibleVersion version, IReadOnlyList<BibleBook> books, IReadOnlyList<BibleVerse> verses, IProgress<int>? progress = null, CancellationToken ct = default);

    Task DeleteVersionAsync(int versionId, CancellationToken ct = default);

    /// <summary>
    /// Generates a slide for one or more consecutive verses.
    /// Pass <paramref name="themeId"/> to override the default theme on the generated slide.
    /// When null, the default theme is used.
    /// </summary>
    Slide GenerateSlide(IReadOnlyList<BibleVerse> verses, int? themeId = null, BibleVersion? version = null);

    /// <summary>
    /// Generates one or more slides, chunking <paramref name="verses"/> into groups of
    /// <paramref name="versesPerSlide"/> consecutive verses (minimum 1). Each chunk becomes
    /// its own slide via <see cref="GenerateSlide"/> with its own reference label/context.
    /// </summary>
    IReadOnlyList<Slide> GenerateSlides(IReadOnlyList<BibleVerse> verses, int versesPerSlide, int? themeId = null, BibleVersion? version = null);
}
