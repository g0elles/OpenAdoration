using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.Domain.Entities;

public class MediaFile : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaType Type { get; set; }
}
