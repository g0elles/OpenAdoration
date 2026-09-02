namespace OpenAdoration.Domain.Entities;

public class NotesScheduleItem : ScheduleItem
{
    public int NoteId { get; set; }
    public Note Note { get; set; } = null!;
}
