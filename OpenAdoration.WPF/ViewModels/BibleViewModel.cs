using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class BibleViewModel : BaseViewModel, IDisposable
{
    private readonly IBibleService           _bibleService;
    private readonly IProjectionService      _projectionService;
    private readonly IBibleImportService     _importService;
    private readonly ILogger<BibleViewModel> _logger;

    private CancellationTokenSource  _booksCts  = new();
    private CancellationTokenSource  _versesCts = new();
    private CancellationTokenSource? _searchCts;

    // Restore state — persists selected location across version switches and reference parses
    private string?      _restoreBookName;
    private int          _restoreChapter;
    private HashSet<int> _restoreVerseNums = new();
    private bool         _hasRestoreTarget;

    // Suppresses OnVerseItemPropertyChanged during bulk selection updates
    private bool _updatingSelection;

    private List<BibleVerse> _chapterVerses = new();
    private List<BibleVerse> _searchResults = new();

    // ── Versions ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleVersion> _versions = new();
    [ObservableProperty] private BibleVersion? _selectedVersion;
    public bool HasVersions   => Versions.Count > 0;
    public bool NoVersionsYet => Versions.Count == 0 && !IsBusy;

    // ── Books ─────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleBook> _books = new();
    [ObservableProperty] private BibleBook? _selectedBook;

    // ── Chapters ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<int> _chapters = new();
    [ObservableProperty] private int _selectedChapter; // 0 = sentinel (G16)

    // ── Verses ────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleVerseCheckItem> _checkableVerses = new();

    // ── Reference bar ─────────────────────────────────────────────────────
    [ObservableProperty] private string _referenceInput = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _bookSuggestions = new();
    public bool HasBookSuggestions => BookSuggestions.Count > 0;

    // ── Slide preview ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlidePreview))]
    private string _slidePreviewText = string.Empty;

    [ObservableProperty] private string _slidePreviewLabel = string.Empty;
    public bool HasSlidePreview => !string.IsNullOrEmpty(SlidePreviewText);

    // ── Mode ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isFrozen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    [NotifyPropertyChangedFor(nameof(ReferenceBarPlaceholder))]
    private bool _isKeywordMode;

    public bool   IsSearchActive         => IsKeywordMode && _searchResults.Count > 0;
    public string ReferenceBarPlaceholder => IsKeywordMode ? "Type keyword to search…" : "e.g. John 3:16-18";

    // ── Import ────────────────────────────────────────────────────────────
    public bool   IsImporting           => _importService.IsImporting;
    public int    ImportProgress        => _importService.Progress;
    public int    ImportTotal           => _importService.Total;
    public string ImportStatusMessage   => _importService.StatusMessage;
    public bool   IsImportIndeterminate => _importService.IsImporting && _importService.Total == 0;
    public bool   CanImport             => !_importService.IsImporting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportSummary))]
    private string _importSummary = string.Empty;
    public bool HasImportSummary => !string.IsNullOrEmpty(ImportSummary);

    // ── Constructor ───────────────────────────────────────────────────────

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

        _importService.StateChanged    += OnImportStateChanged;
        _importService.ImportCompleted += OnImportCompleted;
        _importService.ImportFailed    += OnImportFailed;

        BookSuggestions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasBookSuggestions));
    }

    // ── Import service events ─────────────────────────────────────────────

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

    private void OnImportFailed(object? sender, BibleImportFailedArgs e) => SetError(e.Message);

    // ── Dispose ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _importService.StateChanged    -= OnImportStateChanged;
        _importService.ImportCompleted -= OnImportCompleted;
        _importService.ImportFailed    -= OnImportFailed;

        foreach (var item in CheckableVerses) UnsubscribeItem(item);

        _booksCts.Cancel();  _booksCts.Dispose();
        _versesCts.Cancel(); _versesCts.Dispose();
        _searchCts?.Cancel(); _searchCts?.Dispose();
    }

    // ── Import commands ───────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void ImportVersion(string filePath)
    {
        ImportSummary = string.Empty;
        ClearError();
        _importService.StartImport(filePath);
    }

    [RelayCommand]
    private void CancelImport() => _importService.Cancel();

    // ── Load ──────────────────────────────────────────────────────────────

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

    // ── Version selection ─────────────────────────────────────────────────

    partial void OnSelectedVersionChanged(BibleVersion? value)
    {
        var bookName  = SelectedBook?.Name;
        var chapter   = SelectedChapter;
        var verseNums = GetCheckedVerseNumbers();

        ResetBooksAndBelow();

        if (value is null) return;

        if (bookName is not null)
            SetRestoreTarget(bookName, chapter, verseNums);

        _booksCts.Cancel(); _booksCts.Dispose(); _booksCts = new();
        _ = LoadBooksAndRestoreAsync(value.Id, _booksCts.Token);
    }

    private async Task LoadBooksAndRestoreAsync(int versionId, CancellationToken ct)
    {
        await LoadBooksAsync(versionId, ct);
        if (ct.IsCancellationRequested || !_hasRestoreTarget) return;

        var book = Books.FirstOrDefault(b =>
            b.Name.Equals(_restoreBookName, StringComparison.OrdinalIgnoreCase));
        if (book is null) { _hasRestoreTarget = false; return; }

        SelectedBook = book;
        if (_restoreChapter > 0 && _restoreChapter <= book.ChapterCount)
            SelectedChapter = _restoreChapter;
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

    // ── Book selection ────────────────────────────────────────────────────

    partial void OnSelectedBookChanged(BibleBook? value)
    {
        Chapters.Clear();
        SelectedChapter = 0; // triggers OnSelectedChapterChanged → clears verses

        if (value is null) return;
        for (int c = 1; c <= value.ChapterCount; c++)
            Chapters.Add(c);
    }

    // ── Chapter selection ─────────────────────────────────────────────────

    partial void OnSelectedChapterChanged(int value)
    {
        foreach (var item in CheckableVerses) UnsubscribeItem(item);
        CheckableVerses.Clear();
        _chapterVerses.Clear();
        SlidePreviewText  = string.Empty;
        SlidePreviewLabel = string.Empty;
        RefreshNavState();

        if (value > 0 && SelectedVersion is not null && SelectedBook is not null)
        {
            _versesCts.Cancel(); _versesCts.Dispose(); _versesCts = new();
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
            foreach (var item in CheckableVerses) UnsubscribeItem(item);
            CheckableVerses.Clear();
            foreach (var v in _chapterVerses)
            {
                var item = new BibleVerseCheckItem { Verse = v };
                SubscribeItem(item);
                CheckableVerses.Add(item);
            }

            if (_hasRestoreTarget
                && string.Equals(_restoreBookName, book, StringComparison.OrdinalIgnoreCase)
                && _restoreChapter == chapter)
            {
                _hasRestoreTarget = false;
                ApplyVerseRestore(_restoreVerseNums);
            }
            else
            {
                RebuildPreview();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            _logger.LogError(ex, "Failed to load {Book} {Chapter}", book, chapter);
            SetError("Could not load verses.");
        }
    }

    // ── Verse selection ───────────────────────────────────────────────────

    [RelayCommand]
    private void SelectVerse(BibleVerseCheckItem item)
    {
        _updatingSelection = true;
        try
        {
            foreach (var i in CheckableVerses) i.IsChecked = false;
            item.IsChecked = true;
        }
        finally { _updatingSelection = false; }

        RebuildPreview();
        if (!IsFrozen) ProjectCurrentSelection();
    }

    [RelayCommand]
    private void SelectBookSuggestion(string bookName)
    {
        ReferenceInput = bookName + " ";
        BookSuggestions.Clear();
    }

    // ── Reference parsing ─────────────────────────────────────────────────

    [RelayCommand]
    private void ParseReference()
    {
        if (SelectedVersion is null || Books.Count == 0) return;

        if (IsKeywordMode)
        {
            _ = RunSearchAsync();
            return;
        }

        var parsed = BibleReferenceParser.TryParse(ReferenceInput, Books);
        if (parsed is null) return;

        var book = Books.FirstOrDefault(b =>
            b.Name.Equals(parsed.BookName, StringComparison.OrdinalIgnoreCase));
        if (book is null) return;

        var verseNums = parsed.IsFullChapter
            ? new HashSet<int>()
            : Enumerable.Range(parsed.VerseStart, parsed.VerseEnd - parsed.VerseStart + 1).ToHashSet();

        if (SelectedBook == book && SelectedChapter == parsed.Chapter)
        {
            ApplyVerseRestore(verseNums);
            return;
        }

        SetRestoreTarget(book.Name, parsed.Chapter, verseNums);
        if (SelectedBook != book) SelectedBook = book;
        SelectedChapter = parsed.Chapter;
    }

    [RelayCommand]
    private void ClearReference()
    {
        ReferenceInput = string.Empty;
        BookSuggestions.Clear();

        if (IsKeywordMode)
        {
            _searchResults.Clear();
            RebuildCheckableVersesFromChapter();
            OnPropertyChanged(nameof(IsSearchActive));
        }
    }

    // ── Projection commands ───────────────────────────────────────────────

    [RelayCommand]
    private void ProjectSelected() => ProjectCurrentSelection();

    [RelayCommand]
    private void ExpandSelectionDown()
    {
        var last = LastCheckedIndex();
        var next = last < 0 ? 0 : last + 1;
        if (next >= CheckableVerses.Count) return;
        CheckableVerses[next].IsChecked = true;
    }

    [RelayCommand]
    private void ExpandSelectionUp()
    {
        var last = LastCheckedIndex();
        if (last < 0) return;
        CheckableVerses[last].IsChecked = false;
    }

    private bool CanGoPreviousVerse() => FirstCheckedIndex() > 0;

    [RelayCommand(CanExecute = nameof(CanGoPreviousVerse))]
    private void PreviousVerse()
    {
        var first  = FirstCheckedIndex();
        var target = first <= 0 ? 0 : first - 1;
        SelectVerse(CheckableVerses[target]);
    }

    private bool CanGoNextVerse() =>
        CheckableVerses.Count > 0 && LastCheckedIndex() < CheckableVerses.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoNextVerse))]
    private void NextVerse()
    {
        var last   = LastCheckedIndex();
        var target = last < 0 ? 0 : Math.Min(last + 1, CheckableVerses.Count - 1);
        SelectVerse(CheckableVerses[target]);
    }

    [RelayCommand]
    private void AddSelectedToSchedule()
    {
        // Stub — M4: wire to IWorshipServiceService
    }

    // ── Keyword search ────────────────────────────────────────────────────

    private async Task RunSearchAsync()
    {
        if (SelectedVersion is null || string.IsNullOrWhiteSpace(ReferenceInput))
        {
            ClearReference();
            return;
        }

        _searchCts?.Cancel(); _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsBusy = true;
        ClearError();
        try
        {
            var results = (await _bibleService.SearchAsync(
                SelectedVersion.Id, ReferenceInput, maxResults: 200, ct: ct)).ToList();
            if (ct.IsCancellationRequested) return;

            _searchResults = results;
            foreach (var item in CheckableVerses) UnsubscribeItem(item);
            CheckableVerses.Clear();
            foreach (var v in _searchResults)
            {
                var item = new BibleVerseCheckItem { Verse = v };
                SubscribeItem(item);
                CheckableVerses.Add(item);
            }
            _hasRestoreTarget = false;
            RebuildPreview();
            OnPropertyChanged(nameof(IsSearchActive));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible search failed");
            SetError("Search failed. Please try again.");
        }
        finally { IsBusy = false; }
    }

    // ── Delete version ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteVersionAsync()
    {
        if (SelectedVersion is null || IsBusy) return;

        IsBusy = true;
        ClearError();
        bool ok = false;
        try
        {
            await _bibleService.DeleteVersionAsync(SelectedVersion.Id);
            ok = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Bible version {Id}", SelectedVersion?.Id);
            SetError("Failed to delete version.");
        }
        finally { IsBusy = false; }

        if (ok) await LoadVersionsCoreAsync();
    }

    // ── Reactive property handlers ────────────────────────────────────────

    partial void OnReferenceInputChanged(string value)
    {
        if (IsKeywordMode || Books.Count == 0) { BookSuggestions.Clear(); return; }

        var words = value.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) { BookSuggestions.Clear(); return; }

        var partial_ = string.Join(" ", words.Take(Math.Min(3, words.Length)));
        var sug = BibleReferenceParser.GetSuggestions(partial_, Books, max: 6)
                      .Select(b => b.Name).ToList();
        BookSuggestions.Clear();
        foreach (var s in sug) BookSuggestions.Add(s);
    }

    partial void OnIsFrozenChanged(bool value)
    {
        if (!value) ProjectCurrentSelection();
    }

    partial void OnIsKeywordModeChanged(bool value)
    {
        ReferenceInput = string.Empty;
        _searchResults.Clear();
        BookSuggestions.Clear();

        if (!value)
            RebuildCheckableVersesFromChapter();
    }

    // ── Checkbox subscription helpers ─────────────────────────────────────

    private void SubscribeItem(BibleVerseCheckItem item)
        => item.PropertyChanged += OnVerseItemPropertyChanged;

    private void UnsubscribeItem(BibleVerseCheckItem item)
        => item.PropertyChanged -= OnVerseItemPropertyChanged;

    private void OnVerseItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BibleVerseCheckItem.IsChecked) || _updatingSelection) return;
        RebuildPreview();
        if (!IsFrozen && HasSlidePreview) ProjectCurrentSelection();
    }

    // ── Preview + projection ──────────────────────────────────────────────

    private void RebuildPreview()
    {
        var selected = CheckableVerses.Where(i => i.IsChecked).Select(i => i.Verse).ToList();
        if (selected.Count == 0)
        {
            SlidePreviewText  = string.Empty;
            SlidePreviewLabel = string.Empty;
            RefreshNavState();
            return;
        }
        var slide         = _bibleService.GenerateSlide(selected);
        SlidePreviewText  = slide.Content;
        SlidePreviewLabel = slide.Label;
        RefreshNavState();
    }

    private void ProjectCurrentSelection()
    {
        var selected = CheckableVerses.Where(i => i.IsChecked).Select(i => i.Verse).ToList();
        if (selected.Count == 0) return;
        try
        {
            var slide = _bibleService.GenerateSlide(selected);
            _projectionService.LoadSlides(new[] { slide }, slide.Label);
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to project Bible selection"); }
    }

    // ── Restore helpers ───────────────────────────────────────────────────

    private void SetRestoreTarget(string bookName, int chapter, HashSet<int> verseNums)
    {
        _restoreBookName  = bookName;
        _restoreChapter   = chapter;
        _restoreVerseNums = verseNums;
        _hasRestoreTarget = true;
    }

    private void ApplyVerseRestore(HashSet<int> verseNums)
    {
        _updatingSelection = true;
        try
        {
            foreach (var item in CheckableVerses)
                item.IsChecked = verseNums.Count > 0 && verseNums.Contains(item.Verse.Verse);
        }
        finally { _updatingSelection = false; }

        RebuildPreview();
        if (!IsFrozen && HasSlidePreview)
            ProjectCurrentSelection();
    }

    // ── Index helpers ─────────────────────────────────────────────────────

    private HashSet<int> GetCheckedVerseNumbers()
        => CheckableVerses.Where(i => i.IsChecked).Select(i => i.Verse.Verse).ToHashSet();

    private int FirstCheckedIndex()
    {
        for (int i = 0; i < CheckableVerses.Count; i++)
            if (CheckableVerses[i].IsChecked) return i;
        return -1;
    }

    private int LastCheckedIndex()
    {
        for (int i = CheckableVerses.Count - 1; i >= 0; i--)
            if (CheckableVerses[i].IsChecked) return i;
        return -1;
    }

    private void RebuildCheckableVersesFromChapter()
    {
        foreach (var item in CheckableVerses) UnsubscribeItem(item);
        CheckableVerses.Clear();
        foreach (var v in _chapterVerses)
        {
            var item = new BibleVerseCheckItem { Verse = v };
            SubscribeItem(item);
            CheckableVerses.Add(item);
        }
        RebuildPreview();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void ResetBooksAndBelow()
    {
        foreach (var item in CheckableVerses) UnsubscribeItem(item);
        Books.Clear(); Chapters.Clear();
        SelectedBook    = null;
        SelectedChapter = 0;
        CheckableVerses.Clear();
        _chapterVerses.Clear();
        _searchResults.Clear();
        SlidePreviewText  = string.Empty;
        SlidePreviewLabel = string.Empty;
    }

    private void RefreshNavState()
    {
        PreviousVerseCommand.NotifyCanExecuteChanged();
        NextVerseCommand.NotifyCanExecuteChanged();
    }

    private void NotifyVersionState()
    {
        OnPropertyChanged(nameof(HasVersions));
        OnPropertyChanged(nameof(NoVersionsYet));
    }
}
