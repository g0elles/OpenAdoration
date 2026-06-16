using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class WorshipService : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    /// <summary>
    /// Stable identity of the import source (VideoPsalm agenda) so re-importing the same
    /// <c>.vpagd</c> is detected and skipped/refreshed rather than duplicated. Null for
    /// services created in-app.
    /// </summary>
    public string? SourceGuid { get; set; }

    /// <summary>Path to the retained original <c>.vpagd</c> this service was imported from.</summary>
    public string? SourceArchivePath { get; set; }

    public List<ScheduleItem> Items { get; set; } = new();

    public IReadOnlyList<ScheduleItem> GetOrderedItems() =>
        Items.OrderBy(i => i.Order).ToList();
}
