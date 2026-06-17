using Microsoft.Extensions.Logging;
using OpenAdoration.Plugins.Abstractions;

namespace OpenAdoration.WPF.Plugins;

/// <summary>The narrow host surface handed to a plugin: its settings + a logger.</summary>
internal sealed class PluginHost : IPluginHost
{
    public PluginHost(IReadOnlyDictionary<string, string> settings, ILogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public IReadOnlyDictionary<string, string> Settings { get; }
    public ILogger Logger { get; }
}
