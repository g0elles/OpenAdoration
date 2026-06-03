using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the Zefania XML Bible format — the most common exchange format
/// used by OpenSong, SongBeamer, EasyWorship, and many free Bible repositories.
///
/// Root element is <c>&lt;XMLBIBLE&gt;</c> (or <c>&lt;ZEFANIA&gt;</c>):
/// <code>
/// &lt;XMLBIBLE biblename="King James Version"&gt;
///   &lt;INFORMATION&gt;
///     &lt;title&gt;…&lt;/title&gt;
///     &lt;identifier&gt;KJV&lt;/identifier&gt;
///     &lt;language&gt;ENG&lt;/language&gt;
///   &lt;/INFORMATION&gt;
///   &lt;BIBLEBOOK bnumber="1" bname="Genesis" bsname="Gen"&gt;
///     &lt;CHAPTER cnumber="1"&gt;
///       &lt;VERS vnumber="1"&gt;In the beginning…&lt;/VERS&gt;
///     &lt;/CHAPTER&gt;
///   &lt;/BIBLEBOOK&gt;
/// &lt;/XMLBIBLE&gt;
/// </code>
/// </summary>
internal static class ZefaniaXmlParser
{
    // Tags whose text content should be skipped (footnotes, cross-refs)
    private static readonly HashSet<string> SkipTags = new(StringComparer.OrdinalIgnoreCase)
        { "XREF", "NOTE", "GRAM" };

    // Hardened XML reader settings — applied to all Zefania parses
    private static readonly XmlReaderSettings HardenedSettings = new()
    {
        DtdProcessing           = DtdProcessing.Prohibit,   // reject DTD declarations (S1)
        XmlResolver             = null,                       // no external resource fetches
        IgnoreComments          = true,
        MaxCharactersInDocument = 50_000_000                 // ~50 M chars ceiling
    };

    public static BibleImportResult Parse(string filePath)
    {
        using var xmlReader = XmlReader.Create(filePath, HardenedSettings);
        var doc  = XDocument.Load(xmlReader);
        var root = doc.Root ?? throw new InvalidDataException("Empty XML file.");

        // ── Version metadata ──────────────────────────────────────────────────
        var info  = root.Element("INFORMATION");
        string name = root.Attribute("biblename")?.Value
                   ?? info?.Element("title")?.Value
                   ?? Path.GetFileNameWithoutExtension(filePath);
        string abbr = info?.Element("identifier")?.Value
                   ?? info?.Element("abbreviation")?.Value
                   ?? name[..Math.Min(5, name.Length)];
        string lang = info?.Element("language")?.Value ?? "Unknown";

        var version = new BibleVersion { Name = name, Abbreviation = abbr, Language = lang };

        // ── Books ─────────────────────────────────────────────────────────────
        var books  = new List<BibleBook>();
        var merger = new VerseMerger();
        int bookPos = 0;

        foreach (var bookEl in root.Elements("BIBLEBOOK"))
        {
            bookPos++;
            int bookNum  = ParseInt(bookEl.Attribute("bnumber")?.Value) ?? bookPos;
            string bname = bookEl.Attribute("bname")?.Value ?? $"Book {bookNum}";
            string babbr = bookEl.Attribute("bsname")?.Value ?? bname[..Math.Min(4, bname.Length)];
            var testament = bookNum >= 40 ? Testament.New : Testament.Old;

            int chapCount = AppendBookVerses(bookEl, bname, merger);

            books.Add(new BibleBook
            {
                Name         = bname,
                Abbreviation = babbr,
                Testament    = testament,
                BookNumber   = bookNum,
                ChapterCount = chapCount
            });
        }

        if (books.Count == 0)
            throw new InvalidDataException("No BIBLEBOOK elements found in Zefania XML.");

        return new BibleImportResult(version, books, merger.Verses);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Feeds a book's verses into <paramref name="merger"/> (which collapses any VERS
    /// elements that repeat a verse number — a common Zefania idiom for split verses).
    /// Returns the highest chapter number seen.
    /// </summary>
    private static int AppendBookVerses(XElement bookEl, string bookName, VerseMerger merger)
    {
        int chapCount = 0, chapPos = 0;

        foreach (var chapEl in bookEl.Elements("CHAPTER"))
        {
            chapPos++;
            int chapNum = ParseInt(chapEl.Attribute("cnumber")?.Value) ?? chapPos;
            chapCount   = Math.Max(chapCount, chapNum);

            int versePos = 0;
            foreach (var versEl in chapEl.Elements("VERS"))
            {
                versePos++;
                int verseNum = ParseInt(versEl.Attribute("vnumber")?.Value) ?? versePos;
                var text     = ExtractText(versEl).Trim();
                if (text.Length == 0) continue;

                merger.Add(bookName, chapNum, verseNum, text);
            }
        }

        return chapCount;
    }

    /// <summary>
    /// Recursively collects text from an element, skipping the content
    /// of footnote and cross-reference child elements.
    /// </summary>
    private static string ExtractText(XElement el)
    {
        var sb = new StringBuilder();
        foreach (var node in el.Nodes())
        {
            if (node is XText t)
                sb.Append(t.Value);
            else if (node is XElement child && !SkipTags.Contains(child.Name.LocalName))
                sb.Append(ExtractText(child)); // recurse into styling elements
        }
        return sb.ToString();
    }

    private static int? ParseInt(string? s)
        => int.TryParse(s, out var i) ? i : null;
}
