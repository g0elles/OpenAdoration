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
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
