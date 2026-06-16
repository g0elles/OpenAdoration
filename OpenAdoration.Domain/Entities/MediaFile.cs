using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.Domain.Entities;

public class MediaFile : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaType Type { get; set; }

    /// <summary>
    /// Hash of the file's bytes (SHA-256 hex), used to dedup identical media extracted
    /// from multiple import sources. Null for files imported before hashing existed.
    /// </summary>
    public string? ContentHash { get; set; }
}
