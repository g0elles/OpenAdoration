using System.Reflection;
using System.Runtime.Loader;

namespace OpenAdoration.WPF.Plugins;

/// <summary>
/// Collectible load context for one plugin. Assemblies already in the host (notably
/// <c>OpenAdoration.Plugins.Abstractions</c> and its deps) resolve from the default context
/// so <c>IPlugin</c>/<c>IBibleSourcePlugin</c> are the same <see cref="Type"/> across the
/// boundary; the plugin's own assemblies load in isolation here.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string mainAssemblyPath)
        : base(isCollectible: true) => _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share anything the host already loaded (contract + framework) — return null to defer
        // to the default context, keeping types identical across host/plugin.
        if (Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
            return null;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}
