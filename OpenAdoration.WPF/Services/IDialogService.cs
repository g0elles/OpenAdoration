namespace OpenAdoration.WPF.Services;

public interface IDialogService
{
    bool Confirm(string message, string title = "Confirm");

    /// <summary>Shows a modal informational message (e.g. an import result). Blocks until dismissed.</summary>
    void Inform(string message, string title = "OpenAdoration");

    /// <summary>
    /// Prompts for a password. <paramref name="confirm"/> shows a second "confirm password" field
    /// (backup creation); <paramref name="allowBlank"/> lets the operator submit with no password
    /// (backup creation — blank means "don't encrypt"). Returns null if the dialog was cancelled,
    /// or "" if submitted blank when allowed.
    /// </summary>
    string? PromptPassword(string message, bool confirm, bool allowBlank, string title = "OpenAdoration");
}
