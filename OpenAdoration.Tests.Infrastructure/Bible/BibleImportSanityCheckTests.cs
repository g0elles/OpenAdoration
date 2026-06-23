using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Bible;

/// <summary>
/// Verifies the post-import sanity check (G21): a book whose name has no matching
/// verses must be flagged at import time so the mismatch surfaces loudly instead of
/// silently failing every lookup.
/// </summary>
public sealed class BibleImportSanityCheckTests
{
    [Fact]
    public async Task Import_WarnsWhenBookNameHasNoMatchingVerses()
    {
        var logger = new CapturingLogger<BibleService>();
        var service = new BibleService(new NoOpBibleRepository(), logger);

        var books  = new[] { Book("Judges") };                 // browser lists "Judges"
        var verses = new[] { Verse("Judg", 8, 1, "...") };      // verses stored under "Judg"

        await service.UpsertVersionVersesAsync(Version(), books, verses);

        var warning = Assert.Single(logger.Warnings, w => w.Contains("no matching verses"));
        Assert.Contains("Judges", warning);
    }

    [Fact]
    public async Task Import_DoesNotWarnWhenEveryBookHasVerses()
    {
        var logger = new CapturingLogger<BibleService>();
        var service = new BibleService(new NoOpBibleRepository(), logger);

        var books  = new[] { Book("Judges") };
        var verses = new[] { Verse("Judges", 8, 1, "...") };

        await service.UpsertVersionVersesAsync(Version(), books, verses);

        Assert.DoesNotContain(logger.Warnings, w => w.Contains("no matching verses"));
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────

    private static BibleVersion Version() =>
        new() { Name = "Test", Abbreviation = "TST", Language = "es" };

    private static BibleBook Book(string name) =>
        new() { Name = name, Abbreviation = name[..3], Testament = Testament.Old, BookNumber = 7, ChapterCount = 21 };

    private static BibleVerse Verse(string book, int chapter, int verse, string text) =>
        new() { Book = book, Chapter = chapter, Verse = verse, Text = text };

    // ── Test doubles ──────────────────────────────────────────────────────────────

    private sealed class NoOpBibleRepository : IBibleRepository
    {
        public Task UpsertVersionVersesAsync(BibleVersion version, IReadOnlyList<BibleBook> books,
            IReadOnlyList<BibleVerse> verses, IProgress<int>? progress = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BibleVersion>>([]);
        public Task<BibleVersion?> GetVersionByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult<BibleVersion?>(null);
        public Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BibleBook>>([]);
        public Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int versionId, string book, int chapter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BibleVerse>>([]);
        public Task<BibleVerse?> GetVerseAsync(int versionId, string book, int chapter, int verse, CancellationToken ct = default)
            => Task.FromResult<BibleVerse?>(null);
        public Task<IReadOnlyList<BibleVerse>> SearchAsync(int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BibleVerse>>([]);
        public Task DeleteVersionAsync(int versionId, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<int> DeleteVersionsBySourceAsync(string sourcePluginId, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
