using System.IO;
using System.Xml;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers.SongImport.VideoPsalm;

namespace OpenAdoration.WPF.Helpers.SongImport;

/// <summary>
/// Auto-detects a song file's format and dispatches to the matching parser:
/// <list type="bullet">
///   <item>OpenLyrics XML     -- root &lt;song&gt; in the openlyrics.info namespace</item>
///   <item>OpenSong XML       -- root &lt;song&gt; with no namespace</item>
///   <item>VideoPsalm (.vpagd) -- ZIP agenda yielding one or more songs</item>
///   <item>Plain text         -- everything else (.txt or non-XML content)</item>
/// </list>
/// </summary>
public static class SongFormatDispatcher
{
    public const string FileDialogFilter =
        "Song files|*.xml;*.txt;*.openlyrics;*.opensong;*.vpagd;*.cho;*.crd;*.chopro;*.chordpro" +
        "|OpenLyrics / OpenSong XML (*.xml)|*.xml;*.openlyrics;*.opensong" +
        "|ChordPro (*.cho;*.crd;*.chopro)|*.cho;*.crd;*.chopro;*.chordpro" +
        "|VideoPsalm agenda (*.vpagd)|*.vpagd" +
        "|Plain text (*.txt)|*.txt" +
        "|All files|*.*";

    private const string VideoPsalmExtension = ".vpagd";
    private const long MaxFileSizeBytes = 20L * 1024 * 1024; // 20 MB

    /// <summary>
    /// Imports every song in <paramref name="filePath"/>. Single-song formats return a
    /// one-element list; a VideoPsalm agenda returns one song per <c>Song_{n}.json</c> entry.
    /// </summary>
    public static IReadOnlyList<Song> ImportMany(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        return IsVideoPsalm(filePath)
            ? VideoPsalmParser.Parse(filePath)
            : new[] { Import(filePath) };
    }

    public static Song Import(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        if (new FileInfo(filePath).Length > MaxFileSizeBytes)
            throw new InvalidDataException("Song file is too large to import.");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".txt"                                       => PlainTextParser.Parse(filePath),
            ".cho" or ".crd" or ".chopro" or ".chordpro" => ChordProParser.Parse(filePath),
            ".xml" or ".openlyrics" or ".opensong"       => ImportXml(filePath),
            _                                            => IsXml(filePath)
                                                                ? ImportXml(filePath)
                                                                : PlainTextParser.Parse(filePath)
        };
    }

    private static bool IsVideoPsalm(string filePath) =>
        Path.GetExtension(filePath).Equals(VideoPsalmExtension, StringComparison.OrdinalIgnoreCase);

    private static Song ImportXml(string filePath) =>
        IsOpenLyrics(filePath) ? OpenLyricsParser.Parse(filePath) : OpenSongParser.Parse(filePath);

    private static bool IsOpenLyrics(string filePath)
    {
        using var reader = XmlReader.Create(filePath, SafeSettings());
        while (reader.Read())
            if (reader.NodeType == XmlNodeType.Element)
                return reader.NamespaceURI.Contains("openlyrics.info", StringComparison.OrdinalIgnoreCase);

        throw new InvalidDataException("The XML file has no root element.");
    }

    private static bool IsXml(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b is ' ' or '\t' or '\r' or '\n' or 0xEF or 0xBB or 0xBF or 0xFF or 0xFE) continue;
            return b == '<';
        }
        return false;
    }

    private static XmlReaderSettings SafeSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver   = null,
        IgnoreComments = true
    };
}
