namespace OpenAdoration.WPF.Services;

/// <summary>
/// App-wide signal raised when a song in the library is saved/edited. Registered as a
/// singleton so it crosses DI scopes: the Songs editor raises it, and any other live
/// consumer (e.g. a service schedule projecting that song) can react — even though they
/// live in different navigation scopes and never share a <see cref="ISongService"/> instance.
/// </summary>
public interface ISongLibraryNotifier
{
    /// <summary>Raised after a song is saved; the argument is the saved song's Id.</summary>
    event EventHandler<int>? SongSaved;

    /// <summary>Raises <see cref="SongSaved"/> for <paramref name="songId"/>.</summary>
    void NotifySongSaved(int songId);
}

public sealed class SongLibraryNotifier : ISongLibraryNotifier
{
    public event EventHandler<int>? SongSaved;

    public void NotifySongSaved(int songId) => SongSaved?.Invoke(this, songId);
}
