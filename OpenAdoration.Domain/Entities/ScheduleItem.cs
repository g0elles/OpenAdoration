using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public abstract class ScheduleItem : BaseEntity
{
    public int ServiceId { get; set; }
    public WorshipService Service { get; set; } = null!;

    public int Order { get; set; }

    public int? ThemeId { get; set; }
    public Theme? Theme { get; set; }
}
