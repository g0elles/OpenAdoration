using System.IO;
using System.IO.Compression;
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
    private static readonly string DefaultRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAdoration", "plugins");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly Version _appVersion;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginManager> _logger;
    private readonly List<LoadedPlugin> _loaded = [];

    /// <summary>Where plugins live. Defaults to <c>%LOCALAPPDATA%\OpenAdoration\plugins</c>; overridable for tests.</summary>
    public string Root { get; }

    public PluginManager(Version appVersion, ILoggerFactory loggerFactory, ILogger<PluginManager> logger, string? pluginsRoot = null)
    {
        _appVersion = appVersion;
        _loggerFactory = loggerFactory;
        _logger = logger;
        Root = pluginsRoot ?? DefaultRoot;
    }

    public IReadOnlyList<LoadedPlugin> Loaded => _loaded;

    public void LoadAll() => LoadFrom(Root);

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
        // LoadFromStream copies the PE image during the call, so the stream is done after it returns.
        using var peStream = new MemoryStream(File.ReadAllBytes(assemblyPath));
        var assembly = new PluginLoadContext(assemblyPath).LoadFromStream(peStream);

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

    /// <summary>Extracts an <c>.oaplugin</c> into the plugins dir and loads it live.</summary>
    public LoadedPlugin Install(string oapluginPath)
    {
        using var zip = ZipFile.OpenRead(oapluginPath);
        var manifestEntry = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException("Not a plugin: manifest.json is missing.");
        PluginManifest manifest;
        using (var s = manifestEntry.Open())
            manifest = JsonSerializer.Deserialize<PluginManifest>(s, JsonOpts)
                       ?? throw new InvalidDataException("Invalid manifest.json.");

        var dir = Path.Combine(Root, manifest.Id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); // reinstall / upgrade
        Directory.CreateDirectory(dir);

        foreach (var entry in zip.Entries)
        {
            if (entry.Name.Length == 0) continue; // directory entry
            if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > MaxCompressionRatio)
                throw new InvalidDataException($"Plugin entry '{entry.FullName}' has a suspicious compression ratio.");
            entry.ExtractToFile(SafeCombine(dir, entry.FullName), overwrite: true);
        }

        var loaded = LoadPlugin(dir) ?? throw new InvalidDataException("Plugin failed to load after install.");
        _loaded.RemoveAll(p => p.Manifest.Id == manifest.Id);
        _loaded.Add(loaded);
        return loaded;
    }

    /// <summary>Deletes a plugin's files and drops it from the loaded set (full unload on restart).</summary>
    public void Remove(string id)
    {
        var dir = Path.Combine(Root, id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        _loaded.RemoveAll(p => p.Manifest.Id == id);
    }

    /// <summary>Current persisted settings for a plugin (empty if none saved yet).</summary>
    public IReadOnlyDictionary<string, string> GetSettings(string id) => LoadSettings(Path.Combine(Root, id));

    /// <summary>Persists a plugin's settings and re-initializes it so they take effect immediately.</summary>
    public void UpdateSettings(string id, IReadOnlyDictionary<string, string> settings)
    {
        var dir = Path.Combine(Root, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), JsonSerializer.Serialize(settings, JsonOpts));

        if (_loaded.FirstOrDefault(p => p.Manifest.Id == id) is { } plugin)
            plugin.Instance.Initialize(new PluginHost(settings, _loggerFactory.CreateLogger($"Plugin.{id}")));
    }

    private const long MaxCompressionRatio = 50;

    private static string SafeCombine(string root, string relative)
    {
        var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Plugin entry escapes its directory: '{relative}'.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return full;
    }
}
