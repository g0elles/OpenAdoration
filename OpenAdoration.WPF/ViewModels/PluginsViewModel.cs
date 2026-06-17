using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Plugins.Abstractions;
using OpenAdoration.WPF.Plugins;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

/// <summary>
/// Settings → Plugins page: list installed plugins, add (<c>.oaplugin</c>) / remove, edit a
/// plugin's settings (e.g. an API key), and for a Bible-source plugin fetch + import a version.
/// </summary>
public partial class PluginsViewModel : BaseViewModel
{
    private readonly PluginManager _manager;
    private readonly PluginBibleImporter _bibleImporter;
    private readonly IDialogService _dialog;
    private readonly ILogger<PluginsViewModel> _logger;

    [ObservableProperty] private ObservableCollection<PluginRow> _installedPlugins = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedIsBibleSource))]
    private PluginRow? _selectedPlugin;

    [ObservableProperty] private ObservableCollection<SettingRow> _settings = [];
    [ObservableProperty] private ObservableCollection<PluginVersionRow> _availableVersions = [];
    [ObservableProperty] private PluginVersionRow? _selectedVersion;

    public bool HasSelection => SelectedPlugin is not null;
    public bool SelectedIsBibleSource => SelectedPlugin?.IsBibleSource == true;

    public PluginsViewModel(
        PluginManager manager, PluginBibleImporter bibleImporter, IDialogService dialog, ILogger<PluginsViewModel> logger)
    {
        _manager = manager;
        _bibleImporter = bibleImporter;
        _dialog = dialog;
        _logger = logger;
    }

    [RelayCommand]
    private void Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try { RefreshList(); }
        finally { IsBusy = false; }
    }

    private void RefreshList() => InstalledPlugins = new(_manager.Loaded.Select(p => new PluginRow(p)));

    [RelayCommand]
    private void AddPlugin()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add plugin",
            Filter = "OpenAdoration plugin (*.oaplugin)|*.oaplugin",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true || IsBusy) return;

        IsBusy = true;
        ClearError();
        try { _manager.Install(dialog.FileName); RefreshList(); _dialog.Inform("Plugin installed.", "Plugins"); }
        catch (Exception ex) { _logger.LogError(ex, "Plugin install failed"); SetError("Could not install the plugin."); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void RemovePlugin()
    {
        if (SelectedPlugin is null || IsBusy) return;
        if (!_dialog.Confirm($"Remove plugin '{SelectedPlugin.Name}'? It is fully unloaded after a restart.", "Remove Plugin"))
            return;

        IsBusy = true;
        ClearError();
        try { _manager.Remove(SelectedPlugin.Id); RefreshList(); }
        catch (Exception ex) { _logger.LogError(ex, "Plugin remove failed"); SetError("Could not remove the plugin."); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (SelectedPlugin is null) return;
        try
        {
            _manager.UpdateSettings(SelectedPlugin.Id, Settings.ToDictionary(s => s.Key, s => s.Value));
            _dialog.Inform("Settings saved.", "Plugins");
        }
        catch (Exception ex) { _logger.LogError(ex, "Save plugin settings failed"); SetError("Could not save settings."); }
    }

    [RelayCommand]
    private async Task FetchVersionsAsync()
    {
        if (SelectedPlugin?.Source.Instance is not IBibleSourcePlugin bible || IsBusy) return;

        IsBusy = true;
        ClearError();
        try
        {
            var versions = await bible.GetAvailableVersionsAsync();
            AvailableVersions = new(versions.Select(v => new PluginVersionRow(v.Id, v.Name, v.Abbreviation, v.Language)));
        }
        catch (Exception ex) { _logger.LogError(ex, "Fetch versions failed"); SetError("Could not fetch versions from the plugin."); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ImportVersionAsync()
    {
        if (SelectedPlugin?.Source.Instance is not IBibleSourcePlugin bible || SelectedVersion is null || IsBusy) return;

        IsBusy = true;
        ClearError();
        try
        {
            await _bibleImporter.ImportAsync(bible, SelectedVersion.Id);
            _dialog.Inform($"Imported '{SelectedVersion.Name}' into your Bible library.", "Plugins");
        }
        catch (Exception ex) { _logger.LogError(ex, "Plugin Bible import failed"); SetError("Could not import the version."); }
        finally { IsBusy = false; }
    }

    partial void OnSelectedPluginChanged(PluginRow? value)
    {
        AvailableVersions = [];
        SelectedVersion = null;
        if (value is null) { Settings = []; return; }

        var current = _manager.GetSettings(value.Id);
        Settings = new(value.Source.Manifest.Settings.Select(d =>
            new SettingRow(d.Key, d.Label, d.Secret) { Value = current.GetValueOrDefault(d.Key, string.Empty) }));
    }
}

public sealed class PluginRow(LoadedPlugin source)
{
    public LoadedPlugin Source { get; } = source;
    public string Id => Source.Manifest.Id;
    public string Name => Source.Manifest.Name;
    public string Version => Source.Manifest.Version;
    public string Capability => Source.Manifest.Capability;
    public bool IsBibleSource => Capability == PluginCapabilities.BibleSource;
}

public partial class SettingRow(string key, string label, bool secret) : ObservableObject
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool Secret { get; } = secret;
    [ObservableProperty] private string _value = string.Empty;
}

public sealed record PluginVersionRow(string Id, string Name, string Abbreviation, string Language);
