using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace OpenAdoration.WPF.Localization;

/// <summary>
/// Singleton binding source for localized UI strings. XAML binds to its indexer
/// through <see cref="LocExtension"/>; changing <see cref="CurrentCulture"/> raises a
/// blanket <see cref="PropertyChanged"/> so every localized binding re-reads its value
/// live (no restart needed).
/// </summary>
public sealed class TranslationSource : INotifyPropertyChanged
{
    public static TranslationSource Instance { get; } = new();

    private readonly ResourceManager _resources =
        new("OpenAdoration.WPF.Resources.Strings", typeof(TranslationSource).Assembly);

    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    private TranslationSource() { }

    /// <summary>Returns the localized string for <paramref name="key"/>, or the key itself if missing.</summary>
    public string this[string key] => _resources.GetString(key, _currentCulture) ?? key;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Equals(value)) return;
            _currentCulture = value;
            // Empty property name asks WPF to refresh every binding on this source.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
