namespace OpenAdoration.WPF.Plugins;

/// <summary>
/// The <c>manifest.json</c> shipped inside every <c>.oaplugin</c>. Authored by plugin devs;
/// read by <see cref="PluginManager"/> to gate and load the plugin.
/// </summary>
public sealed record PluginManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Capability { get; init; }
    public required string MinOaVersion { get; init; }
    public required string EntryAssembly { get; init; }
    public IReadOnlyList<PluginSettingDef> Settings { get; init; } = [];
}

/// <summary>A setting the plugin needs the operator to fill in (e.g. an API key).</summary>
public sealed record PluginSettingDef(string Key, string Label, bool Secret = false);
