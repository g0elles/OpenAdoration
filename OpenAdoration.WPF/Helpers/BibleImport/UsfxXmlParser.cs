using System.IO;
using System.Text;
using System.Xml;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the USFX XML format used by eBible.org and the
/// <c>seven1m/open-bibles</c> repository (the source for RV1909, PDT, VBL, BES).
///
/// USFX is a sequential marker-based format. Verse text is collected between
/// <c>&lt;v id="1"/&gt;</c> (verse start) and <c>&lt;ve/&gt;</c> (verse end)
/// as sibling nodes inside paragraph elements.
///
/// Elements whose text is silently dropped: note, f, ef, fe, x, ex.
/// </summary>
internal static class UsfxXmlParser
{
    private static readonly HashSet<string> SkipContent = new(StringComparer.OrdinalIgnoreCase)
        { "note", "f", "ef", "fe", "x", "ex", "rq", "zw" };

    public static BibleImportResult Parse(string filePath)
    {
        var version = new BibleVersion { Name = "Unknown", Abbreviation = "?", Language = "Unknown" };
        var books   = new List<BibleBook>();
        var merger  = new VerseMerger();

        // Parser state
        string currentBookId   = "";
        string currentBookName = "";
        string currentBookAbbr = "";
        int    currentBookPos  = 0;
        int    currentChapter  = 0;
        int    currentVerse    = 0;
        int    chapCount       = 0;
        bool   inVerse         = false;
        int    skipDepth       = 0;
        int    tocLevel        = 0;
        var    verseText       = new StringBuilder();

        // The book name used for BOTH the BibleBook row and its verses, so they always
        // match on lookup. Files without an <h>/toc name fall back to the canonical
        // catalog name (e.g. id "JDG" → "Judges"), never the raw book id (G21).
        string ResolveBookName() =>
            currentBookName.Length > 0
                ? currentBookName
                : OsisBookCatalog.GetOrFallback(currentBookId, currentBookPos, currentBookName).Name;

        void FinaliseVerse()
        {
            if (!inVerse || currentVerse == 0) return;
            var text = verseText.ToString().Trim();
            if (text.Length > 0)
                merger.Add(ResolveBookName(), currentChapter, currentVerse, text);
            inVerse   = false;
            skipDepth = 0;
            verseText.Clear();
        }

        void FinaliseBook()
        {
            if (currentBookPos == 0) return;
            var info = OsisBookCatalog.GetOrFallback(currentBookId, currentBookPos, currentBookName);
            books.Add(new BibleBook
            {
                Name         = ResolveBookName(),
                Abbreviation = currentBookAbbr.Length > 0 ? currentBookAbbr : info.Abbreviation,
                Testament    = info.Testament,
                BookNumber   = info.Number > 0 ? info.Number : currentBookPos,
                ChapterCount = chapCount
            });
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing           = DtdProcessing.Prohibit,
            XmlResolver             = null,
            MaxCharactersInDocument = 50_000_000,
            IgnoreComments          = true
        };

        using var reader = XmlReader.Create(filePath, settings);

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    var el = reader.LocalName;

                    // Skip-content depth tracking
                    if (SkipContent.Contains(el) && !reader.IsEmptyElement)
                    {
                        skipDepth++;
                        break;
                    }

                    switch (el)
                    {
                        // Top-level language code
                        case "languageCode":
                            if (!reader.IsEmptyElement)
                                version.Language = reader.ReadElementContentAsString();
                            break;

                        // New book: finalise previous, reset state
                        case "book":
                        {
                            FinaliseVerse();
                            FinaliseBook();
                            currentBookId   = reader.GetAttribute("id") ?? string.Empty;
                            currentBookName = string.Empty;
                            currentBookAbbr = string.Empty;
                            currentBookPos++;
                            currentChapter  = 0;
                            currentVerse    = 0;
                            chapCount       = 0;
                            break;
                        }

                        // Book display name (first occurrence wins)
                        case "h" when currentBookName.Length == 0 && !reader.IsEmptyElement:
                            currentBookName = reader.ReadElementContentAsString().Trim();
                            break;

                        // Table of contents entries — level 2 = short name, level 3 = abbreviation
                        case "toc":
                        {
                            tocLevel = int.TryParse(reader.GetAttribute("level"), out var lv) ? lv : 0;
                            if (!reader.IsEmptyElement)
                            {
                                var tocText = reader.ReadElementContentAsString().Trim();
                                if (tocLevel == 2 && currentBookName.Length == 0)
                                    currentBookName = tocText;
                                if (tocLevel == 3 && currentBookAbbr.Length == 0)
                                    currentBookAbbr = tocText;
                            }
                            break;
                        }

                        // Chapter marker (always self-closing in well-formed USFX)
                        case "c":
                        {
                            FinaliseVerse();
                            if (int.TryParse(reader.GetAttribute("id"), out var cn))
                            {
                                currentChapter = cn;
                                chapCount      = Math.Max(chapCount, cn);
                            }
                            currentVerse = 0;
                            break;
                        }

                        // Verse start marker (usually self-closing: <v id="1"/>)
                        case "v":
                        {
                            FinaliseVerse();
                            if (int.TryParse(reader.GetAttribute("id"), out var vn))
                                currentVerse = vn;
                            inVerse   = true;
                            skipDepth = 0;
                            verseText.Clear();
                            break;
                        }

                        // Verse end marker (always self-closing: <ve/>)
                        case "ve":
                            FinaliseVerse();
                            break;
                    }
                    break;
                }

                case XmlNodeType.EndElement:
                    if (SkipContent.Contains(reader.LocalName) && skipDepth > 0)
                        skipDepth--;
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.SignificantWhitespace:
                    if (inVerse && skipDepth == 0)
                        verseText.Append(reader.Value);
                    break;
            }
        }

        // Flush last book
        FinaliseVerse();
        FinaliseBook();

        if (version.Name == "Unknown")
            version.Name = Path.GetFileNameWithoutExtension(filePath);

        if (books.Count == 0)
            throw new InvalidDataException("No <book> elements found in USFX XML.");

        return new BibleImportResult(version, books, merger.Verses);
    }
}
