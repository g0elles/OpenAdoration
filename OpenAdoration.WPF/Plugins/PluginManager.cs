using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    // S5: per-plugin settings (incl. bring-your-own-key API keys) are DPAPI-protected at rest,
    // keyed to the current Windows user, so they aren't readable as plaintext on disk/backups.
    // ProtectedData ships in the net10.0-windows framework — no package reference needed.
    private const string SettingsFile = "settings.dat";
    private static readonly byte[] SettingsEntropy = "OpenAdoration.Plugins.Settings"u8.ToArray();

    private static IReadOnlyDictionary<string, string> LoadSettings(string dir)
    {
        var path = Path.Combine(dir, SettingsFile);
        if (!File.Exists(path)) return new Dictionary<string, string>();
        var json = ProtectedData.Unprotect(File.ReadAllBytes(path), SettingsEntropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts)
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

        // P4: bound the total uncompressed payload before extracting — LoadPlugin reads the entry
        // assembly fully into memory, and a huge bundle would balloon disk + startup allocations.
        var totalBytes = zip.Entries.Sum(e => e.Length);
        if (totalBytes > MaxPluginTotalBytes)
            throw new InvalidDataException($"Plugin exceeds the {MaxPluginTotalBytes / (1024 * 1024)} MB size limit.");

        var dir = PluginDir(manifest.Id);
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
        var dir = PluginDir(id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        _loaded.RemoveAll(p => p.Manifest.Id == id);
    }

    /// <summary>Current persisted settings for a plugin (empty if none saved yet).</summary>
    public IReadOnlyDictionary<string, string> GetSettings(string id) => LoadSettings(PluginDir(id));

    /// <summary>Persists a plugin's settings and re-initializes it so they take effect immediately.</summary>
    public void UpdateSettings(string id, IReadOnlyDictionary<string, string> settings)
    {
        var dir = PluginDir(id);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOpts);
        var encrypted = ProtectedData.Protect(json, SettingsEntropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Path.Combine(dir, SettingsFile), encrypted);

        if (_loaded.FirstOrDefault(p => p.Manifest.Id == id) is { } plugin)
            plugin.Instance.Initialize(new PluginHost(settings, _loggerFactory.CreateLogger($"Plugin.{id}")));
    }

    private const long MaxCompressionRatio = 50;
    private const long MaxPluginTotalBytes = 100L * 1024 * 1024; // 100 MB uncompressed (P4)

    // Plugin id comes from an untrusted .oaplugin manifest and is used as a directory name.
    // Restrict it to a narrow identifier grammar so it can't carry separators, drive roots, or
    // ".." traversal (first char excludes '.'), then bound the resolved path to Root.
    private static readonly Regex IdPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.Compiled);

    private string PluginDir(string id)
    {
        if (string.IsNullOrEmpty(id) || !IdPattern.IsMatch(id))
            throw new InvalidDataException($"Invalid plugin id: '{id}'.");

        var rootFull = Path.GetFullPath(Root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(Root, id));
        if (!(full + Path.DirectorySeparatorChar).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Plugin id escapes the plugins root: '{id}'.");
        return full;
    }

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
