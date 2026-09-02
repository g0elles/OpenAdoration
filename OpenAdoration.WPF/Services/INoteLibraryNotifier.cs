namespace OpenAdoration.WPF.Services;

/// <summary>
/// App-wide signal raised when a note in the library is saved/edited. Registered as a
/// singleton so it crosses DI scopes: the Notes editor raises it, and any other live
/// consumer (e.g. a service schedule projecting that note) can react. Mirrors
/// <see cref="ISongLibraryNotifier"/>.
/// </summary>
public interface INoteLibraryNotifier
{
    /// <summary>Raised after a note is saved; the argument is the saved note's Id.</summary>
    event EventHandler<int>? NoteSaved;

    /// <summary>Raises <see cref="NoteSaved"/> for <paramref name="noteId"/>.</summary>
    void NotifyNoteSaved(int noteId);
}

public sealed class NoteLibraryNotifier : INoteLibraryNotifier
{
    public event EventHandler<int>? NoteSaved;

    public void NotifyNoteSaved(int noteId) => NoteSaved?.Invoke(this, noteId);
}
