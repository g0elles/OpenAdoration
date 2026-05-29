namespace OpenAdoration.Domain.Entities;

public class SongScheduleItem : ScheduleItem
{
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    /// <summary>
    /// Per-service section order, overriding <see cref="Song.VerseOrder"/> for this agenda item only.
    /// Same token syntax as <see cref="Song.VerseOrder"/> (e.g. "V1 C V2 C B C").
    /// Null/empty = fall back to the song's own order.
    /// </summary>
    public string? VerseOrderOverride { get; set; }
}
