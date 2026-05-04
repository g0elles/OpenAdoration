using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class BibleVersion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;

    public List<BibleBook> Books { get; set; } = new();
}
