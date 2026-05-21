using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace OpenAdoration.WPF.Converters;

/// <summary>
/// Converts an absolute file-path string to a BitmapImage.
/// Returns null for null, empty, or non-existent paths so Image.Source shows nothing.
/// </summary>
public sealed class FilePathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            // Decode to preview size — avoids loading a 4K source image to show a thumbnail (P5)
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource        = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption      = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 400; // generous for theme preview panels
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
