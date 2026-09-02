using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers.SongImport;
using Xunit;
using SectionType = OpenAdoration.Domain.Enums.SectionType;

namespace OpenAdoration.Tests.Infrastructure.SongImport;

/// <summary>
/// Validated against real church song files with two different authoring styles: lyric lines
/// joined by manual &lt;w:br/&gt; breaks inside one paragraph, and one paragraph per lyric line.
/// Both use a blank paragraph as the verse separator, which is the heuristic under test.
/// </summary>
public sealed class DocxParserTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private Song ParseTemp(string titleNoExt, params Paragraph[] paragraphs)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{titleNoExt}_{Guid.NewGuid():N}.docx");
        _tempFiles.Add(path);

        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(paragraphs));
            mainPart.Document.Save();
        }

        // Parse under the intended file name (temp path has a GUID suffix for uniqueness).
        var finalPath = Path.Combine(Path.GetTempPath(), $"{titleNoExt}.docx");
        File.Copy(path, finalPath, overwrite: true);
        _tempFiles.Add(finalPath);
        return DocxParser.Parse(finalPath);
    }

    private static Paragraph Para(params string[] lines)
    {
        var p = new Paragraph();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) p.Append(new Run(new Break()));
            p.Append(new Run(new Text(lines[i])));
        }
        return p;
    }

    private static Paragraph Blank() => new();

    [Fact]
    public void Parse_TitleComesFromFileName()
    {
        var song = ParseTemp("Amazing Grace", Para("Line one"));

        Assert.Equal("Amazing Grace", song.Title);
    }

    [Fact]
    public void Parse_LineBreaksWithinOneParagraph_BecomeOneVerse()
    {
        var song = ParseTemp("song1", Para("Line one", "Line two", "Line three"));

        var section = Assert.Single(song.Sections);
        Assert.Equal(SectionType.Verse, section.Type);
        Assert.Equal("Line one\nLine two\nLine three", section.Lyrics);
    }

    [Fact]
    public void Parse_BlankParagraph_SeparatesVerses()
    {
        var song = ParseTemp("song2",
            Para("Verse one line a", "Verse one line b"),
            Blank(),
            Para("Verse two"));

        Assert.Equal(2, song.Sections.Count);
        Assert.Equal("Verse one line a\nVerse one line b", song.Sections[0].Lyrics);
        Assert.Equal("Verse two", song.Sections[1].Lyrics);
        Assert.Equal([1, 2], song.Sections.Select(s => s.SectionNumber));
        Assert.Equal([0, 1], song.Sections.Select(s => s.Order));
    }

    [Fact]
    public void Parse_OneParagraphPerLine_GroupsConsecutiveParagraphsUntilBlank()
    {
        var song = ParseTemp("song3",
            Para("Aleluya"), Para("Aleluya"),
            Blank(),
            Para("Yo te adoro"));

        Assert.Equal(2, song.Sections.Count);
        Assert.Equal("Aleluya\nAleluya", song.Sections[0].Lyrics);
        Assert.Equal("Yo te adoro", song.Sections[1].Lyrics);
    }

    [Fact]
    public void Parse_NoLyrics_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid():N}.docx");
        _tempFiles.Add(path);
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(Blank()));
            mainPart.Document.Save();
        }

        Assert.Throws<InvalidDataException>(() => DocxParser.Parse(path));
    }

    [Fact]
    public void Dispatcher_ImportRoutesDocxToDocxParser()
    {
        var path = Path.Combine(Path.GetTempPath(), $"routed_{Guid.NewGuid():N}.docx");
        _tempFiles.Add(path);
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(Para("Only line")));
            mainPart.Document.Save();
        }

        var song = SongFormatDispatcher.Import(path);

        Assert.Single(song.Sections);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }
}
