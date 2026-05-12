using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class Song : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Classification { get; set; }

    public List<SongSection> Sections { get; set; } = new();

    public IReadOnlyList<SongSection> GetOrderedSections() =>
        Sections.OrderBy(s => s.Order).ToList();
}
