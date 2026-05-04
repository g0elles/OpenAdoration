namespace OpenAdoration.Domain.Entities;

public class BibleScheduleItem : ScheduleItem
{
    public string Book { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int VerseStart { get; set; }
    public int VerseEnd { get; set; }

    public int? BibleVersionId { get; set; }
    public BibleVersion? BibleVersion { get; set; }

    public string Reference => VerseStart == VerseEnd
        ? $"{Book} {Chapter}:{VerseStart}"
        : $"{Book} {Chapter}:{VerseStart}-{VerseEnd}";
}
