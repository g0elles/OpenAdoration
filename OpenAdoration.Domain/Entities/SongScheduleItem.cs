namespace OpenAdoration.Domain.Entities;

public class SongScheduleItem : ScheduleItem
{
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
}
