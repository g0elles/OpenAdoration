using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers.VideoPsalmMigration;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class ServiceScheduleViewModel : BaseViewModel, IDisposable
{
    private readonly IWorshipServiceService      _serviceService;
    private readonly ISongService                _songService;
    private readonly IBibleService               _bibleService;
    private readonly IMediaService               _mediaService;
    private readonly IProjectionService          _projectionService;
    private readonly IDialogService              _dialogService;
    private readonly IAppSettingsService         _appSettings;
    private readonly ISongLibraryNotifier        _songNotifier;
    private readonly VideoPsalmServiceImporter   _vpImporter;
    private readonly ILogger<ServiceScheduleViewModel> _logger;

    // ── Service list ─────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<WorshipService> _services = [];

    public bool HasNoServices      => Services.Count == 0;
    public bool HasNoScheduleItems => ScheduleItems.Count == 0;
    [ObservableProperty] private WorshipService? _selectedService;

    // Create form
    [ObservableProperty] private bool   _isCreatingService;
    [ObservableProperty] private string _newServiceName = string.Empty;
    [ObservableProperty] private DateTime _newServiceDate = DateTime.Today;

    // ── Builder ───────────────────────────────────────────────────────────────

    [ObservableProperty] private WorshipService? _openedService;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBuilding))]
    private bool _isServiceOpen;
    [ObservableProperty] private ObservableCollection<ScheduleItemViewModel> _scheduleItems = [];
    [ObservableProperty] private ScheduleItemViewModel? _selectedItem;

    // Add Song panel
    [ObservableProperty] private bool   _isAddingSong;
    [ObservableProperty] private string _songPickerSearchTerm = string.Empty;
    [ObservableProperty] private ObservableCollection<Song> _songPickerResults = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirmAddSong))]
    private Song? _pickerSelectedSong;

    public bool CanConfirmAddSong => PickerSelectedSong is not null;

    // Add Bible panel
    [ObservableProperty] private bool   _isAddingBible;
    [ObservableProperty] private ObservableCollection<BibleVersion> _bibleVersions = [];
    [ObservableProperty] private BibleVersion? _selectedAddBibleVersion;
    [ObservableProperty] private ObservableCollection<BibleBook> _addBibleBooks = [];
    [ObservableProperty] private BibleBook? _selectedAddBibleBook;
    [ObservableProperty] private ObservableCollection<int> _addBibleChapters = [];
    [ObservableProperty] private int    _selectedAddBibleChapterNumber;
    [ObservableProperty] private ObservableCollection<BibleVersePickerItem> _addBibleVerses = [];

    private int _versePickerAnchor;
    private int _versePickerEnd;

    public bool   CanConfirmAddBible       => _versePickerAnchor > 0;
    public string BiblePickerSelectionLabel
    {
        get
        {
            if (_versePickerAnchor == 0) return "Click a verse to select it, then click another to extend the range.";
            var lo = Math.Min(_versePickerAnchor, _versePickerEnd);
            var hi = Math.Max(_versePickerAnchor, _versePickerEnd);
            return lo == hi ? $"Selected: verse {lo}" : $"Selected: verses {lo} – {hi}";
        }
    }

    // Add Media panel
    [ObservableProperty] private bool _isAddingMedia;
    [ObservableProperty] private ObservableCollection<MediaFile> _mediaPickerFiles = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirmAddMedia))]
    private MediaFile? _pickerSelectedMedia;

    public bool CanConfirmAddMedia => PickerSelectedMedia is not null;

    // ── Live mode ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrevItemCommand))]
    [NotifyPropertyChangedFor(nameof(IsBuilding))]
    private bool _isLiveMode;

    public bool IsBuilding => IsServiceOpen && !IsLiveMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentLiveItemTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentLiveDisplayNumber))]
    private int _currentLiveIndex = -1;

    public string CurrentLiveItemTitle =>
        CurrentLiveIndex >= 0 && CurrentLiveIndex < ScheduleItems.Count
            ? ScheduleItems[CurrentLiveIndex].DisplayTitle
            : string.Empty;

    public int CurrentLiveDisplayNumber => CurrentLiveIndex + 1;

    public ServiceScheduleViewModel(
        IWorshipServiceService      serviceService,
        ISongService                songService,
        IBibleService               bibleService,
        IMediaService               mediaService,
        IProjectionService          projectionService,
        IDialogService              dialogService,
        IAppSettingsService         appSettings,
        ISongLibraryNotifier        songNotifier,
        VideoPsalmServiceImporter   vpImporter,
        ILogger<ServiceScheduleViewModel> logger)
    {
        _serviceService    = serviceService;
        _songService       = songService;
        _bibleService      = bibleService;
        _mediaService      = mediaService;
        _projectionService = projectionService;
        _dialogService     = dialogService;
        _appSettings       = appSettings;
        _songNotifier      = songNotifier;
        _vpImporter        = vpImporter;
        _logger            = logger;

        _projectionService.ProjectionStateChanged        += OnProjectionStateChanged;
        _projectionService.SlideChanged                  += OnSlideChangedForAutoAdvance;
        _projectionService.NextScheduleItemRequested     += OnNextItemRequested;
        _projectionService.PreviousScheduleItemRequested += OnPrevItemRequested;
        _songNotifier.SongSaved                          += OnSongLibrarySaved;
    }

    // App-wide default applied to newly added items; null = manual.
    private int? DefaultAutoAdvanceSeconds =>
        _appSettings.Current.DefaultAutoAdvanceSeconds > 0 ? _appSettings.Current.DefaultAutoAdvanceSeconds : null;

    // App-wide verses-per-slide for Bible items; minimum 1.
    private int BibleVersesPerSlide => Math.Max(1, _appSettings.Current.DefaultBibleVersesPerSlide);

    partial void OnServicesChanged(ObservableCollection<WorshipService> value)
    {
        OnPropertyChanged(nameof(HasNoServices));
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoServices));
    }

    partial void OnScheduleItemsChanged(ObservableCollection<ScheduleItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasNoScheduleItems));
        OnPropertyChanged(nameof(CurrentLiveItemTitle));
        value.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoScheduleItems));
            OnPropertyChanged(nameof(CurrentLiveItemTitle));
        };
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var list = await _serviceService.GetAllAsync();
            Services = new ObservableCollection<WorshipService>(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load worship services");
            SetError("Could not load services.");
        }
        finally { IsBusy = false; }
    }

    // ── Service list commands ─────────────────────────────────────────────────

    [RelayCommand]
    private void ShowCreateForm()
    {
        NewServiceName  = string.Empty;
        NewServiceDate  = DateTime.Today;
        IsCreatingService = true;
    }

    [RelayCommand]
    private void CancelCreate()
    {
        IsCreatingService = false;
    }

    [RelayCommand]
    private async Task ConfirmCreateServiceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewServiceName))
        {
            SetError("Service name is required.");
            return;
        }

        ClearError();
        try
        {
            var created = await _serviceService.CreateAsync(new WorshipService
            {
                Name = NewServiceName.Trim(),
                Date = NewServiceDate
            });
            _logger.LogInformation("Created worship service {ServiceId}: {Name}", created.Id, created.Name);
            IsCreatingService = false;
            Services.Insert(0, created);
            await OpenServiceAsync(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create service");
            SetError("Could not create service.");
        }
    }

    [RelayCommand]
    private async Task ImportVideoPsalmServiceAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import VideoPsalm service",
            Filter = "VideoPsalm agenda (*.vpagd)|*.vpagd",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        if (IsBusy) return;
        IsBusy = true;
        ClearError();

        VpImportSummary summary;
        try
        {
            summary = await _vpImporter.ImportAsync(dialog.FileName);
            _logger.LogInformation("Imported VideoPsalm agenda from {File}", dialog.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import VideoPsalm agenda from {File}", dialog.FileName);
            _dialogService.Inform("Could not import this VideoPsalm agenda. It may be corrupt or an unsupported file.", "Import Service");
            return;
        }
        finally { IsBusy = false; }

        await LoadAsync();
        _dialogService.Inform(FormatSummary(summary), "Import Service");
    }

    private static string FormatSummary(VpImportSummary s)
    {
        if (s.AlreadyImported)
            return $"\"{s.ServiceName}\" was already imported — nothing was added.";

        var lines = new List<string>
        {
            $"Imported \"{s.ServiceName}\" ({s.TotalItems} items):",
            $"• Songs: {s.SongsImported} new, {s.SongsReused} reused",
            $"• Scripture references: {s.ScriptureReferences} (verse text omitted — it's licensed; install a Bible version to display it)",
            $"• Media: {s.MediaImported} new, {s.MediaReused} reused"
        };
        if (s.ThemesCreated > 0) lines.Add($"• Themes reconstructed: {s.ThemesCreated}");
        if (s.MediaMissing > 0) lines.Add($"• Media skipped (bytes not found): {s.MediaMissing}");
        if (s.ItemsSkipped > 0) lines.Add($"• Other items skipped: {s.ItemsSkipped}");
        return string.Join(Environment.NewLine, lines);
    }

    [RelayCommand]
    private async Task DeleteServiceAsync(WorshipService? service)
    {
        if (service is null) return;
        if (!_dialogService.Confirm($"Delete \"{service.Name}\"? This will remove all its schedule items.", "Delete Service"))
            return;

        try
        {
            await _serviceService.DeleteAsync(service.Id);
            Services.Remove(service);
            if (OpenedService?.Id == service.Id)
                CloseService();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete service {ServiceId}", service.Id);
            SetError("Could not delete service.");
        }
    }

    [RelayCommand]
    private async Task OpenServiceAsync(WorshipService? service)
    {
        if (service is null) return;
        ClearError();
        IsBusy = true;
        try
        {
            var loaded = await _serviceService.GetWithItemsAsync(service.Id);
            if (loaded is null)
            {
                SetError("Service not found.");
                return;
            }
            OpenedService   = loaded;
            IsServiceOpen   = true;
            IsLiveMode      = false;
            CurrentLiveIndex = -1;
            RebuildScheduleItems(loaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open service {ServiceId}", service.Id);
            SetError("Could not load service.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CloseService()
    {
        if (IsLiveMode)
            StopLive();

        IsServiceOpen    = false;
        OpenedService    = null;
        ScheduleItems    = [];
        SelectedItem     = null;
        IsAddingSong     = false;
        IsAddingBible    = false;
        IsAddingMedia    = false;
    }

    // ── Add Song ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ShowAddSongPanelAsync()
    {
        IsAddingBible      = false;
        IsAddingMedia      = false;
        SongPickerSearchTerm = string.Empty;
        PickerSelectedSong   = null;
        IsAddingSong         = true;

        try
        {
            var songs = await _songService.GetAllAsync();
            SongPickerResults = new ObservableCollection<Song>(songs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load songs for picker");
            SetError("Could not load songs.");
            IsAddingSong = false;
        }
    }

    partial void OnSongPickerSearchTermChanged(string value)
    {
        _ = FilterSongPickerAsync(value);
    }

    private async Task FilterSongPickerAsync(string term)
    {
        try
        {
            var results = string.IsNullOrWhiteSpace(term)
                ? await _songService.GetAllAsync()
                : await _songService.SearchByTitleAsync(term);
            SongPickerResults = new ObservableCollection<Song>(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Song picker search failed");
        }
    }

    [RelayCommand]
    private void CancelAddSong()
    {
        IsAddingSong         = false;
        SongPickerSearchTerm = string.Empty;
        PickerSelectedSong   = null;
    }

    [RelayCommand]
    private async Task ConfirmAddSongAsync()
    {
        if (PickerSelectedSong is null || OpenedService is null) return;

        try
        {
            await _serviceService.AddSongItemAsync(OpenedService.Id, PickerSelectedSong.Id, autoAdvanceSeconds: DefaultAutoAdvanceSeconds);
            _logger.LogInformation("Added song {SongId} to service {ServiceId}", PickerSelectedSong.Id, OpenedService.Id);
            IsAddingSong = false;
            await RefreshScheduleItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add song to service");
            SetError("Could not add song.");
        }
    }

    // ── Add Bible ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ShowAddBiblePanelAsync()
    {
        IsAddingSong                  = false;
        IsAddingMedia                 = false;
        SelectedAddBibleBook          = null;
        SelectedAddBibleVersion       = null;
        AddBibleBooks                 = [];
        AddBibleChapters.Clear();
        SelectedAddBibleChapterNumber = 0;
        _versePickerAnchor            = 0;
        _versePickerEnd               = 0;
        AddBibleVerses.Clear();
        RefreshVersePickerHighlight();
        IsAddingBible                 = true;

        try
        {
            var versions = await _bibleService.GetVersionsAsync();
            BibleVersions = new ObservableCollection<BibleVersion>(versions);
            if (BibleVersions.Count > 0)
                SelectedAddBibleVersion = BibleVersions[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Bible versions for picker");
            SetError("Could not load Bible versions.");
            IsAddingBible = false;
        }
    }

    partial void OnSelectedAddBibleBookChanged(BibleBook? value)
    {
        AddBibleChapters.Clear();
        SelectedAddBibleChapterNumber = 0;
        _versePickerAnchor = 0;
        _versePickerEnd    = 0;
        AddBibleVerses.Clear();
        RefreshVersePickerHighlight();
        if (value is null) return;

        for (int i = 1; i <= value.ChapterCount; i++)
            AddBibleChapters.Add(i);

        if (AddBibleChapters.Count > 0)
            SelectedAddBibleChapterNumber = 1;
    }

    async partial void OnSelectedAddBibleChapterNumberChanged(int value)
    {
        _versePickerAnchor = 0;
        _versePickerEnd    = 0;
        AddBibleVerses.Clear();
        RefreshVersePickerHighlight();

        if (value <= 0 || SelectedAddBibleVersion is null || SelectedAddBibleBook is null) return;

        try
        {
            var verses = await _bibleService.GetVersesAsync(
                SelectedAddBibleVersion.Id, SelectedAddBibleBook.Name, value);
            AddBibleVerses = new ObservableCollection<BibleVersePickerItem>(
                verses.Select(v => new BibleVersePickerItem { Verse = v }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load verses for picker chapter {Chapter}", value);
        }
    }

    async partial void OnSelectedAddBibleVersionChanged(BibleVersion? value)
    {
        AddBibleBooks        = [];
        SelectedAddBibleBook = null;
        AddBibleChapters.Clear();
        SelectedAddBibleChapterNumber = 0;
        _versePickerAnchor = 0;
        _versePickerEnd    = 0;
        AddBibleVerses.Clear();
        RefreshVersePickerHighlight();
        if (value is null) return;

        try
        {
            var books = await _bibleService.GetBooksAsync(value.Id);
            AddBibleBooks = new ObservableCollection<BibleBook>(books.OrderBy(b => b.BookNumber));
            if (AddBibleBooks.Count > 0)
                SelectedAddBibleBook = AddBibleBooks[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load books for version {VersionId}", value.Id);
        }
    }

    [RelayCommand]
    private void CancelAddBible()
    {
        IsAddingBible      = false;
        _versePickerAnchor = 0;
        _versePickerEnd    = 0;
        RefreshVersePickerHighlight();
    }

    [RelayCommand]
    private void SelectVerseInPicker(BibleVersePickerItem item)
    {
        if (_versePickerAnchor == 0 || item.Number < _versePickerAnchor)
        {
            // New anchor — start fresh selection
            _versePickerAnchor = item.Number;
            _versePickerEnd    = item.Number;
        }
        else if (item.Number == _versePickerAnchor && _versePickerAnchor == _versePickerEnd)
        {
            // Clicking the single selected verse deselects
            _versePickerAnchor = 0;
            _versePickerEnd    = 0;
        }
        else
        {
            // Extend range end
            _versePickerEnd = item.Number;
        }
        RefreshVersePickerHighlight();
    }

    private void RefreshVersePickerHighlight()
    {
        var lo = Math.Min(_versePickerAnchor, _versePickerEnd);
        var hi = Math.Max(_versePickerAnchor, _versePickerEnd);
        foreach (var v in AddBibleVerses)
            v.IsInRange = _versePickerAnchor > 0 && v.Number >= lo && v.Number <= hi;
        OnPropertyChanged(nameof(BiblePickerSelectionLabel));
        OnPropertyChanged(nameof(CanConfirmAddBible));
    }

    // ── Add Media ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ShowAddMediaPanelAsync()
    {
        IsAddingSong        = false;
        IsAddingBible       = false;
        PickerSelectedMedia = null;
        MediaPickerFiles    = [];
        IsAddingMedia       = true;

        try
        {
            var files = await _mediaService.GetAllAsync();
            MediaPickerFiles = new ObservableCollection<MediaFile>(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media files for picker");
            SetError("Could not load media files.");
            IsAddingMedia = false;
        }
    }

    [RelayCommand]
    private void CancelAddMedia()
    {
        IsAddingMedia       = false;
        PickerSelectedMedia = null;
    }

    [RelayCommand]
    private async Task ConfirmAddMediaAsync()
    {
        if (PickerSelectedMedia is null || OpenedService is null) return;

        try
        {
            await _serviceService.AddMediaItemAsync(OpenedService.Id, PickerSelectedMedia.Id, autoAdvanceSeconds: DefaultAutoAdvanceSeconds);
            _logger.LogInformation("Added media {MediaId} to service {ServiceId}",
                PickerSelectedMedia.Id, OpenedService.Id);
            IsAddingMedia = false;
            await RefreshScheduleItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add media item to service");
            SetError("Could not add media file.");
        }
    }

    [RelayCommand]
    private async Task ConfirmAddBibleAsync()
    {
        if (OpenedService is null || SelectedAddBibleVersion is null || SelectedAddBibleBook is null)
        {
            SetError("Select a Bible version and book.");
            return;
        }

        var chapter = SelectedAddBibleChapterNumber;
        if (chapter < 1) { SetError("Select a chapter."); return; }

        if (_versePickerAnchor <= 0) { SetError("Select at least one verse."); return; }

        var verseStart = Math.Min(_versePickerAnchor, _versePickerEnd);
        var verseEnd   = Math.Max(_versePickerAnchor, _versePickerEnd);

        ClearError();
        try
        {
            await _serviceService.AddBibleItemAsync(
                OpenedService.Id,
                SelectedAddBibleBook.Name,
                chapter, verseStart, verseEnd,
                SelectedAddBibleVersion.Id,
                autoAdvanceSeconds: DefaultAutoAdvanceSeconds);
            _logger.LogInformation("Added Bible {Book} {Ch}:{Vs}-{Ve} to service {ServiceId}",
                SelectedAddBibleBook.Name, chapter, verseStart, verseEnd, OpenedService.Id);
            IsAddingBible = false;
            await RefreshScheduleItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add Bible item to service");
            SetError("Could not add Bible passage.");
        }
    }

    // ── Schedule item management ──────────────────────────────────────────────

    private async Task RefreshScheduleItemsAsync()
    {
        if (OpenedService is null) return;
        try
        {
            var loaded = await _serviceService.GetWithItemsAsync(OpenedService.Id);
            if (loaded is null) return;
            OpenedService = loaded;
            RebuildScheduleItems(loaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh schedule items");
        }
    }

    private void RebuildScheduleItems(WorshipService service)
    {
        foreach (var vm in ScheduleItems)
            UnsubscribeItemEvents(vm);

        var ordered = service.GetOrderedItems();
        var vms = ordered.Select((item, index) =>
        {
            var vm = new ScheduleItemViewModel(item)
            {
                CanMoveUp   = index > 0,
                CanMoveDown = index < ordered.Count - 1
            };
            SubscribeItemEvents(vm);
            return vm;
        }).ToList();

        ScheduleItems = new ObservableCollection<ScheduleItemViewModel>(vms);
        RefreshLiveHighlight();
    }

    private void SubscribeItemEvents(ScheduleItemViewModel vm)
    {
        vm.MoveUpRequested            += OnItemMoveUp;
        vm.MoveDownRequested          += OnItemMoveDown;
        vm.DeleteRequested            += OnItemDelete;
        vm.Selected                   += OnItemSelected;
        vm.AutoAdvanceChangeRequested += OnAutoAdvanceChangeRequested;
        vm.VerseOrderOverrideChangeRequested += OnVerseOrderOverrideChangeRequested;
    }

    private void UnsubscribeItemEvents(ScheduleItemViewModel vm)
    {
        vm.MoveUpRequested            -= OnItemMoveUp;
        vm.MoveDownRequested          -= OnItemMoveDown;
        vm.DeleteRequested            -= OnItemDelete;
        vm.Selected                   -= OnItemSelected;
        vm.AutoAdvanceChangeRequested -= OnAutoAdvanceChangeRequested;
        vm.VerseOrderOverrideChangeRequested -= OnVerseOrderOverrideChangeRequested;
    }

    private async void OnItemMoveUp(object? sender, EventArgs e)
    {
        if (sender is not ScheduleItemViewModel vm || OpenedService is null) return;
        var index = ScheduleItems.IndexOf(vm);
        if (index <= 0) return;

        ScheduleItems.Move(index, index - 1);
        await PersistOrderAsync();
    }

    private async void OnItemMoveDown(object? sender, EventArgs e)
    {
        if (sender is not ScheduleItemViewModel vm || OpenedService is null) return;
        var index = ScheduleItems.IndexOf(vm);
        if (index < 0 || index >= ScheduleItems.Count - 1) return;

        ScheduleItems.Move(index, index + 1);
        await PersistOrderAsync();
    }

    private async Task PersistOrderAsync()
    {
        if (OpenedService is null) return;

        UpdateMoveButtons();

        var orderedIds = ScheduleItems.Select(vm => vm.Item.Id).ToList();
        try
        {
            await _serviceService.ReorderItemsAsync(OpenedService.Id, orderedIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist item order");
        }
    }

    private void UpdateMoveButtons()
    {
        for (var i = 0; i < ScheduleItems.Count; i++)
        {
            ScheduleItems[i].CanMoveUp   = i > 0;
            ScheduleItems[i].CanMoveDown = i < ScheduleItems.Count - 1;
        }
    }

    private async void OnItemDelete(object? sender, EventArgs e)
    {
        if (sender is not ScheduleItemViewModel vm || OpenedService is null) return;
        if (!_dialogService.Confirm($"Remove \"{vm.DisplayTitle}\" from the schedule?", "Remove Item"))
            return;

        UnsubscribeItemEvents(vm);
        try
        {
            await _serviceService.RemoveItemAsync(vm.Item.Id);
            ScheduleItems.Remove(vm);
            UpdateMoveButtons();

            if (IsLiveMode)
            {
                // Recalculate live index after deletion
                CurrentLiveIndex = Math.Min(CurrentLiveIndex, ScheduleItems.Count - 1);
                RefreshLiveHighlight();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove schedule item {ItemId}", vm.Item.Id);
            SetError("Could not remove item.");
            SubscribeItemEvents(vm); // re-attach on failure
        }
    }

    private void OnItemSelected(object? sender, EventArgs e)
    {
        if (sender is not ScheduleItemViewModel vm || !IsLiveMode) return;
        var index = ScheduleItems.IndexOf(vm);
        if (index < 0) return;
        CurrentLiveIndex = index;
        RefreshLiveHighlight();
        _ = LoadSlidesForCurrentItemAsync();
    }

    // ── Live mode ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartLive()
    {
        if (ScheduleItems.Count == 0) { SetError("Add at least one item before starting."); return; }
        ClearError();
        IsAddingSong  = false;
        IsAddingBible = false;
        IsAddingMedia = false;
        IsLiveMode       = true;
        CurrentLiveIndex = 0;
        _projectionService.SetServiceScheduleActive(true);
        RefreshLiveHighlight();
        _ = LoadSlidesForCurrentItemAsync();
    }

    [RelayCommand]
    private void StopLive()
    {
        StopAutoAdvanceTimer();
        IsLiveMode = false;
        _projectionService.SetNextScheduleItemPreview(null);
        _projectionService.Stop();
        RefreshLiveHighlight();
    }

    [RelayCommand(CanExecute = nameof(CanNextItem))]
    private void NextItem()
    {
        if (CurrentLiveIndex >= ScheduleItems.Count - 1) return;
        CurrentLiveIndex++;
        RefreshLiveHighlight();
        _ = LoadSlidesForCurrentItemAsync();
    }

    private bool CanNextItem() => IsLiveMode && CurrentLiveIndex < ScheduleItems.Count - 1;

    [RelayCommand(CanExecute = nameof(CanPrevItem))]
    private void PrevItem()
    {
        if (CurrentLiveIndex <= 0) return;
        CurrentLiveIndex--;
        RefreshLiveHighlight();
        _ = LoadSlidesForCurrentItemAsync();
    }

    private bool CanPrevItem() => IsLiveMode && CurrentLiveIndex > 0;

    partial void OnCurrentLiveIndexChanged(int value)
    {
        NextItemCommand.NotifyCanExecuteChanged();
        PrevItemCommand.NotifyCanExecuteChanged();
    }

    private void RefreshLiveHighlight()
    {
        for (var i = 0; i < ScheduleItems.Count; i++)
            ScheduleItems[i].IsCurrentLiveItem = IsLiveMode && i == CurrentLiveIndex;
    }

    private async Task LoadSlidesForCurrentItemAsync()
    {
        if (CurrentLiveIndex < 0 || CurrentLiveIndex >= ScheduleItems.Count) return;
        var itemVm = ScheduleItems[CurrentLiveIndex];
        ClearError();

        try
        {
            switch (itemVm.Item)
            {
                case SongScheduleItem songItem:
                {
                    var slides = _songService.GenerateSlides(songItem.Song, songItem.ThemeId, songItem.VerseOrderOverride);
                    if (slides.Count == 0) { SetError("This song has no lyrics to project."); return; }
                    _projectionService.LoadSlides(slides, songItem.Song.Title, ProjectionContextKeys.ServiceSong(songItem.SongId));
                    break;
                }

                case BibleScheduleItem bibleItem:
                {
                    var versionId = bibleItem.BibleVersionId ?? 0;
                    var allVerses = await _bibleService.GetVersesAsync(versionId, bibleItem.Book, bibleItem.Chapter);
                    var verses = allVerses
                        .Where(v => v.Verse >= bibleItem.VerseStart && v.Verse <= bibleItem.VerseEnd)
                        .ToList();
                    if (verses.Count == 0) { SetError($"No verses found for {bibleItem.Reference}."); return; }
                    var slides = _bibleService.GenerateSlides(verses, BibleVersesPerSlide, bibleItem.ThemeId);
                    _projectionService.LoadSlides(slides, bibleItem.Reference);
                    break;
                }

                case MediaScheduleItem mediaItem:
                {
                    var slide = _mediaService.GenerateSlide(mediaItem.MediaFile, mediaItem.ThemeId);
                    _projectionService.LoadSlides([slide], mediaItem.MediaFile.FileName);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load slides for schedule item {ItemId}", itemVm.Item.Id);
            SetError("Could not project this item.");
        }

        // Push the first slide of the next schedule item so the stage view
        // can show it in "UP NEXT" when the operator reaches the last slide.
        var nextPreview = await GetNextItemFirstSlideAsync();
        _projectionService.SetNextScheduleItemPreview(nextPreview);
    }

    private async Task<Slide?> GetNextItemFirstSlideAsync()
    {
        var nextIdx = CurrentLiveIndex + 1;
        if (nextIdx >= ScheduleItems.Count) return null;

        try
        {
            return ScheduleItems[nextIdx].Item switch
            {
                SongScheduleItem songItem =>
                    _songService.GenerateSlides(songItem.Song, songItem.ThemeId, songItem.VerseOrderOverride).FirstOrDefault(),

                BibleScheduleItem bibleItem when bibleItem.BibleVersionId.HasValue =>
                    await GetBibleItemFirstSlideAsync(bibleItem),

                MediaScheduleItem mediaItem =>
                    _mediaService.GenerateSlide(mediaItem.MediaFile, mediaItem.ThemeId),

                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not generate next item preview slide (non-fatal)");
            return null;
        }
    }

    private async Task<Slide?> GetBibleItemFirstSlideAsync(BibleScheduleItem item)
    {
        var allVerses = await _bibleService.GetVersesAsync(
            item.BibleVersionId!.Value, item.Book, item.Chapter);
        var verses = allVerses
            .Where(v => v.Verse >= item.VerseStart && v.Verse <= item.VerseEnd)
            .ToList();
        return verses.Count > 0
            ? _bibleService.GenerateSlides(verses, BibleVersesPerSlide, item.ThemeId).FirstOrDefault()
            : null;
    }

    private void OnNextItemRequested(object? sender, EventArgs e)
    {
        if (!IsLiveMode || !CanNextItem()) return;
        System.Windows.Application.Current?.Dispatcher.Invoke(NextItem);
    }

    private void OnPrevItemRequested(object? sender, EventArgs e)
    {
        if (!IsLiveMode || !CanPrevItem()) return;
        System.Windows.Application.Current?.Dispatcher.Invoke(PrevItem);
    }

    // A library song was edited. Refresh the cached entity on EVERY schedule item that uses it
    // (the items were loaded once when the service opened, so queued items would otherwise project
    // stale content), then live-update the projector if the song is the current or next item.
    private async void OnSongLibrarySaved(object? sender, int songId)
    {
        if (SongItems(songId).Count == 0) return;
        try
        {
            var fresh = await _songService.GetByIdAsync(songId);
            if (fresh is null) return;

            foreach (var item in SongItems(songId))
                item.Song = fresh; // queued items pick this up when the operator reaches them

            if (IsLiveMode) ApplyEditedSongToLiveProjection(songId, fresh);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply edited song {SongId} to the service schedule", songId);
        }
    }

    private List<SongScheduleItem> SongItems(int songId) =>
        ScheduleItems.Select(vm => vm.Item).OfType<SongScheduleItem>().Where(s => s.SongId == songId).ToList();

    private void ApplyEditedSongToLiveProjection(int songId, Song fresh)
    {
        // On screen now → swap slides in place, keeping position + this item's theme/verse order.
        if (CurrentLiveSong(songId) is { } current)
        {
            var slides = _songService.GenerateSlides(fresh, current.ThemeId, current.VerseOrderOverride);
            if (slides.Count > 0)
                _projectionService.TryUpdateSlides(ProjectionContextKeys.ServiceSong(songId), slides, fresh.Title);
        }

        // Queued as the next item → refresh the stage view's UP NEXT preview.
        if (IsNextLiveSong(songId))
            _ = RefreshNextItemPreviewAsync();
    }

    private SongScheduleItem? CurrentLiveSong(int songId) =>
        CurrentLiveIndex >= 0 && CurrentLiveIndex < ScheduleItems.Count
        && ScheduleItems[CurrentLiveIndex].Item is SongScheduleItem s && s.SongId == songId ? s : null;

    private bool IsNextLiveSong(int songId)
    {
        var nextIdx = CurrentLiveIndex + 1;
        return nextIdx < ScheduleItems.Count
            && ScheduleItems[nextIdx].Item is SongScheduleItem s && s.SongId == songId;
    }

    private async Task RefreshNextItemPreviewAsync() =>
        _projectionService.SetNextScheduleItemPreview(await GetNextItemFirstSlideAsync());

    // ── Auto-advance timer ────────────────────────────────────────────────────

    private DispatcherTimer? _autoAdvanceTimer;

    private void OnSlideChangedForAutoAdvance(object? sender, Slide? slide)
    {
        // Called on any thread; timer operations must be on the UI thread.
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!IsLiveMode || CurrentLiveIndex < 0 || CurrentLiveIndex >= ScheduleItems.Count)
            {
                StopAutoAdvanceTimer();
                return;
            }
            var seconds = ScheduleItems[CurrentLiveIndex].Item.AutoAdvanceSeconds;
            if (seconds is > 0)
                StartAutoAdvanceTimer(seconds.Value);
            else
                StopAutoAdvanceTimer();
        });
    }

    private void StartAutoAdvanceTimer(int seconds)
    {
        StopAutoAdvanceTimer();
        _autoAdvanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _autoAdvanceTimer.Tick += OnAutoAdvanceTick;
        _autoAdvanceTimer.Start();
    }

    private void StopAutoAdvanceTimer()
    {
        if (_autoAdvanceTimer is null) return;
        _autoAdvanceTimer.Stop();
        _autoAdvanceTimer.Tick -= OnAutoAdvanceTick;
        _autoAdvanceTimer = null;
    }

    private void OnAutoAdvanceTick(object? sender, EventArgs e)
    {
        StopAutoAdvanceTimer(); // Always one-shot; SlideChanged restarts it if needed.

        if (_projectionService.CurrentSlideIndex < _projectionService.CurrentSlides.Count - 1)
        {
            _projectionService.Next(); // SlideChanged will restart the timer.
        }
        else if (CanNextItem())
        {
            NextItem(); // LoadSlidesForCurrentItemAsync restarts the timer via SlideChanged.
        }
        // else: end of service — timer stays stopped.
    }

    private async void OnAutoAdvanceChangeRequested(object? sender, int? seconds)
    {
        if (sender is not ScheduleItemViewModel vm) return;
        try
        {
            await _serviceService.SetItemAutoAdvanceAsync(vm.Item.Id, seconds);
            vm.Item.AutoAdvanceSeconds = seconds; // keep entity in sync without a full reload
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist auto-advance for item {ItemId}", vm.Item.Id);
        }
    }

    private async void OnVerseOrderOverrideChangeRequested(object? sender, string? verseOrder)
    {
        if (sender is not ScheduleItemViewModel vm || vm.Item is not SongScheduleItem songItem) return;
        try
        {
            await _serviceService.SetItemVerseOrderOverrideAsync(vm.Item.Id, verseOrder);
            songItem.VerseOrderOverride = verseOrder; // keep entity in sync without a full reload
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist verse order override for item {ItemId}", vm.Item.Id);
        }
    }

    private void OnProjectionStateChanged(object? sender, bool isProjecting)
    {
        // If the operator stops projection from the main bar while in live mode, exit live mode.
        if (!isProjecting && IsLiveMode)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                StopAutoAdvanceTimer();
                IsLiveMode = false;
                RefreshLiveHighlight();
            });
        }
    }

    public void Dispose()
    {
        StopAutoAdvanceTimer();
        _projectionService.SetServiceScheduleActive(false);
        _projectionService.SetNextScheduleItemPreview(null);
        _projectionService.ProjectionStateChanged        -= OnProjectionStateChanged;
        _projectionService.SlideChanged                  -= OnSlideChangedForAutoAdvance;
        _projectionService.NextScheduleItemRequested     -= OnNextItemRequested;
        _projectionService.PreviousScheduleItemRequested -= OnPrevItemRequested;
        _songNotifier.SongSaved                          -= OnSongLibrarySaved;
        foreach (var vm in ScheduleItems)
            UnsubscribeItemEvents(vm);
    }
}
