using System.IO;
using System.Text.Json;
using System.Xml;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Auto-detects the format of a Bible data file and dispatches to the
/// appropriate parser. Supports:
/// <list type="bullet">
///   <item>Zefania XML  — <c>XMLBIBLE</c> / <c>ZEFANIA</c> root</item>
///   <item>OSIS XML     — <c>osis</c> root</item>
///   <item>USFX XML    — <c>usfx</c> root</item>
///   <item>OpenAdoration JSON — <c>{ "name":…, "books":[…] }</c></item>
///   <item>Array JSON (thiagobodruk) — <c>[{ "abbrev":…, "chapters":[[…]] }]</c></item>
/// </list>
/// </summary>
public static class BibleFormatDispatcher
{
    // Accepted file extensions shown in the file-open dialog
    public const string FileDialogFilter =
        "Bible files|*.xml;*.json" +
        "|Zefania XML (OpenSong, EasyWorship)|*.xml" +
        "|OSIS XML (CrossWire SWORD)|*.xml" +
        "|USFX XML (eBible / seven1m)|*.xml" +
        "|OpenAdoration JSON|*.json" +
        "|Array JSON (thiagobodruk format)|*.json" +
        "|All files|*.*";

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Detects the file format and returns parsed Bible data, or throws
    /// <see cref="InvalidDataException"/> when the format is unrecognised.
    /// </summary>
    public static BibleImportResult Import(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".xml"  => ImportXml(filePath),
            ".json" => ImportJson(filePath),
            _       => TryAll(filePath)    // unknown extension — try each parser
        };
    }

    // ── XML dispatch ──────────────────────────────────────────────────────────

    private static BibleImportResult ImportXml(string filePath)
    {
        var rootName = PeekXmlRoot(filePath);

        return rootName.ToUpperInvariant() switch
        {
            "XMLBIBLE" or "ZEFANIA" or "ZEFANIABIBLE" or "XMLBIBLELT"
                => ZefaniaXmlParser.Parse(filePath),

            "OSIS"
                => OsisXmlParser.Parse(filePath),

            "USFX"
                => UsfxXmlParser.Parse(filePath),

            _ => throw new InvalidDataException(
                    $"Unrecognised XML Bible format. Root element: <{rootName}>. " +
                    "Expected XMLBIBLE (Zefania), osis (OSIS), or usfx (USFX).")
        };
    }

    /// <summary>Reads just far enough into the file to obtain the root element name.</summary>
    private static string PeekXmlRoot(string filePath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing  = DtdProcessing.Ignore,
            IgnoreComments = true
        };

        using var reader = XmlReader.Create(filePath, settings);

        while (reader.Read())
            if (reader.NodeType == XmlNodeType.Element)
                return reader.LocalName;

        throw new InvalidDataException("The XML file has no root element.");
    }

    // ── JSON dispatch ─────────────────────────────────────────────────────────

    private static BibleImportResult ImportJson(string filePath)
    {
        // Peek at just the first non-whitespace character to decide root type
        using var stream = File.OpenRead(filePath);
        using var doc    = JsonDocument.Parse(stream,
            new JsonDocumentOptions { AllowTrailingCommas = true });

        return doc.RootElement.ValueKind switch
        {
            // Root array → thiagobodruk  ([ { "abbrev": …, "chapters": [[…]] } ])
            JsonValueKind.Array => ThiagobodrukJsonParser.Parse(filePath),

            // Root object → inspect further
            JsonValueKind.Object => ClassifyJsonObject(doc.RootElement, filePath),

            _ => throw new InvalidDataException("JSON root must be an object or array.")
        };
    }

    private static BibleImportResult ClassifyJsonObject(JsonElement root, string filePath)
    {
        // OpenAdoration format: top-level "books" array whose items have "chapters"
        // that are arrays of *objects* (not arrays of arrays).
        if (root.TryGetProperty("books", out var booksEl) && booksEl.ValueKind == JsonValueKind.Array)
        {
            // Peek inside: thiagobodruk wrapped in { "books": [...] } has array-of-array chapters;
            // OpenAdoration has array-of-object chapters.
            if (booksEl.GetArrayLength() > 0)
            {
                var firstBook = booksEl.EnumerateArray().First();
                if (firstBook.TryGetProperty("chapters", out var chaps) && chaps.ValueKind == JsonValueKind.Array)
                {
                    var firstChap = chaps.EnumerateArray().FirstOrDefault();
                    if (firstChap.ValueKind == JsonValueKind.Array)
                        return ThiagobodrukJsonParser.Parse(filePath); // { "books": [[chapter arrays]] }
                }
            }

            return OpenADorationJsonParser.Parse(filePath);
        }

        // Fallback: try OpenAdoration parser (will throw descriptively on failure)
        return OpenADorationJsonParser.Parse(filePath);
    }

    // ── Unknown extension — try parsers in order ──────────────────────────────

    private static BibleImportResult TryAll(string filePath)
    {
        // Check if it looks like XML (starts with '<' after any BOM/whitespace)
        using var stream = File.OpenRead(filePath);
        Span<byte> buf   = stackalloc byte[4];
        var read = stream.Read(buf);
        stream.Seek(0, SeekOrigin.Begin);

        bool looksLikeXml = read > 0 && (buf[0] == '<' || (buf[0] == 0xEF && read > 2)); // UTF-8 BOM

        if (looksLikeXml)
            return ImportXml(filePath);

        return ImportJson(filePath);
    }
}
