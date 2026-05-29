using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public abstract class ScheduleItem : BaseEntity
{
    public int ServiceId { get; set; }
    public WorshipService Service { get; set; } = null!;

    public int Order { get; set; }

    /// <summary>
    /// Seconds between automatic slide advances. Null or 0 = manual control.
    /// When the last slide of the item is reached the timer advances to the next schedule item.
    /// </summary>
    public int? AutoAdvanceSeconds { get; set; }

    public int? ThemeId { get; set; }
    public Theme? Theme { get; set; }
}
