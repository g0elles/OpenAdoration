using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class Note : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Content-level theme (M14 cascade). When set, this note carries its own look whether
    /// projected standalone or inside a service. Null = inherit from the cascade
    /// (schedule-item theme → this → Notes content-type default).
    /// </summary>
    public int? ThemeId { get; set; }
}
