using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OpenAdoration.WPF.Converters;

/// <summary>
/// Converts a nullable <see cref="System.Windows.Media.Color"/> to a <see cref="SolidColorBrush"/>.
/// Used to bind xctk:ColorPicker.SelectedColor directly to WPF brush-consuming properties.
/// </summary>
[ValueConversion(typeof(System.Windows.Media.Color?), typeof(SolidColorBrush))]
public sealed class ColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Media.Color color)
            return new SolidColorBrush(color);

        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
