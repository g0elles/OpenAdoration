using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class BibleVerse : BaseEntity
{
    public int BibleVersionId { get; set; }
    public BibleVersion BibleVersion { get; set; } = null!;

    public string Book { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Text { get; set; } = string.Empty;

    public string Reference => $"{Book} {Chapter}:{Verse}";
}
