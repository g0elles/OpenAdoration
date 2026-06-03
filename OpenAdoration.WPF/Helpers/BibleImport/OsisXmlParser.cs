using System.IO;
using System.Text;
using System.Xml;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Parses the OSIS XML Bible format (Open Scripture Information Standard).
/// Used by CrossWire SWORD modules and many scholarly tools.
///
/// Supports both container-style and milestone-style verses:
/// <code>
/// Container: &lt;verse osisID="Gen.1.1"&gt;text&lt;/verse&gt;
/// Milestone: &lt;verse sID="Gen.1.1"/&gt;text&lt;verse eID="Gen.1.1"/&gt;
/// </code>
/// Elements whose text is silently dropped: note, catchWord, rdg, ref, figure.
/// </summary>
internal static class OsisXmlParser
{
    private static readonly HashSet<string> SkipContent = new(StringComparer.OrdinalIgnoreCase)
        { "note", "catchWord", "rdg", "ref", "figure", "milestone", "lb" };

    public static BibleImportResult Parse(string filePath)
    {
        var version = new BibleVersion { Name = "Unknown", Abbreviation = "?", Language = "Unknown" };
        var books   = new List<BibleBook>();
        var merger  = new VerseMerger();

        // Parser state
        string currentBookOsisId = "";
        string currentBookName   = "";
        int    currentBookPos    = 0;
        int    currentChapter    = 0;
        int    currentVerse      = 0;
        int    currentChapCount  = 0;
        bool   inVerse           = false;
        int    skipDepth         = 0;  // > 0 when inside a skip element
        var    verseText         = new StringBuilder();

        // Book-level chapter count, finalised when the next book/end starts
        int lastChapterCount  = 0;

        // The book name used for BOTH the BibleBook row and its verses, so they always
        // match on lookup. Files without a <title> fall back to the canonical catalog
        // name (e.g. osisID "Judg" → "Judges"), never the raw osisID (G21).
        string ResolveBookName() =>
            currentBookName.Length > 0
                ? currentBookName
                : OsisBookCatalog.GetOrFallback(currentBookOsisId, currentBookPos, currentBookName).Name;

        void FinaliseBook()
        {
            if (currentBookPos == 0) return;
            var info = OsisBookCatalog.GetOrFallback(currentBookOsisId, currentBookPos, currentBookName);
            books.Add(new BibleBook
            {
                Name         = ResolveBookName(),
                Abbreviation = info.Abbreviation,
                Testament    = info.Testament,
                BookNumber   = info.Number > 0 ? info.Number : currentBookPos,
                ChapterCount = lastChapterCount
            });
        }

        void FinaliseVerse()
        {
            if (!inVerse || currentVerse == 0) return;
            var text = verseText.ToString().Trim();
            if (text.Length > 0)
                merger.Add(ResolveBookName(), currentChapter, currentVerse, text);
            inVerse  = false;
            skipDepth = 0;
            verseText.Clear();
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing           = DtdProcessing.Prohibit,
            XmlResolver             = null,
            MaxCharactersInDocument = 50_000_000,
            IgnoreComments          = true,
            IgnoreWhitespace        = false
        };

        using var reader = XmlReader.Create(filePath, settings);

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                // ── Start element ─────────────────────────────────────────────
                case XmlNodeType.Element:
                {
                    var name = reader.LocalName;

                    // Skip-content tracking (footnotes, cross-refs, etc.)
                    if (SkipContent.Contains(name) && !reader.IsEmptyElement)
                    {
                        skipDepth++;
                        break;
                    }

                    switch (name)
                    {
                        // Version metadata
                        case "work":
                        {
                            var osisWork = reader.GetAttribute("osisWork");
                            if (osisWork != null && version.Abbreviation == "?")
                                version.Abbreviation = osisWork;
                            break;
                        }
                        case "title" when reader.GetAttribute("type") == null:
                            // First bare <title> inside <header><work> = version name
                            // (will be overwritten on next iteration if inside a book div)
                            break;

                        // Book boundary — <div type="book">
                        case "div" when reader.GetAttribute("type") == "book":
                        {
                            FinaliseVerse();
                            FinaliseBook();
                            currentBookOsisId = reader.GetAttribute("osisID") ?? string.Empty;
                            currentBookName   = string.Empty;
                            currentBookPos++;
                            currentChapter   = 0;
                            currentVerse     = 0;
                            currentChapCount = 0;
                            lastChapterCount = 0;
                            break;
                        }

                        // Book name — first <title> inside the book div
                        case "title" when currentBookPos > 0 && currentBookName.Length == 0
                                       && reader.GetAttribute("type") is null or "main":
                            currentBookName = reader.ReadElementContentAsString();
                            break;

                        // Chapter boundary
                        case "chapter":
                        {
                            FinaliseVerse();
                            var osisId = reader.GetAttribute("osisID");
                            if (osisId != null)
                            {
                                var parts = osisId.Split('.');
                                if (parts.Length >= 2 && int.TryParse(parts[^1], out var cn))
                                {
                                    currentChapter   = cn;
                                    currentChapCount = Math.Max(currentChapCount, cn);
                                    lastChapterCount = currentChapCount;
                                }
                            }
                            currentVerse = 0;
                            break;
                        }

                        // Verse — container style: <verse osisID="Gen.1.1">text</verse>
                        //         milestone start:  <verse sID="Gen.1.1"/>
                        case "verse":
                        {
                            var sId     = reader.GetAttribute("sID");
                            var eId     = reader.GetAttribute("eID");
                            var osisId  = reader.GetAttribute("osisID");
                            var isEmpty = reader.IsEmptyElement;

                            if (eId != null)
                            {
                                // Milestone end → save current verse
                                FinaliseVerse();
                                break;
                            }

                            // Milestone start or container start
                            FinaliseVerse();

                            var idToParse = sId ?? osisId;
                            if (idToParse != null)
                            {
                                var parts = idToParse.Split('.');
                                if (parts.Length >= 1 && int.TryParse(parts[^1], out var vn))
                                    currentVerse = vn;
                            }

                            if (currentVerse > 0)
                            {
                                inVerse   = !isEmpty; // container style is NOT empty
                                skipDepth = 0;
                                verseText.Clear();
                            }
                            break;
                        }
                    }
                    break;
                }

                // ── End element ───────────────────────────────────────────────
                case XmlNodeType.EndElement:
                {
                    var name = reader.LocalName;

                    if (SkipContent.Contains(name) && skipDepth > 0)
                    {
                        skipDepth--;
                        break;
                    }

                    if (name == "verse" && inVerse)
                        FinaliseVerse(); // container style end-tag
                    break;
                }

                // ── Text / significant whitespace ─────────────────────────────
                case XmlNodeType.Text:
                case XmlNodeType.SignificantWhitespace:
                    if (inVerse && skipDepth == 0)
                        verseText.Append(reader.Value);
                    break;

                // ── Attribute-like: version metadata ──────────────────────────
                case XmlNodeType.None:
                default:
                    break;
            }
        }

        // Flush the last book
        FinaliseVerse();
        FinaliseBook();

        // Try to pull version info from the osisText osisIDWork attribute if still unknown
        if (version.Name == "Unknown")
            version.Name = Path.GetFileNameWithoutExtension(filePath);

        if (books.Count == 0)
            throw new InvalidDataException("No books (div[@type='book']) found in OSIS XML.");

        return new BibleImportResult(version, books, merger.Verses);
    }
}
