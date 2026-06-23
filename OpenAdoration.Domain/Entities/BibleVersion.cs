using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class BibleVersion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Id of the plugin that downloaded this version (null for built-in/manual imports). Lets the
    /// app remove a plugin's downloaded Bibles when the plugin is uninstalled — required so licensed
    /// content does not outlive the plugin/licence that provided it.
    /// </summary>
    public string? SourcePluginId { get; set; }

    public List<BibleBook> Books { get; set; } = new();
}
