using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.Domain.Entities;

public class BibleBook : BaseEntity
{
    public int BibleVersionId { get; set; }
    public BibleVersion BibleVersion { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public Testament Testament { get; set; }

    // Canonical position (Genesis = 1, Revelation = 66)
    public int BookNumber { get; set; }
    public int ChapterCount { get; set; }
}
