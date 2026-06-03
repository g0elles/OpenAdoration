using OpenAdoration.WPF.Helpers.BibleImport;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.BibleImport;

public sealed class BibleParserTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "BibleImport", "Fixtures", fileName);

    [Fact]
    public void ZefaniaXml_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("zefania_minimal.xml"));

        Assert.NotNull(result.Version);
        Assert.Single(result.Books);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void ZefaniaXml_MergesRepeatedVerseNumbersIntoOneEntry()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("zefania_split_verses.xml"));

        // Two <VERS vnumber="1"> fragments must collapse to a single verse so the
        // unique (Book, Chapter, Verse) constraint is not violated on import.
        Assert.Equal(2, result.Verses.Count);
        var verse1 = result.Verses.Single(v => v.Chapter == 1 && v.Verse == 1);
        Assert.Equal("En el principio creó Dios los cielos y la tierra.", verse1.Text);
    }

    [Fact]
    public void OsisXml_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("osis_minimal.xml"));

        Assert.NotNull(result.Version);
        Assert.Single(result.Books);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void OsisXml_MergesRepeatedVerseNumbersIntoOneEntry()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("osis_split_verses.xml"));

        // Two <verse osisID="Gen.1.1"> fragments must collapse to a single verse.
        Assert.Equal(2, result.Verses.Count);
        var verse1 = result.Verses.Single(v => v.Chapter == 1 && v.Verse == 1);
        Assert.Equal("In the beginning God created the heavens and the earth.", verse1.Text);
    }

    [Fact]
    public void OsisXml_VerseBookNameMatchesBookRowWhenTitleMissing()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("osis_no_title.xml"));

        // With no <title>, the book row and its verses must both resolve to the
        // canonical catalog name ("Judges") — not the raw osisID ("Judg") — or the
        // browser lists "Judges" while verses are stored under "Judg" and never match.
        Assert.Single(result.Books);
        Assert.Equal("Judges", result.Books[0].Name);
        Assert.NotEmpty(result.Verses);
        Assert.All(result.Verses, v => Assert.Equal("Judges", v.Book));
    }

    [Fact]
    public void UsfxXml_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("usfx_minimal.xml"));

        Assert.NotNull(result.Version);
        Assert.Single(result.Books);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void ThiagobodrukJson_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("thiagobodruk_minimal.json"));

        Assert.NotNull(result.Version);
        Assert.Single(result.Books);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void OpenADorationJson_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("openadoration_minimal.json"));

        Assert.NotNull(result.Version);
        Assert.Single(result.Books);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void BibleSuperSearchJson_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("bss_minimal.json"));

        Assert.NotNull(result.Version);
        Assert.Equal("Test Bible BSS", result.Version.Name);
        Assert.Single(result.Books);
        Assert.Equal("Genesis", result.Books[0].Name);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void BibleSuperSearchZip_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("bss_minimal.zip"));

        Assert.NotNull(result.Version);
        Assert.Equal("Test Bible BSS", result.Version.Name);
        Assert.Single(result.Books);
        Assert.Equal("Genesis", result.Books[0].Name);
        Assert.Equal(3, result.Verses.Count);
    }

    [Fact]
    public void BibleSuperSearchSqlite_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("bss_minimal.sqlite"));

        Assert.NotNull(result.Version);
        Assert.Equal("Test Bible BSS", result.Version.Name);
        Assert.Single(result.Books);
        Assert.Equal("Genesis", result.Books[0].Name);
        Assert.Equal(3, result.Verses.Count);
    }
}
