namespace OpenAdoration.WPF.Services;

public interface IDialogService
{
    bool Confirm(string message, string title = "Confirm");
}
