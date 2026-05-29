using System.IO;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Validates that a media file's leading bytes match a known image/video signature,
/// so a renamed or corrupt file is rejected at import rather than failing later
/// during projection.
/// </summary>
public static class MediaSignatureValidator
{
    private const int HeaderSize = 16;

    /// <summary>
    /// True when the file header matches a supported signature for the expected category.
    /// </summary>
    public static bool IsValid(string path, bool expectVideo)
    {
        byte[] header = new byte[HeaderSize];
        int read;
        try
        {
            using var fs = File.OpenRead(path);
            read = fs.Read(header, 0, HeaderSize);
        }
        catch
        {
            return false;
        }

        // A real media container/header needs at least a few bytes.
        if (read < 12) return false;

        var span = header.AsSpan(0, read);
        return expectVideo ? IsKnownVideo(span) : IsKnownImage(span);
    }

    private static bool IsKnownImage(ReadOnlySpan<byte> h) =>
        StartsWith(h, 0xFF, 0xD8, 0xFF)                                  // JPEG
        || StartsWith(h, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) // PNG
        || StartsWithAscii(h, "GIF8")                                    // GIF87a / GIF89a
        || StartsWithAscii(h, "BM")                                      // BMP
        || StartsWith(h, 0x49, 0x49, 0x2A, 0x00)                         // TIFF little-endian
        || StartsWith(h, 0x4D, 0x4D, 0x00, 0x2A)                         // TIFF big-endian
        || (StartsWithAscii(h, "RIFF") && AsciiAt(h, 8, "WEBP"));        // WEBP

    private static bool IsKnownVideo(ReadOnlySpan<byte> h) =>
        AsciiAt(h, 4, "ftyp")                                            // MP4 / M4V / MOV (ISO BMFF)
        || (StartsWithAscii(h, "RIFF") && AsciiAt(h, 8, "AVI "))         // AVI
        || StartsWith(h, 0x30, 0x26, 0xB2, 0x75)                         // ASF / WMV
        || StartsWith(h, 0x1A, 0x45, 0xDF, 0xA3);                        // Matroska / WebM (MKV)

    private static bool StartsWith(ReadOnlySpan<byte> h, params byte[] sig)
    {
        if (h.Length < sig.Length) return false;
        for (var i = 0; i < sig.Length; i++)
            if (h[i] != sig[i]) return false;
        return true;
    }

    private static bool StartsWithAscii(ReadOnlySpan<byte> h, string ascii) => AsciiAt(h, 0, ascii);

    private static bool AsciiAt(ReadOnlySpan<byte> h, int offset, string ascii)
    {
        if (h.Length < offset + ascii.Length) return false;
        for (var i = 0; i < ascii.Length; i++)
            if (h[offset + i] != (byte)ascii[i]) return false;
        return true;
    }
}
