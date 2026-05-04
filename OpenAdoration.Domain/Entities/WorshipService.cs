using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class WorshipService : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    public List<ScheduleItem> Items { get; set; } = new();

    public IReadOnlyList<ScheduleItem> GetOrderedItems() =>
        Items.OrderBy(i => i.Order).ToList();
}
