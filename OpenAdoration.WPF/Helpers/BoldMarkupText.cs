using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OpenAdoration.Domain.Common;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Attached property that renders <see cref="BoldMarkup"/> "**bold**" spans as Inlines on a
/// TextBlock (F8) — a drop-in replacement for binding straight to TextBlock.Text.
/// </summary>
public static class BoldMarkupText
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(BoldMarkupText), new PropertyMetadata(null, OnTextChanged));

    public static void SetText(TextBlock target, string? value) => target.SetValue(TextProperty, value);
    public static string? GetText(TextBlock target) => (string?)target.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock block) Apply(block, e.NewValue as string ?? string.Empty);
    }

    public static void Apply(TextBlock block, string text)
    {
        block.Inlines.Clear();
        foreach (var segment in BoldMarkup.Split(text))
        {
            var run = new Run(segment.Text);
            if (segment.IsBold) run.FontWeight = FontWeights.Bold;
            block.Inlines.Add(run);
        }
    }
}
