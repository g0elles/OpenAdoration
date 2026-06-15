using System.IO.Compression;
using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Helpers.SongImport;
using OpenAdoration.WPF.Helpers.SongImport.VideoPsalm;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.SongImport;

public sealed class VideoPsalmImportTests : IDisposable
{
    private readonly string _agendaPath = Path.Combine(Path.GetTempPath(), $"vp_{Guid.NewGuid():N}.vpagd");

    // VideoPsalm's real dialect: unquoted keys and literal newlines inside string values.
    private const string Song0 =
        "{Guid:\"abc123\",Verses:[{\nText:\"First line\nSecond line\"},{ID:2,\nText:\"Chorus text\"},{ID:9,\nText:\"   \"}],"
        + "Style:{Body:{FontSize:200},Background:{Video:\"HTB3.wmv\"}},\nText:\"Hermoso Momento\"}";

    private const string Song1 =
        "{Guid:\"def456\",Verses:[{\nText:\"Only verse\"}],\nText:\"\"}";

    // Must be ignored by the song importer.
    private const string SongBook0 = "{Description:\"unrelated\",Songs:[],Guid:\"x\",\nText:\"SONG\"}";
    private const string Image0 = "{FileName:\"C:\\\\pic.jpg\",\nText:\"pic\"}";

    private void BuildAgenda(params (string Name, string Content)[] entries)
    {
        using var stream = File.Create(_agendaPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }

    [Fact]
    public void Parse_ExtractsEachSong_TitleFromTextField()
    {
        BuildAgenda(("Song_0.json", Song0), ("SongBook_0.json", SongBook0),
                    ("Image_0.json", Image0), ("Song_1.json", Song1));

        var songs = VideoPsalmParser.Parse(_agendaPath);

        Assert.Equal(2, songs.Count);
        Assert.Equal("Hermoso Momento", songs[0].Title);
        Assert.Equal("Untitled", songs[1].Title); // blank Text falls back
    }

    [Fact]
    public void Parse_MapsVerses_Sequentially_PreservingNewlines_SkippingBlank()
    {
        BuildAgenda(("Song_0.json", Song0));

        var song = VideoPsalmParser.Parse(_agendaPath).Single();

        Assert.Equal(2, song.Sections.Count); // the whitespace-only verse is dropped
        Assert.All(song.Sections, s => Assert.Equal(SectionType.Verse, s.Type));
        Assert.Equal(new[] { 1, 2 }, song.Sections.Select(s => s.SectionNumber));
        Assert.Equal(new[] { 0, 1 }, song.Sections.Select(s => s.Order));
        Assert.Equal("First line\nSecond line", song.Sections[0].Lyrics);
        Assert.Equal("Chorus text", song.Sections[1].Lyrics);
    }

    [Fact]
    public void Parse_OrdersSongsByEntryIndex_NotZipOrder()
    {
        BuildAgenda(("Song_10.json", Song1), ("Song_2.json", Song0));

        var songs = VideoPsalmParser.Parse(_agendaPath);

        Assert.Equal("Hermoso Momento", songs[0].Title); // Song_2 before Song_10
    }

    [Fact]
    public void Parse_NoSongs_Throws()
    {
        BuildAgenda(("SongBook_0.json", SongBook0), ("Image_0.json", Image0));

        Assert.Throws<InvalidDataException>(() => VideoPsalmParser.Parse(_agendaPath));
    }

    [Fact]
    public void Dispatcher_ImportMany_RoutesVpagdToVideoPsalm()
    {
        BuildAgenda(("Song_0.json", Song0), ("Song_1.json", Song1));

        var songs = SongFormatDispatcher.ImportMany(_agendaPath);

        Assert.Equal(2, songs.Count);
    }

    public void Dispose()
    {
        if (File.Exists(_agendaPath)) File.Delete(_agendaPath);
    }
}
