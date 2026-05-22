using System.Globalization;
using System.Windows.Data;

namespace OpenAdoration.WPF.Converters;

public sealed class IntEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is int a && values[1] is int b)
            return a == b;
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
