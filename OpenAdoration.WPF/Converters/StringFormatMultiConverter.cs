using System.Globalization;
using System.Windows.Data;

namespace OpenAdoration.WPF.Converters;

/// <summary>
/// Formats a localized template string (values[0], e.g. <c>No songs found for "{0}".</c>) with the
/// remaining bound values as arguments. Use via a MultiBinding whose first child binds the
/// <c>{loc:Loc Key}</c> template — so the whole sentence (and its word order) stays in the
/// translator's hands and updates live when the language changes. The translation-safe way to
/// localize a sentence that contains a runtime value.
/// </summary>
public sealed class StringFormatMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not string template) return string.Empty;
        var args = values.Skip(1).ToArray();
        try { return string.Format(culture, template, args); }
        catch (FormatException) { return template; } // malformed template → show it raw, never crash
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
