namespace OpenAdoration.Application.Common;

/// <summary>
/// Metadata bag passed to ITokenResolver so header/footer templates can be resolved
/// against the currently displayed slide. Add fields here as new token categories
/// are supported (e.g. songbook fields, CCLI number).
/// </summary>
public sealed class SlideContext
{
    // Song tokens
    public string? SongTitle  { get; init; }
    public string? SongAuthor { get; init; }
    public string? SongVerseTag { get; init; }   // e.g. "Verse 1", "Chorus"

    // Bible tokens
    public string? BibleBookName    { get; init; }
    public string? BibleChapterId   { get; init; }
    public string? BibleVerseId     { get; init; }
    public string? BibleReference   { get; init; } // e.g. "John 3:16" or "John 3:16-18"
    public string? BibleDescription { get; init; } // Bible version name (e.g. "King James Version")

    public static SlideContext Empty { get; } = new();
}
