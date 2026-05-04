using CommunityToolkit.Mvvm.ComponentModel;

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
}
