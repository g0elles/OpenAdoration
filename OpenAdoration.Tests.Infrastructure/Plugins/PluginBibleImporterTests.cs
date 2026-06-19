using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.Plugins.Sample;
using OpenAdoration.WPF.Plugins;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Plugins;

/// <summary>
/// M13.2: the plugin → domain mapping feeds the enrichable Bible sink with correctly mapped
/// version/book/verse data (incl. the testament enum).
/// </summary>
public sealed class PluginBibleImporterTests
{
    [Fact]
    public async Task ImportAsync_MapsPluginDataOntoTheUpsertSink()
    {
        var bible = new CapturingBibleService();
        var importer = new PluginBibleImporter(bible);

        await importer.ImportAsync(new EchoBibleSourcePlugin(), "echo");

        Assert.Equal("ECHO", bible.Version!.Abbreviation);
        var book = Assert.Single(bible.Books!);
        Assert.Equal("Genesis", book.Name);
        Assert.Equal(Testament.Old, book.Testament);
        var verse = Assert.Single(bible.Verses!);
        Assert.Equal(("Genesis", 1, 1), (verse.Book, verse.Chapter, verse.Verse));
    }

    private sealed class CapturingBibleService : IBibleService
    {
        public BibleVersion? Version;
        public IReadOnlyList<BibleBook>? Books;
        public IReadOnlyList<BibleVerse>? Verses;

        public Task UpsertVersionVersesAsync(BibleVersion version, IReadOnlyList<BibleBook> books,
            IReadOnlyList<BibleVerse> verses, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            Version = version; Books = books; Verses = verses;
            return Task.CompletedTask;
        }

        // Unused by the importer.
        public Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int versionId, string book, int chapter, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BibleVerse?> GetVerseAsync(int versionId, string book, int chapter, int verse, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BibleVerse>> SearchAsync(int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteVersionAsync(int versionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Slide GenerateSlide(IReadOnlyList<BibleVerse> verses, int? themeId = null, BibleVersion? version = null) => throw new NotImplementedException();
        public IReadOnlyList<Slide> GenerateSlides(IReadOnlyList<BibleVerse> verses, int versesPerSlide, int? themeId = null, BibleVersion? version = null) => throw new NotImplementedException();
    }
}
