using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers.BibleImport;

namespace OpenAdoration.WPF.ViewModels;

public partial class BibleViewModel : BaseViewModel
{
    private readonly IBibleService      _bibleService;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<BibleViewModel> _logger;

    // ── Versions ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleVersion> _versions = new();
    [ObservableProperty] private BibleVersion? _selectedVersion;

    public bool HasVersions   => Versions.Count > 0;
    public bool NoVersionsYet => Versions.Count == 0 && !IsBusy;

    // ── Books ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleBook> _books = new();
    [ObservableProperty] private BibleBook? _selectedBook;

    // ── Chapters ──────────────────────────────────────────────────────────────
    // Generated client-side from BibleBook.ChapterCount — no DB call needed.
    [ObservableProperty] private ObservableCollection<int> _chapters = new();

    // 0 = nothing selected (never stored in the collection, so ListBox deselects)
    [ObservableProperty] private int _selectedChapter;

    // ── Verses ────────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleVerse> _displayedVerses = new();
    [ObservableProperty] private BibleVerse? _selectedVerse;

    private List<BibleVerse> _chapterVerses = new(); // cache for clearing search

    // ── Search ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    private string _searchText = string.Empty;

    private List<BibleVerse> _searchResults = new();
    public  bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText) && _searchResults.Count > 0;

    // ── Import progress ───────────────────────────────────────────────────────
    [ObservableProperty] private string _importStatus = string.Empty;
    [ObservableProperty] private bool   _isImporting;

    // ── Projection state ──────────────────────────────────────────────────────
    public bool IsProjecting       => _projectionService.IsProjecting;
    public bool CanGoPreviousVerse => CurrentVerseIndex > 0;
    public bool CanGoNextVerse     => CurrentVerseIndex >= 0
                                   && CurrentVerseIndex < DisplayedVerses.Count - 1;

    private int CurrentVerseIndex =>
        SelectedVerse is null ? -1 : DisplayedVerses.IndexOf(SelectedVerse);

    // ── Constructor ───────────────────────────────────────────────────────────

    public BibleViewModel(
        IBibleService      bibleService,
        IProjectionService projectionService,
        ILogger<BibleViewModel> logger)
    {
        _bibleService      = bibleService;
        _projectionService = projectionService;
        _logger            = logger;

        _projectionService.ProjectionStateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsProjecting));
            RefreshNavigationState();
        };
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();
        try
        {
            var list = await _bibleService.GetVersionsAsync();

            Versions.Clear();
            foreach (var v in list) Versions.Add(v);

            NotifyVersionState();

            // Preserve the previously-selected version if it still exists,
            // otherwise default to the first one.
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
            NotifyVersionState(); // ensure NoVersionsYet reflects IsBusy = false
        }
    }

    // ── Version selection ─────────────────────────────────────────────────────

    partial void OnSelectedVersionChanged(BibleVersion? value)
    {
        ResetBooksAndBelow();
        if (value is not null)
            _ = LoadBooksAsync(value.Id);
    }

    private async Task LoadBooksAsync(int versionId)
    {
        try
        {
            var list = await _bibleService.GetBooksAsync(versionId);
            Books.Clear();
            foreach (var b in list) Books.Add(b);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load books for version {Id}", versionId);
            SetError("Could not load books.");
        }
    }

    // ── Book selection ────────────────────────────────────────────────────────

    partial void OnSelectedBookChanged(BibleBook? value)
    {
        Chapters.Clear();
        SelectedChapter = 0; // triggers OnSelectedChapterChanged(0) → clears verses
        SelectedVerse   = null;

        if (value is null) return;

        for (int c = 1; c <= value.ChapterCount; c++)
            Chapters.Add(c);
    }

    // ── Chapter selection ─────────────────────────────────────────────────────

    partial void OnSelectedChapterChanged(int value)
    {
        DisplayedVerses.Clear();
        _chapterVerses.Clear();
        SelectedVerse = null;

        if (value > 0 && SelectedVersion is not null && SelectedBook is not null)
            _ = LoadVersesAsync(SelectedVersion.Id, SelectedBook.Name, value);
    }

    private async Task LoadVersesAsync(int versionId, string book, int chapter)
    {
        try
        {
            var list = await _bibleService.GetVersesAsync(versionId, book, chapter);
            _chapterVerses = list.ToList();

            DisplayedVerses.Clear();
            foreach (var v in _chapterVerses) DisplayedVerses.Add(v);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load {Book} {Chapter}", book, chapter);
            SetError("Could not load verses.");
        }
    }

    // ── Verse projection — single-click to project ────────────────────────────

    partial void OnSelectedVerseChanged(BibleVerse? value)
    {
        RefreshNavigationState();

        if (value is null) return;

        try
        {
            var slide = _bibleService.GenerateSlide(new[] { value });
            _projectionService.LoadSlides(new[] { slide }, value.Reference);
            OnPropertyChanged(nameof(IsProjecting));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to project verse {Ref}", value.Reference);
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

    [RelayCommand]
    private void ShowBlank() => _projectionService.ShowBlank();

    [RelayCommand]
    private void StopProjection()
    {
        _projectionService.Stop();
        SelectedVerse = null;
        OnPropertyChanged(nameof(IsProjecting));
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (SelectedVersion is null || string.IsNullOrWhiteSpace(SearchText))
        {
            ClearSearch();
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            _searchResults = (await _bibleService.SearchAsync(SelectedVersion.Id, SearchText, maxResults: 200)).ToList();

            DisplayedVerses.Clear();
            foreach (var v in _searchResults) DisplayedVerses.Add(v);
            OnPropertyChanged(nameof(IsSearchActive));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible search failed: '{Term}'", SearchText);
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

    // ── Import ────────────────────────────────────────────────────────────────

    /// <summary>Called from BibleView code-behind with the path chosen via OpenFileDialog.</summary>
    [RelayCommand]
    private async Task ImportVersionAsync(string filePath)
    {
        if (IsBusy) return;

        IsImporting   = true;
        ImportStatus  = "Detecting format…";
        IsBusy        = true;
        ClearError();

        try
        {
            ImportStatus = "Parsing file…";
            var (version, books, verses) = BibleFormatDispatcher.Import(filePath);

            ImportStatus = $"Importing {verses.Count:N0} verses…";
            await _bibleService.ImportVersionAsync(version, books, verses);

            _logger.LogInformation("Bible imported: {Name} ({Books} books, {Verses} verses)",
                version.Name, books.Count, verses.Count);

            ImportStatus = string.Empty;
            await LoadAsync();
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Bible import — bad JSON format");
            SetError($"Invalid file format: {ex.Message}");
            ImportStatus = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible import failed");
            SetError($"Import failed: {ex.Message}");
            ImportStatus = string.Empty;
        }
        finally
        {
            IsImporting = false;
            IsBusy      = false;
        }
    }

    // ── Delete version ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteVersionAsync()
    {
        if (SelectedVersion is null || IsBusy) return;

        IsBusy = true;
        ClearError();
        try
        {
            await _bibleService.DeleteVersionAsync(SelectedVersion.Id);
            await LoadAsync();
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
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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
