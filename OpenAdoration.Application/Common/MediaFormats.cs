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

    public static bool IsVideo(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && VideoExtensions.Contains(Path.GetExtension(path));
}
