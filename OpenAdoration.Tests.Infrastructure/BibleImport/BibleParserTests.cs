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
    public void OsisXml_ParsesOneBookThreeVerses()
    {
        var result = BibleFormatDispatcher.Import(FixturePath("osis_minimal.xml"));

        Assert.NotNull(result.Version);
        Assert.Single(result.Books);
        Assert.Equal(3, result.Verses.Count);
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
