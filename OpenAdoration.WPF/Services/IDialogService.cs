namespace OpenAdoration.WPF.Services;

public interface IDialogService
{
    bool Confirm(string message, string title = "Confirm");

    /// <summary>Shows a modal informational message (e.g. an import result). Blocks until dismissed.</summary>
    void Inform(string message, string title = "OpenAdoration");
}
