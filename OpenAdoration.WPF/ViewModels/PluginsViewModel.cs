using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
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
    private readonly IBibleService _bibleService;
    private readonly IDialogService _dialog;
    private readonly ILogger<PluginsViewModel> _logger;

    [ObservableProperty] private ObservableCollection<PluginRow> _installedPlugins = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedIsBibleSource))]
    private PluginRow? _selectedPlugin;

    [ObservableProperty] private ObservableCollection<SettingRow> _settings = [];
    [ObservableProperty] private string _versionFilter = string.Empty;
    [ObservableProperty] private string _importStatus = string.Empty;

    private readonly ObservableCollection<PluginVersionRow> _allVersions = [];

    /// <summary>Fetched versions, grouped by language and live-filtered for the picker.</summary>
    public ICollectionView VersionsView { get; }

    public bool HasSelection => SelectedPlugin is not null;
    public bool SelectedIsBibleSource => SelectedPlugin?.IsBibleSource == true;

    public PluginsViewModel(
        PluginManager manager, PluginBibleImporter bibleImporter, IBibleService bibleService,
        IDialogService dialog, ILogger<PluginsViewModel> logger)
    {
        _manager = manager;
        _bibleImporter = bibleImporter;
        _bibleService = bibleService;
        _dialog = dialog;
        _logger = logger;

        VersionsView = CollectionViewSource.GetDefaultView(_allVersions);
        VersionsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PluginVersionRow.Language)));
        VersionsView.SortDescriptions.Add(new SortDescription(nameof(PluginVersionRow.Language), ListSortDirection.Ascending));
        VersionsView.SortDescriptions.Add(new SortDescription(nameof(PluginVersionRow.Name), ListSortDirection.Ascending));
        VersionsView.Filter = MatchesFilter;
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
            Title = L("Plugins_AddTitle"),
            Filter = L("Plugins_Filter") + "|*.oaplugin",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true || IsBusy) return;

        IsBusy = true;
        ClearError();
        try { _manager.Install(dialog.FileName); RefreshList(); _dialog.Inform(L("Plugins_Installed"), L("Plugins_Title")); }
        catch (Exception ex) { _logger.LogError(ex, "Plugin install failed"); SetError(L("Plugins_ErrInstall")); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemovePluginAsync()
    {
        if (SelectedPlugin is null || IsBusy) return;
        var pluginId = SelectedPlugin.Id;

        // Bibles this plugin downloaded are deleted with it, so licensed content never outlives the
        // plugin/licence that provided it. Warn with the count when there are any.
        var owned = (await _bibleService.GetVersionsAsync()).Count(v => v.SourcePluginId == pluginId);
        var confirmed = owned > 0
            ? _dialog.Confirm(L("Plugins_ConfirmRemoveWithBibles", SelectedPlugin.Name, owned), L("Plugins_RemoveTitle"))
            : _dialog.Confirm(L("Plugins_ConfirmRemove", SelectedPlugin.Name), L("Plugins_RemoveTitle"));
        if (!confirmed) return;

        IsBusy = true;
        ClearError();
        try
        {
            if (owned > 0) await _bibleService.DeleteVersionsBySourceAsync(pluginId);
            _manager.Remove(pluginId);
            RefreshList();
        }
        catch (Exception ex) { _logger.LogError(ex, "Plugin remove failed"); SetError(L("Plugins_ErrRemove")); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (SelectedPlugin is null) return;
        try
        {
            _manager.UpdateSettings(SelectedPlugin.Id, Settings.ToDictionary(s => s.Key, s => s.Value));
            _dialog.Inform(L("Plugins_SettingsSaved"), L("Plugins_Title"));
        }
        catch (Exception ex) { _logger.LogError(ex, "Save plugin settings failed"); SetError(L("Settings_ErrSave")); }
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
            // Flag versions already in the library (matched by abbreviation, the same key the upsert
            // merges on) so operators don't spend API quota re-downloading them.
            var existing = (await _bibleService.GetVersionsAsync())
                .Select(x => x.Abbreviation)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _allVersions.Clear();
            foreach (var v in versions)
                _allVersions.Add(new PluginVersionRow(v.Id, v.Name, v.Abbreviation, v.Language)
                {
                    AlreadyDownloaded = existing.Contains(v.Abbreviation)
                });
            VersionFilter = string.Empty;
            VersionsView.Refresh();
        }
        catch (Exception ex) { _logger.LogError(ex, "Fetch versions failed"); SetError(L("Plugins_ErrFetch")); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ImportSelectedAsync(CancellationToken ct)
    {
        if (SelectedPlugin?.Source.Instance is not IBibleSourcePlugin bible || IsBusy) return;
        var selected = _allVersions.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0) return;

        IsBusy = true;
        ClearError();
        // Progress<int> marshals back to the UI thread; the plugin reports cumulative verses fetched.
        var progress = new Progress<int>(n => ImportStatus = L("Plugins_ImportProgress", n));
        try
        {
            foreach (var v in selected)
            {
                ImportStatus = L("Plugins_ImportStarting", v.Name);
                _logger.LogInformation("Importing Bible version {Name} ({Id}) via plugin {Plugin}",
                    v.Name, v.Id, SelectedPlugin!.Id);
                await _bibleImporter.ImportAsync(bible, v.Id, progress, ct);
                v.AlreadyDownloaded = true;   // reflect the new library state in the picker
                v.IsSelected = false;
            }
            _dialog.Inform(L("Plugins_ImportedCount", selected.Count), L("Plugins_Title"));
        }
        catch (OperationCanceledException) { /* operator cancelled — no error banner */ }
        catch (Exception ex) { _logger.LogError(ex, "Plugin Bible import failed"); SetError(L("Plugins_ErrImport")); }
        finally { IsBusy = false; ImportStatus = string.Empty; }
    }

    private bool MatchesFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(VersionFilter)) return true;
        var r = (PluginVersionRow)item;
        var q = VersionFilter.Trim();
        return r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || r.Abbreviation.Contains(q, StringComparison.OrdinalIgnoreCase)
            || r.Language.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnVersionFilterChanged(string value) => VersionsView.Refresh();

    partial void OnSelectedPluginChanged(PluginRow? value)
    {
        _allVersions.Clear();
        VersionFilter = string.Empty;
        ImportStatus = string.Empty;
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

public partial class PluginVersionRow(string id, string name, string abbreviation, string language) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Abbreviation { get; } = abbreviation;
    public string Language { get; } = language;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _alreadyDownloaded;
}
