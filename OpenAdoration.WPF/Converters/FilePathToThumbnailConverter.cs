using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using OpenAdoration.WPF.Helpers;

namespace OpenAdoration.WPF.Converters;

/// <summary>
/// File path → Windows shell thumbnail (works for images AND videos). Used by the media
/// library tiles and the schedule add-media picker so the operator sees what they're choosing.
/// </summary>
public sealed class FilePathToThumbnailConverter : IValueConverter
{
    private const int Size = 256; // covers the 90px tile + 28px picker row after downscale

    // ponytail: cache keyed by path only — media files don't change in place at runtime.
    // Resolved synchronously on the UI thread; the OS caches thumbnails so it's fast for the
    // dozens-of-files libraries churches have. Upgrade to async PriorityBinding if it ever janks.
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string path && !string.IsNullOrWhiteSpace(path)
            ? Cache.GetOrAdd(path, p => ShellThumbnail.TryGet(p, Size))
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
