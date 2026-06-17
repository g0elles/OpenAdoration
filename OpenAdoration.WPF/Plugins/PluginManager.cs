using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAdoration.Plugins.Abstractions;

namespace OpenAdoration.WPF.Plugins;

/// <summary>
/// Discovers installed plugins under <c>%LOCALAPPDATA%\OpenAdoration\plugins\&lt;id&gt;\</c>,
/// gates them on <c>minOaVersion</c>, and loads each in a collectible context. A failed or
/// incompatible plugin is logged and skipped — it never blocks the others or app startup.
/// </summary>
public sealed class PluginManager
{
    public static readonly string PluginsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAdoration", "plugins");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly Version _appVersion;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginManager> _logger;
    private readonly List<LoadedPlugin> _loaded = [];

    public PluginManager(Version appVersion, ILoggerFactory loggerFactory, ILogger<PluginManager> logger)
    {
        _appVersion = appVersion;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public IReadOnlyList<LoadedPlugin> Loaded => _loaded;

    public void LoadAll() => LoadFrom(PluginsRoot);

    /// <summary>Loads every plugin under <paramref name="root"/>. Exposed for tests.</summary>
    public IReadOnlyList<LoadedPlugin> LoadFrom(string root)
    {
        _loaded.Clear();
        if (!Directory.Exists(root)) return _loaded;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (LoadPlugin(dir) is { } plugin) _loaded.Add(plugin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Dir}", dir);
            }
        }
        return _loaded;
    }

    private LoadedPlugin? LoadPlugin(string dir)
    {
        var manifestPath = Path.Combine(dir, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), JsonOpts)
            ?? throw new InvalidDataException("manifest.json is empty or invalid.");

        if (Version.TryParse(manifest.MinOaVersion, out var min) && min > _appVersion)
        {
            _logger.LogWarning(
                "Plugin {Id} requires OpenAdoration >= {Min} (running {App}) — skipped.",
                manifest.Id, manifest.MinOaVersion, _appVersion);
            return null;
        }

        var assemblyPath = Path.Combine(dir, manifest.EntryAssembly);
        // Load from bytes, not the path: LoadFromAssemblyPath would lock the DLL on disk,
        // which blocks removing the plugin (the file stays open until process exit). Deps
        // still resolve via the resolver built from the path inside PluginLoadContext.
        var assembly = new PluginLoadContext(assemblyPath).LoadFromStream(new MemoryStream(File.ReadAllBytes(assemblyPath)));

        var type = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            ?? throw new InvalidDataException($"No IPlugin implementation in {manifest.EntryAssembly}.");

        var instance = (IPlugin)Activator.CreateInstance(type)!;
        instance.Initialize(new PluginHost(LoadSettings(dir), _loggerFactory.CreateLogger($"Plugin.{manifest.Id}")));

        _logger.LogInformation("Loaded plugin {Id} v{Version} ({Capability})", manifest.Id, manifest.Version, manifest.Capability);
        return new LoadedPlugin { Manifest = manifest, Instance = instance };
    }

    private static IReadOnlyDictionary<string, string> LoadSettings(string dir)
    {
        // ponytail: plaintext per-plugin settings (incl. API keys) for v1; DPAPI is a later hardening.
        var path = Path.Combine(dir, "settings.json");
        if (!File.Exists(path)) return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), JsonOpts)
               ?? new Dictionary<string, string>();
    }
}
