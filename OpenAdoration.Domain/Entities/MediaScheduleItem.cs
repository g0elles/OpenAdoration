namespace OpenAdoration.Domain.Entities;

public class MediaScheduleItem : ScheduleItem
{
    public int MediaFileId { get; set; }
    public MediaFile MediaFile { get; set; } = null!;
}
