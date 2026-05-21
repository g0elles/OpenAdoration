using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

/// <summary>
/// ViewModel for the Bible browser. Implements <see cref="IDisposable"/> so that
/// singleton event subscriptions (<see cref="IProjectionService"/> and
/// <see cref="IBibleImportService"/>) are released when the view is unloaded.
/// </summary>
public partial class BibleViewModel : BaseViewModel, IDisposable
{
    private readonly IBibleService           _bibleService;
    private readonly IProjectionService      _projectionService;
    private readonly IBibleImportService     _importService;
    private readonly ILogger<BibleViewModel> _logger;

    // Cancellation tokens for rapid-selection races.
    private CancellationTokenSource _booksCts  = new();
    private CancellationTokenSource _versesCts = new();

    // Cancellation token for in-flight searches.
    private CancellationTokenSource? _searchCts;

    // -- Versions --------------------------------------------------------------
    [ObservableProperty] private ObservableCollection<BibleVersion> _versions = new();
    [ObservableProperty] private BibleVersion? _selectedVersion;

    public bool HasVersions   => Versions.Count > 0;
    public bool NoVersionsYet => Versions.Count == 0 && !IsBusy;

    // -- Books -----------------------------------------------------------------
    [ObservableProperty] private ObservableCollection<BibleBook> _books = new();
    [ObservableProperty] private BibleBook? _selectedBook;

    // -- Chapters --------------------------------------------------------------
    [ObservableProperty] private ObservableCollection<int> _chapters = new();

    // 0 = nothing selected (never present in collection, so ListBox deselects cleanly)
    [ObservableProperty] private int _selectedChapter;

    // -- Verses ----------------------------------------------------------------
    [ObservableProperty] private ObservableCollection<BibleVerse> _displayedVerses = new();
    [ObservableProperty] private BibleVerse? _selectedVerse;

    private List<BibleVerse> _chapterVerses = new();

    // -- Search ----------------------------------------------------------------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    private string _searchText = string.Empty;

    private List<BibleVerse> _searchResults = new();
    public  bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText) && _searchResults.Count > 0;

    // -- Import progress (delegated to singleton IBibleImportService) ----------
    public bool   IsImporting         => _importService.IsImporting;
    public int    ImportProgress      => _importService.Progress;
    public int    ImportTotal         => _importService.Total;
    public string ImportStatusMessage => _importService.StatusMessage;

    // Indeterminate while parsing (before Total is known); determinate once verse count is set.
    public bool IsImportIndeterminate => _importService.IsImporting && _importService.Total == 0;

    // Import button is disabled while an import is running.
    public bool CanImport => !_importService.IsImporting;

    // -- Import summary (shown after a successful import) ----------------------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportSummary))]
    private string _importSummary = string.Empty;

    public bool HasImportSummary => !string.IsNullOrEmpty(ImportSummary);

    // -- Projection state ------------------------------------------------------
    public bool CanGoPreviousVerse => CurrentVerseIndex > 0;
    public bool CanGoNextVerse     => CurrentVerseIndex >= 0
                                   && CurrentVerseIndex < DisplayedVerses.Count - 1;

    private int CurrentVerseIndex =>
        SelectedVerse is null ? -1 : DisplayedVerses.IndexOf(SelectedVerse);

    // -- Constructor -----------------------------------------------------------

    public BibleViewModel(
        IBibleService           bibleService,
        IProjectionService      projectionService,
        IBibleImportService     importService,
        ILogger<BibleViewModel> logger)
    {
        _bibleService      = bibleService;
        _projectionService = projectionService;
        _importService     = importService;
        _logger            = logger;

        _importService.StateChanged   += OnImportStateChanged;
        _importService.ImportCompleted += OnImportCompleted;
        _importService.ImportFailed    += OnImportFailed;
    }

    private void OnImportStateChanged(object? sender, EventArgs _)
    {
        OnPropertyChanged(nameof(IsImporting));
        OnPropertyChanged(nameof(ImportProgress));
        OnPropertyChanged(nameof(ImportTotal));
        OnPropertyChanged(nameof(ImportStatusMessage));
        OnPropertyChanged(nameof(IsImportIndeterminate));
        OnPropertyChanged(nameof(CanImport));
        ImportVersionCommand.NotifyCanExecuteChanged();
    }

    private async void OnImportCompleted(object? sender, BibleImportCompletedArgs e)
    {
        ImportSummary = $"Imported {e.VerseCount:N0} verses ({e.VersionName})";
        await LoadVersionsCoreAsync();
    }

    private void OnImportFailed(object? sender, BibleImportFailedArgs e)
    {
        SetError(e.Message);
    }

    // -- Dispose -- releases singleton event subscriptions --------------------

    public void Dispose()
    {
        _importService.StateChanged   -= OnImportStateChanged;
        _importService.ImportCompleted -= OnImportCompleted;
        _importService.ImportFailed    -= OnImportFailed;

        _booksCts.Cancel();
        _booksCts.Dispose();
        _versesCts.Cancel();
        _versesCts.Dispose();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }

    // -- Import ----------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void ImportVersion(string filePath)
    {
        ImportSummary = string.Empty;
        ClearError();
        _importService.StartImport(filePath);
    }

    [RelayCommand]
    private void CancelImport() => _importService.Cancel();

    // -- Load ------------------------------------------------------------------

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        await LoadVersionsCoreAsync();
    }

    private async Task LoadVersionsCoreAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            var list = await _bibleService.GetVersionsAsync();

            Versions.Clear();
            foreach (var v in list) Versions.Add(v);

            NotifyVersionState();

            SelectedVersion = Versions.FirstOrDefault(v => v.Id == SelectedVersion?.Id)
                           ?? Versions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Bible versions");
            SetError("Could not load Bible versions.");
        }
        finally
        {
            IsBusy = false;
            NotifyVersionState();
        }
    }

    // -- Version selection -----------------------------------------------------

    partial void OnSelectedVersionChanged(BibleVersion? value)
    {
        ResetBooksAndBelow();
        if (value is not null)
        {
            _booksCts.Cancel();
            _booksCts.Dispose();
            _booksCts = new CancellationTokenSource();
            _ = LoadBooksAsync(value.Id, _booksCts.Token);
        }
    }

    private async Task LoadBooksAsync(int versionId, CancellationToken ct)
    {
        try
        {
            var list = await _bibleService.GetBooksAsync(versionId);

            if (ct.IsCancellationRequested) return;

            Books.Clear();
            foreach (var b in list) Books.Add(b);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            _logger.LogError(ex, "Failed to load books for version {Id}", versionId);
            SetError("Could not load books.");
        }
    }

    // -- Book selection --------------------------------------------------------

    partial void OnSelectedBookChanged(BibleBook? value)
    {
        Chapters.Clear();
        SelectedChapter = 0;
        SelectedVerse   = null;

        if (value is null) return;

        for (int c = 1; c <= value.ChapterCount; c++)
            Chapters.Add(c);
    }

    // -- Chapter selection -----------------------------------------------------

    partial void OnSelectedChapterChanged(int value)
    {
        DisplayedVerses.Clear();
        _chapterVerses.Clear();
        SelectedVerse = null;

        if (value > 0 && SelectedVersion is not null && SelectedBook is not null)
        {
            _versesCts.Cancel();
            _versesCts.Dispose();
            _versesCts = new CancellationTokenSource();
            _ = LoadVersesAsync(SelectedVersion.Id, SelectedBook.Name, value, _versesCts.Token);
        }
    }

    private async Task LoadVersesAsync(int versionId, string book, int chapter, CancellationToken ct)
    {
        try
        {
            var list = await _bibleService.GetVersesAsync(versionId, book, chapter);

            if (ct.IsCancellationRequested) return;

            _chapterVerses = list.ToList();
            DisplayedVerses.Clear();
            foreach (var v in _chapterVerses) DisplayedVerses.Add(v);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            _logger.LogError(ex, "Failed to load {Book} {Chapter}", book, chapter);
            SetError("Could not load verses.");
        }
    }

    // -- Verse projection ------------------------------------------------------

    partial void OnSelectedVerseChanged(BibleVerse? value)
    {
        RefreshNavigationState();

        if (value is null) return;

        try
        {
            // Use all verses in the active list so Next/Prev slide navigates verse by verse.
            // In search mode use search results; otherwise use the full chapter.
            var sourceList = IsSearchActive ? _searchResults : _chapterVerses;

            if (sourceList.Count == 0)
            {
                var single = _bibleService.GenerateSlide(new[] { value });
                _projectionService.LoadSlides(new[] { single }, value.Reference);
                return;
            }

            var slides     = sourceList.Select(v => _bibleService.GenerateSlide(new[] { v })).ToList();
            var startIndex = sourceList.IndexOf(value);
            if (startIndex < 0) startIndex = 0;

            var contextLabel = IsSearchActive
                ? value.Reference
                : $"{value.Book} {value.Chapter}";

            _projectionService.LoadSlides(slides, contextLabel);

            if (startIndex > 0)
                _projectionService.GoTo(startIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to project verse");
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousVerse))]
    private void PreviousVerse()
    {
        var i = CurrentVerseIndex;
        if (i > 0) SelectedVerse = DisplayedVerses[i - 1];
    }

    [RelayCommand(CanExecute = nameof(CanGoNextVerse))]
    private void NextVerse()
    {
        var i = CurrentVerseIndex;
        if (i >= 0 && i < DisplayedVerses.Count - 1)
            SelectedVerse = DisplayedVerses[i + 1];
    }

    // -- Search ----------------------------------------------------------------

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (SelectedVersion is null || string.IsNullOrWhiteSpace(SearchText))
        {
            ClearSearch();
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsBusy = true;
        ClearError();
        try
        {
            var results = (await _bibleService.SearchAsync(
                SelectedVersion.Id, SearchText, maxResults: 200, ct: ct)).ToList();

            if (ct.IsCancellationRequested) return;

            _searchResults = results;
            DisplayedVerses.Clear();
            foreach (var v in _searchResults) DisplayedVerses.Add(v);
            OnPropertyChanged(nameof(IsSearchActive));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible search failed");
            SetError("Search failed. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText     = string.Empty;
        _searchResults = new();
        DisplayedVerses.Clear();
        foreach (var v in _chapterVerses) DisplayedVerses.Add(v);
        OnPropertyChanged(nameof(IsSearchActive));
    }

    // -- Delete version --------------------------------------------------------

    [RelayCommand]
    private async Task DeleteVersionAsync()
    {
        if (SelectedVersion is null || IsBusy) return;

        IsBusy = true;
        ClearError();
        bool deletedOk = false;

        try
        {
            await _bibleService.DeleteVersionAsync(SelectedVersion.Id);
            deletedOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Bible version {Id}", SelectedVersion?.Id);
            SetError("Failed to delete version.");
        }
        finally
        {
            IsBusy = false;
        }

        if (deletedOk)
            await LoadVersionsCoreAsync();
    }

    // -- Private helpers -------------------------------------------------------

    private void ResetBooksAndBelow()
    {
        Books.Clear();
        Chapters.Clear();
        SelectedChapter = 0;
        SelectedVerse   = null;
        DisplayedVerses.Clear();
        _chapterVerses.Clear();
    }

    private void RefreshNavigationState()
    {
        OnPropertyChanged(nameof(CanGoPreviousVerse));
        OnPropertyChanged(nameof(CanGoNextVerse));
        PreviousVerseCommand.NotifyCanExecuteChanged();
        NextVerseCommand.NotifyCanExecuteChanged();
    }

    private void NotifyVersionState()
    {
        OnPropertyChanged(nameof(HasVersions));
        OnPropertyChanged(nameof(NoVersionsYet));
    }
}
