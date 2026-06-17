using Microsoft.Extensions.Logging;

namespace OpenAdoration.Plugins.Abstractions;

/// <summary>
/// The minimal surface the host hands a plugin on <see cref="IPlugin.Initialize"/>: its
/// persisted settings (keyed by the manifest's setting keys — e.g. an API key) and a logger.
/// Intentionally narrow — no database, filesystem, or app-service access.
/// </summary>
public interface IPluginHost
{
    IReadOnlyDictionary<string, string> Settings { get; }

    ILogger Logger { get; }
}
