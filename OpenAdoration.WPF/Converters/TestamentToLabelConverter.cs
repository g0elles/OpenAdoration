using System.Globalization;
using System.Windows.Data;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Converters;

/// <summary>
/// Converts a <see cref="Testament"/> enum value (used as a CollectionViewSource group key)
/// to a human-readable section header: "OLD TESTAMENT" or "NEW TESTAMENT".
/// </summary>
[ValueConversion(typeof(Testament), typeof(string))]
public sealed class TestamentToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Testament t
            ? (t == Testament.Old ? "OLD TESTAMENT" : "NEW TESTAMENT")
            : value?.ToString() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
