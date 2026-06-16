using System.IO.Compression;
using OpenAdoration.WPF.Helpers.VideoPsalmMigration;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.VideoPsalmMigration;

public sealed class VideoPsalmAgendaParserTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"vpagd_{Guid.NewGuid():N}.vpagd");

    // One song, one scripture group (5 entries), one image, one video — in agenda (zip) order,
    // plus skip-able style/metadata entries and a parallel AgendaItemProperties array.
    private static readonly (string Name, string Content)[] FullAgenda =
    [
        ("Version.json", "2"),
        ("RootStyle.json", "{Body:{FontName:\"Candara\",FontStyle:{Fill:{Color:\"FFFFFFFF\"}}},Background:{Image:\"bg-root.png\"}}"),
        ("SongBookStyle.json", "{Header:{Template:\"scratch text\"}}"),
        ("BibleStyle.json", "{Header:{Template:\"[BibleBookName] [BibleChapterID]:[BibleVerseID]\"},Footer:{Template:\"[BibleDescription]\"},Background:{Image:\"bg-bible.jpg\"}}"),
        ("Song_0.json", "{Guid:\"song-guid-1\",Style:{Background:{Video:\"song-bg.wmv\"}},Verses:[{\nText:\"Line one\"}],\nText:\"Mi Canción\"}"),
        ("SongBook_0.json", "{Text:\"SONG\",Guid:\"book\"}"),
        ("BibleVerses_0.json", "{Verses:[{\nText:\"v1\"},{ID:2,\nText:\"v2\"},{ID:3,\nText:\"v3\"}]}"),
        ("BibleChapter_0.json", "{ID:7,Verses:[]}"),
        ("BibleBook_0.json", "{ID:5,Text:\"Josué\",Abbreviation:\"Jos\",Chapters:[]}"),
        ("Testament_0.json", "{Text:\"Old Testament\",Books:[]}"),
        ("Bible_0.json", "{Abbreviation:\"NVI-S\",Language:\"es\",Text:\"Nueva Versión Internacional\",Testaments:[]}"),
        ("Image_0.json", "{FileName:\"C:\\\\Temp\\\\frb.jpeg\",\nText:\"My Image\"}"),
        ("Video_0.json", "{FileName:\"C:\\\\Temp\\\\vid.MOV\",\nText:\"My Video\"}"),
        ("Images/frb.jpeg", "binary-image-bytes"),
        ("Users/x/Temp/vid.MOV", "binary-video-bytes"),
        ("AgendaItemProperties.json",
            "{Items:[" +
            "{FlowType:0,AutoAdvance:0,Interval:5000,VerseOrderIndex:2,HiddenSlides:[]}," +
            "{FlowType:0,AutoAdvance:1,Interval:8000,VerseOrderIndex:-1}," +
            "{FlowType:2,AutoAdvance:0,Interval:5000,VerseOrderIndex:-1}," +
            "{FlowType:0,AutoAdvance:0,Interval:5000,VerseOrderIndex:-1}]}"),
    ];

    private void Build(params (string Name, string Content)[] entries)
    {
        using var stream = File.Create(_path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }
    }

    [Fact]
    public void Parse_OrdersItemsByZipAnchorOrder()
    {
        Build(FullAgenda);

        var agenda = VideoPsalmAgendaParser.Parse(_path);

        Assert.Equal(
            new[] { VpItemType.Song, VpItemType.Scripture, VpItemType.Image, VpItemType.Video },
            agenda.Items.Select(i => i.Type));
    }

    [Fact]
    public void Parse_Song_CapturesGuidAndTitle()
    {
        Build(FullAgenda);

        var song = VideoPsalmAgendaParser.Parse(_path).Items[0].Song;

        Assert.NotNull(song);
        Assert.Equal("Mi Canción", song!.Title);
        Assert.Equal("song-guid-1", song.SourceGuid);
    }

    [Fact]
    public void Parse_Scripture_IsReferenceOnly_BookIdZeroIndexed()
    {
        Build(FullAgenda);

        var s = VideoPsalmAgendaParser.Parse(_path).Items[1].Scripture;

        Assert.NotNull(s);
        Assert.Equal(6, s!.BookNumber);     // VideoPsalm ID 5 (0-indexed) → Joshua = 6
        Assert.Equal("Josué", s.BookName);
        Assert.Equal(7, s.Chapter);
        Assert.Equal(1, s.VerseStart);      // first verse omits ID (=1)
        Assert.Equal(3, s.VerseEnd);
        Assert.Equal("NVI-S", s.VersionAbbreviation);
    }

    [Fact]
    public void Parse_MapsParallelProperties_ByAnchorIndex()
    {
        Build(FullAgenda);

        var items = VideoPsalmAgendaParser.Parse(_path).Items;

        Assert.Equal(2, items[0].Properties.VerseOrderIndex);
        Assert.Null(items[0].Properties.AutoAdvanceSeconds);     // AutoAdvance:0 = manual
        Assert.Equal(8, items[1].Properties.AutoAdvanceSeconds); // AutoAdvance:1, Interval 8000ms
    }

    [Fact]
    public void Parse_Media_ResolvesBytesByBasename()
    {
        Build(FullAgenda);

        var items = VideoPsalmAgendaParser.Parse(_path).Items;

        Assert.Equal("Images/frb.jpeg", items[2].MediaEntryName);
        Assert.Equal("My Image", items[2].MediaCaption);
        Assert.Equal("Users/x/Temp/vid.MOV", items[3].MediaEntryName);
    }

    [Fact]
    public void Parse_Song_ResolvesStyle_RootFontWithItemVideoBackground()
    {
        Build(FullAgenda);

        var style = VideoPsalmAgendaParser.Parse(_path).Items[0].Style;

        Assert.NotNull(style);
        Assert.Equal("Candara", style!.FontFamily);   // inherited from RootStyle
        Assert.Equal("#FFFFFF", style.FontColor);      // ARGB FFFFFFFF, alpha dropped
        Assert.Equal("song-bg.wmv", style.BackgroundVideo); // item Style wins
        Assert.Null(style.BackgroundImage);            // video replaces the root image as a unit
    }

    [Fact]
    public void Parse_Scripture_ResolvesStyle_BibleTemplatesAndImageBackground()
    {
        Build(FullAgenda);

        var style = VideoPsalmAgendaParser.Parse(_path).Items[1].Style;

        Assert.NotNull(style);
        Assert.Equal("[BibleBookName] [BibleChapterID]:[BibleVerseID]", style!.HeaderTemplate);
        Assert.Equal("[BibleDescription]", style.FooterTemplate);
        Assert.Equal("bg-bible.jpg", style.BackgroundImage); // BibleStyle replaces root image
    }

    [Fact]
    public void Parse_NoRecognizableItems_Throws()
    {
        Build(("Version.json", "2"), ("RootStyle.json", "{}"), ("AgendaItemProperties.json", "{Items:[]}"));

        Assert.Throws<InvalidDataException>(() => VideoPsalmAgendaParser.Parse(_path));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
