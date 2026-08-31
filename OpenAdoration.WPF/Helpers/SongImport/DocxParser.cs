using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Parses a Word (.docx) song: the file name (without extension) becomes the title, and
/// each run of consecutive non-blank paragraphs becomes a sequentially numbered verse —
/// a blank paragraph ends the current verse, the same "blank line separates a verse"
/// heuristic <see cref="PlainTextParser"/> uses, at paragraph granularity. A manual line
/// break (<c>&lt;w:br/&gt;</c>) inside a paragraph becomes a newline within its verse.
/// </summary>
public static class DocxParser
{
    public static Song Parse(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("The Word file has no document body.");

        var sections = BuildSections(body.Elements<Paragraph>());
        if (sections.Count == 0)
            throw new InvalidDataException("The Word file contains no lyrics to import.");

        return new Song
        {
            Title    = Path.GetFileNameWithoutExtension(filePath),
            Sections = sections
        };
    }

    private static List<SongSection> BuildSections(IEnumerable<Paragraph> paragraphs)
    {
        var sections = new List<SongSection>();
        var buffer = new List<string>();

        void Flush()
        {
            var lyrics = string.Join("\n", buffer).Trim();
            if (lyrics.Length > 0)
                sections.Add(new SongSection
                {
                    Type          = OpenAdoration.Domain.Enums.SectionType.Verse,
                    SectionNumber = sections.Count + 1,
                    Lyrics        = lyrics,
                    Order         = sections.Count
                });
            buffer.Clear();
        }

        foreach (var paragraph in paragraphs)
        {
            var text = ParagraphText(paragraph);
            if (string.IsNullOrWhiteSpace(text))
                Flush();
            else
                buffer.Add(text);
        }
        Flush();

        return sections;
    }

    private static string ParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();
        foreach (var element in paragraph.Descendants())
        {
            switch (element)
            {
                case Text t:
                    sb.Append(t.Text);
                    break;
                case Break:
                    sb.Append('\n');
                    break;
            }
        }
        return sb.ToString();
    }
}
