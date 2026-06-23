using System.IO;

namespace OpenAdoration.Application.Common;

/// <summary>
/// Central knowledge of which media files are video (vs. image). Shared by the projection
/// engine, the stage preview, and <see cref="IProjectionService"/> so the definition of
/// "a video slide" is identical everywhere.
/// </summary>
public static class MediaFormats
{
    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".m4v" };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };

    public static bool IsVideo(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && VideoExtensions.Contains(Path.GetExtension(path));

    public static bool IsImage(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && ImageExtensions.Contains(Path.GetExtension(path));

    /// <summary>A path the app can project — either a known image or video extension.</summary>
    public static bool IsSupported(string? path) => IsImage(path) || IsVideo(path);
}
