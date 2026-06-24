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

    /// <summary>
    /// Exclusive category flag: true = a theme background (shown only in the Backgrounds
    /// library + theme picker), false = general media projectable as a slide. A file is one
    /// or the other, never both; dedup by <see cref="ContentHash"/> is scoped per-category.
    /// </summary>
    public bool IsBackground { get; set; }
}
