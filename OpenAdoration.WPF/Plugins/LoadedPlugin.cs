using OpenAdoration.Plugins.Abstractions;

namespace OpenAdoration.WPF.Plugins;

/// <summary>A discovered, loaded, and initialized plugin paired with its manifest.</summary>
public sealed class LoadedPlugin
{
    public required PluginManifest Manifest { get; init; }
    public required IPlugin Instance { get; init; }
}
