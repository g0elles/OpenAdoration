using System.IO;

namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>
/// VideoPsalm ships complete Bibles only as <c>.vpc</c> files whose single entry is
/// <b>AES-encrypted (ZIP method 99)</b> — uniformly, even public-domain modules. There is no
/// unencrypted VideoPsalm Bible to parse, so OpenAdoration never imports one: it only detects
/// the DRM so the "Import Bible…" UI can tell the operator plainly to obtain the version from a
/// legal source. We never decrypt. See VIDEOPSALM_REFERENCE.md §8b.
/// </summary>
public static class VideoPsalmBibleDetector
{
    private const int AesCompressionMethod = 99; // WinZip AES (ZIP method 99)

    /// <summary>
    /// True if the file is a DRM-protected VideoPsalm Bible (its first ZIP entry uses AES).
    /// Reads only the first local file header. (ponytail: trusts the encrypted entry is the
    /// first local header — true for VP single-entry <c>.vpc</c>.)
    /// </summary>
    public static bool IsDrmProtected(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        Span<byte> header = stackalloc byte[10];
        if (stream.Read(header) < header.Length) return false;

        // Local file header signature "PK\x03\x04"; compression method is a LE ushort at offset 8.
        if (header[0] != 'P' || header[1] != 'K' || header[2] != 3 || header[3] != 4) return false;
        return (header[8] | (header[9] << 8)) == AesCompressionMethod;
    }
}
