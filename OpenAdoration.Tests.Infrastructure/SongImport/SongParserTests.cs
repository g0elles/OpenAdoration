using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Helpers.SongImport;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.SongImport;

public sealed class SongParserTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "SongImport", "Fixtures", fileName);

    [Fact]
    public void OpenSong_ParsesMetadataAndSections()
    {
        var song = SongFormatDispatcher.Import(FixturePath("opensong_minimal.xml"));

        Assert.Equal("Amazing Grace", song.Title);
        Assert.Equal("John Newton", song.Author);
        Assert.Equal("Public Domain", song.Copyright);
        Assert.Equal("22025", song.CcliNumber);
        Assert.Equal("V1 C V2", song.VerseOrder);
        Assert.Equal(3, song.Sections.Count);
    }

    [Fact]
    public void OpenSong_SkipsChordsAndComments_KeepsLeadingDigitLyric()
    {
        var song = SongFormatDispatcher.Import(FixturePath("opensong_minimal.xml"));

        var chorus = song.Sections.Single(s => s.Type == SectionType.Chorus);
        Assert.Contains("10,000 reasons line stays intact", chorus.Lyrics);
        Assert.DoesNotContain("A   D   A", chorus.Lyrics);
        Assert.DoesNotContain("comment", song.Sections.Last().Lyrics);
    }

    [Fact]
    public void OpenSong_NumberedLines_SplitIntoStackedVerses()
    {
        var song = SongFormatDispatcher.Import(FixturePath("opensong_numbered.xml"));

        Assert.Equal(2, song.Sections.Count);
        Assert.All(song.Sections, s => Assert.Equal(SectionType.Verse, s.Type));
        Assert.Equal(new[] { 1, 2 }, song.Sections.Select(s => s.SectionNumber));
        Assert.Equal("First verse line one\nFirst verse line two", song.Sections[0].Lyrics);
        Assert.Equal("Second verse line one\nSecond verse line two", song.Sections[1].Lyrics);
    }

    [Fact]
    public void PlainText_Labelled_ParsesSectionsByLabel()
    {
        var song = SongFormatDispatcher.Import(FixturePath("plaintext_labelled.txt"));

        Assert.Equal("plaintext_labelled", song.Title);
        Assert.Equal(3, song.Sections.Count);
        Assert.Equal(SectionType.Verse, song.Sections[0].Type);
        Assert.Equal(1, song.Sections[0].SectionNumber);
        Assert.Equal(SectionType.Chorus, song.Sections[1].Type);
        Assert.Equal(SectionType.Verse, song.Sections[2].Type);
        Assert.Equal(2, song.Sections[2].SectionNumber);
    }

    [Fact]
    public void PlainText_NoLabels_SplitsByBlankLines()
    {
        var song = SongFormatDispatcher.Import(FixturePath("plaintext_blocks.txt"));

        Assert.Equal(3, song.Sections.Count);
        Assert.All(song.Sections, s => Assert.Equal(SectionType.Verse, s.Type));
        Assert.Equal(new[] { 1, 2, 3 }, song.Sections.Select(s => s.SectionNumber));
        Assert.Equal("First block line one\nFirst block line two", song.Sections[0].Lyrics);
    }

    [Fact]
    public void Dispatcher_RoutesOpenLyricsByNamespace()
    {
        var song = SongFormatDispatcher.Import(FixturePath("openlyrics_minimal.xml"));

        Assert.Equal("OpenLyrics Sample", song.Title);
        Assert.Equal("Jane Doe", song.Author);
        Assert.Equal("V1 C V2", song.VerseOrder);
        Assert.Equal(3, song.Sections.Count);
    }
}
