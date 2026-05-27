using System.IO;
using System.Text.Json;
using System.Xml;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Auto-detects the format of a Bible data file and dispatches to the
/// appropriate parser. Supports:
/// <list type="bullet">
///   <item>Zefania XML  -- <c>XMLBIBLE</c> / <c>ZEFANIA</c> root</item>
///   <item>OSIS XML     -- <c>osis</c> root</item>
///   <item>USFX XML    -- <c>usfx</c> root</item>
///   <item>OpenAdoration JSON -- <c>{ "name":..., "books":[...] }</c></item>
///   <item>Array JSON (thiagobodruk) -- <c>[{ "abbrev":..., "chapters":[[...]] }]</c></item>
///   <item>BibleSuperSearch JSON -- <c>{ "metadata":{...}, "verses":[...] }</c></item>
///   <item>BibleSuperSearch ZIP -- <c>info.json</c> + <c>verses.txt</c> inside a <c>.zip</c></item>
///   <item>BibleSuperSearch SQLite -- <c>meta</c> + <c>verses</c> tables in a <c>.sqlite</c></item>
/// </list>
/// </summary>
public static class BibleFormatDispatcher
{
    // Accepted file extensions shown in the file-open dialog
    public const string FileDialogFilter =
        "Bible files|*.xml;*.json;*.zip;*.sqlite" +
        "|Zefania XML (OpenSong, EasyWorship)|*.xml" +
        "|OSIS XML (CrossWire SWORD)|*.xml" +
        "|USFX XML (eBible / seven1m)|*.xml" +
        "|OpenAdoration JSON|*.json" +
        "|Array JSON (thiagobodruk format)|*.json" +
        "|BibleSuperSearch ZIP|*.zip" +
        "|BibleSuperSearch SQLite|*.sqlite" +
        "|All files|*.*";

    // Maximum file size accepted before any parsing attempt (S2)
    private const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB

    // -- Public entry point ----------------------------------------------------

    /// <summary>
    /// Detects the file format and returns parsed Bible data, or throws
    /// <see cref="InvalidDataException"/> when the format is unrecognised.
    /// </summary>
    public static BibleImportResult Import(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidDataException(
                $"File is too large ({fileInfo.Length / 1_048_576} MB). " +
                $"The maximum accepted size is {MaxFileSizeBytes / 1_048_576} MB.");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".xml"    => ImportXml(filePath),
            ".json"   => ImportJson(filePath),
            ".zip"    => BibleSuperSearchZipParser.Parse(filePath),
            ".sqlite" => BibleSuperSearchSqliteParser.Parse(filePath),
            _         => TryAll(filePath)    // unknown extension -- sniff first byte
        };
    }

    // -- XML dispatch ----------------------------------------------------------

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
            DtdProcessing           = DtdProcessing.Prohibit,
            XmlResolver             = null,
            MaxCharactersInDocument = 50_000_000,
            IgnoreComments          = true
        };

        using var reader = XmlReader.Create(filePath, settings);

        while (reader.Read())
            if (reader.NodeType == XmlNodeType.Element)
                return reader.LocalName;

        throw new InvalidDataException("The XML file has no root element.");
    }

    // -- JSON dispatch ---------------------------------------------------------

    private static BibleImportResult ImportJson(string filePath)
    {
        // Parse the file exactly once.  The loaded JsonDocument is reused for both
        // format classification and data extraction -- no second full-file read (P2-2).
        using var stream = File.OpenRead(filePath);
        using var doc    = JsonDocument.Parse(stream,
            new JsonDocumentOptions { AllowTrailingCommas = true, MaxDepth = 32 });

        var root = doc.RootElement;

        // doc stays alive until the end of this using block, which is after the
        // ParseElement call returns -- JsonElement references into doc remain valid.
        return root.ValueKind switch
        {
            // Root array -> thiagobodruk  ([ { "abbrev": ..., "chapters": [[...]] } ])
            JsonValueKind.Array => ThiagobodrukJsonParser.ParseElement(root, filePath),

            // Root object -> inspect structure to pick the right parser
            JsonValueKind.Object => ClassifyAndParseJsonObject(root, filePath),

            _ => throw new InvalidDataException("JSON root must be an object or array.")
        };
    }

    private static BibleImportResult ClassifyAndParseJsonObject(JsonElement root, string filePath)
    {
        // BibleSuperSearch JSON: { "metadata": {...}, "verses": [...] }
        // Must be checked before the "books" branch — BSS files never have a "books" key.
        if (root.TryGetProperty("metadata", out _) && root.TryGetProperty("verses", out _))
            return BibleSuperSearchJsonParser.ParseElement(root);

        // OpenAdoration format: top-level "books" array whose items have "chapters"
        // that are arrays of *objects* (not arrays of arrays).
        if (root.TryGetProperty("books", out var booksEl) && booksEl.ValueKind == JsonValueKind.Array)
        {
            // Peek inside the first book's chapters to distinguish formats:
            //   thiagobodruk wrapped:  { "books": [ { "chapters": [[strings]] } ] }
            //   OpenAdoration:         { "books": [ { "chapters": [{ "verses": [...] }] } ] }
            if (booksEl.GetArrayLength() > 0)
            {
                var firstBook = booksEl.EnumerateArray().First();
                if (firstBook.TryGetProperty("chapters", out var chaps)
                    && chaps.ValueKind == JsonValueKind.Array)
                {
                    var firstChap = chaps.EnumerateArray().FirstOrDefault();
                    if (firstChap.ValueKind == JsonValueKind.Array)
                        return ThiagobodrukJsonParser.ParseElement(root, filePath);
                }
            }
            return OpenADorationJsonParser.ParseElement(root);
        }

        // Fallback: try OpenAdoration parser (throws descriptively on failure)
        return OpenADorationJsonParser.ParseElement(root);
    }

    // -- Unknown extension: sniff first content byte --------------------------

    private static BibleImportResult TryAll(string filePath)
    {
        // Open the stream, skip any BOM and whitespace, then inspect the first
        // actual content byte.  We read byte-by-byte so any amount of leading
        // whitespace is handled correctly -- a fixed 8-byte buffer can miss a '<'
        // that is preceded by a BOM plus more than a couple of whitespace chars (R4).
        byte first;
        using (var stream = File.OpenRead(filePath))
            first = PeekFirstContentByte(stream);

        if (first == (byte)'<')
            return ImportXml(filePath);

        return ImportJson(filePath);
    }

    /// <summary>
    /// Reads from <paramref name="stream"/> until the first byte that is not part
    /// of a recognized BOM and not ASCII whitespace (space, tab, CR, LF).
    /// Returns 0 if the stream is empty or contains only BOM/whitespace.
    /// Handles UTF-8 BOM (EF BB BF), UTF-16 LE BOM (FF FE), UTF-16 BE BOM (FE FF).
    /// </summary>
    private static byte PeekFirstContentByte(Stream stream)
    {
        // Read a 3-byte header to detect and consume any BOM.
        Span<byte> header = stackalloc byte[3];
        int headerRead = 0;
        while (headerRead < 3)
        {
            int n = stream.Read(header[headerRead..]);
            if (n == 0) break;
            headerRead += n;
        }

        // Determine how many header bytes belong to the BOM (to skip) vs. content.
        int bomLength;
        if (headerRead >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
            bomLength = 3; // UTF-8 BOM
        else if (headerRead >= 2 && header[0] == 0xFF && header[1] == 0xFE)
            bomLength = 2; // UTF-16 LE BOM
        else if (headerRead >= 2 && header[0] == 0xFE && header[1] == 0xFF)
            bomLength = 2; // UTF-16 BE BOM
        else
            bomLength = 0; // no BOM -- all header bytes are content

        // Check the non-BOM header bytes for the first content byte.
        for (int i = bomLength; i < headerRead; i++)
        {
            byte b = header[i];
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                return b;
        }

        // Continue reading byte-by-byte to skip any leading whitespace.
        // This handles arbitrarily indented XML/JSON openings.
        int ch;
        while ((ch = stream.ReadByte()) != -1)
        {
            byte b = (byte)ch;
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                return b;
        }

        return 0; // empty or whitespace-only file
    }
}
