using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.Domain.Entities;

public class SongSection : BaseEntity
{
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    public SectionType Type { get; set; }

    // Tracks repetition within the same type (e.g. Verse 1, Verse 2)
    public int SectionNumber { get; set; } = 1;

    public string Lyrics { get; set; } = string.Empty;

    // Display position within the song
    public int Order { get; set; }

    public string Label => Type switch
    {
        SectionType.Verse => $"Verse {SectionNumber}",
        SectionType.Bridge => $"Bridge {SectionNumber}",
        _ => Type.ToString()
    };
}
