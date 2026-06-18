using CommunityToolkit.Mvvm.ComponentModel;
using OpenAdoration.WPF.Localization;

namespace OpenAdoration.WPF.ViewModels;

/// <summary>
/// Base class for all ViewModels. Provides INotifyPropertyChanged via CommunityToolkit source generators.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    protected void SetError(string message)
    {
        ErrorMessage = message;
        OnPropertyChanged(nameof(HasError));
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HasError));
    }

    /// <summary>Localized string for <paramref name="key"/> (current UI culture).</summary>
    protected static string L(string key) => TranslationSource.Instance[key];

    /// <summary>Localized string with <see cref="string.Format(string,object[])"/> arguments.</summary>
    protected static string L(string key, params object[] args) =>
        string.Format(TranslationSource.Instance[key], args);
}
