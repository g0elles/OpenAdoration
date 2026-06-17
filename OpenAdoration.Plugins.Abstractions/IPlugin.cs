namespace OpenAdoration.Plugins.Abstractions;

/// <summary>
/// Base contract every OpenAdoration plugin implements. A plugin is a separately distributed
/// <c>.oaplugin</c> assembly loaded into the host (in-process, full trust); capability
/// interfaces such as <see cref="IBibleSourcePlugin"/> describe what a given plugin can do.
/// </summary>
public interface IPlugin
{
    /// <summary>Stable unique id (matches the manifest <c>id</c>), e.g. "apibible".</summary>
    string Id { get; }

    string Name { get; }

    Version Version { get; }

    /// <summary>Called once after the plugin is loaded, with the host-provided settings + logger.</summary>
    void Initialize(IPluginHost host);
}
