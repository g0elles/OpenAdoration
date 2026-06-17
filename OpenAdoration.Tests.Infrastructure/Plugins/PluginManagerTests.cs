using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Plugins.Abstractions;
using OpenAdoration.WPF.Plugins;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Plugins;

/// <summary>
/// M13.2: discovery + collectible-ALC loading + the minOaVersion gate, exercised against the
/// real sample plugin assembly copied into a temp plugins directory.
/// </summary>
public sealed class PluginManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oa-plugins-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best-effort temp cleanup */ }
        catch (UnauthorizedAccessException) { /* best-effort temp cleanup */ }
    }

    private PluginManager Manager(Version appVersion) =>
        new(appVersion, NullLoggerFactory.Instance, NullLogger<PluginManager>.Instance);

    private string InstallSample(string id, string minOaVersion)
    {
        var dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "OpenAdoration.Plugins.Sample.dll"),
            Path.Combine(dir, "OpenAdoration.Plugins.Sample.dll"));
        File.WriteAllText(Path.Combine(dir, "manifest.json"), $$"""
            {
              "id": "{{id}}",
              "name": "Sample Echo",
              "version": "1.0.0",
              "capability": "bible-source",
              "minOaVersion": "{{minOaVersion}}",
              "entryAssembly": "OpenAdoration.Plugins.Sample.dll"
            }
            """);
        return dir;
    }

    [Fact]
    public async Task LoadsCompatiblePlugin_AndItsCapabilityWorks()
    {
        InstallSample("sample.echo", minOaVersion: "1.0.0");

        var loaded = Manager(new Version(1, 1, 0)).LoadFrom(_root);

        var plugin = Assert.Single(loaded);
        Assert.Equal("sample.echo", plugin.Manifest.Id);
        var bible = Assert.IsAssignableFrom<IBibleSourcePlugin>(plugin.Instance);
        var data = await bible.FetchAsync("echo");
        Assert.Equal("ECHO", data.Version.Abbreviation);
    }

    [Fact]
    public void SkipsPlugin_RequiringNewerApp()
    {
        InstallSample("future.plugin", minOaVersion: "999.0.0");

        var loaded = Manager(new Version(1, 1, 0)).LoadFrom(_root);

        Assert.Empty(loaded);
    }

    [Fact]
    public void SkipsDirectory_WithoutManifest_WithoutThrowing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "not-a-plugin"));

        var loaded = Manager(new Version(1, 1, 0)).LoadFrom(_root);

        Assert.Empty(loaded);
    }

    [Fact]
    public void InstallThenRemove_RoundTrips()
    {
        var oaplugin = BuildOaplugin("sample.echo");
        var installRoot = Path.Combine(_root, "installed");
        var manager = new PluginManager(new Version(1, 1, 0), NullLoggerFactory.Instance, NullLogger<PluginManager>.Instance, installRoot);

        var loaded = manager.Install(oaplugin);
        Assert.Equal("sample.echo", loaded.Manifest.Id);
        Assert.Single(manager.Loaded);
        Assert.True(Directory.Exists(Path.Combine(installRoot, "sample.echo")));

        manager.Remove("sample.echo");
        Assert.Empty(manager.Loaded);
        Assert.False(Directory.Exists(Path.Combine(installRoot, "sample.echo")));
    }

    [Fact]
    public void UpdateSettings_PersistsAndIsReadBack()
    {
        var installRoot = Path.Combine(_root, "installed");
        var manager = new PluginManager(new Version(1, 1, 0), NullLoggerFactory.Instance, NullLogger<PluginManager>.Instance, installRoot);
        manager.Install(BuildOaplugin("sample.echo"));

        manager.UpdateSettings("sample.echo", new Dictionary<string, string> { ["apiKey"] = "secret123" });

        Assert.Equal("secret123", manager.GetSettings("sample.echo")["apiKey"]);
    }

    private string BuildOaplugin(string id)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, id + ".oaplugin");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
            w.Write($$"""
                {"id":"{{id}}","name":"Sample Echo","version":"1.0.0","capability":"bible-source","minOaVersion":"1.0.0","entryAssembly":"OpenAdoration.Plugins.Sample.dll"}
                """);
        zip.CreateEntryFromFile(
            Path.Combine(AppContext.BaseDirectory, "OpenAdoration.Plugins.Sample.dll"), "OpenAdoration.Plugins.Sample.dll");
        return path;
    }
}
