using System.Windows.Markup;

namespace OpenAdoration.WPF.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Loc SomeKey}</c> resolves to a live one-way binding
/// against <see cref="TranslationSource"/>, so the text updates automatically when the
/// application language changes.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Fully-qualified to avoid the System.Windows.Forms.Binding ambiguity (G1).
        var binding = new System.Windows.Data.Binding($"[{Key}]")
        {
            Source = TranslationSource.Instance,
            Mode   = System.Windows.Data.BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
