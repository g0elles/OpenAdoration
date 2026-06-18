using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
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
    private readonly IAppSettingsService     _appSettings;
    private readonly ILogger<BibleViewModel> _logger;

    private CancellationTokenSource  _booksCts  = new();
    private CancellationTokenSource  _versesCts = new();
    private CancellationTokenSource? _searchCts;

    // Restore state — persists selected location across version switches and reference parses
    private string?      _restoreBookName;
    private int          _restoreChapter;
    private HashSet<int> _restoreVerseNums = new();
    private bool         _hasRestoreTarget;

    // Survives scope disposal so the selected version is restored on every navigation back.
    private static int? _lastVersionId;

    // Suppresses OnVerseItemPropertyChanged during bulk selection updates
    private bool _updatingSelection;
    // Guards against syncing the UI during the initial chapter-projection load
    private bool _loadingProjection;
    // True while the full chapter is loaded as individual slides (enables main-window Prev/Next)
    private bool _isChapterProjection;

    private List<BibleVerse> _chapterVerses = new();
    private List<BibleVerse> _secondaryChapterVerses = new();
    private List<BibleVerse> _searchResults = new();

    // ── Versions ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BibleVersion> _versions = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteVersion))]
    private BibleVersion? _selectedVersion;

    // Optional second version stacked under the primary on each slide (M10.3 dual-version).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondaryVersion))]
    private BibleVersion? _secondaryVersion;

    public bool HasSecondaryVersion => SecondaryVersion is not null;

    [RelayCommand]
    private void ClearSecondaryVersion() => SecondaryVersion = null;
    public bool CanDeleteVersion => SelectedVersion is not null;
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
    public bool HasSelection    => CheckableVerses.Any(i => i.IsChecked);
    private void NotifyHasSelection() => OnPropertyChanged(nameof(HasSelection));

    // ── Mode ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isFrozen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    [NotifyPropertyChangedFor(nameof(ReferenceBarPlaceholder))]
    private bool _isKeywordMode;

    // False = keyword (all words, prefix); True = exact phrase. Only relevant in keyword search mode.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReferenceBarPlaceholder))]
    private bool _isPhraseSearch;

    public bool   IsSearchActive         => IsKeywordMode && _searchResults.Count > 0;
    public string ReferenceBarPlaceholder => IsKeywordMode
        ? (IsPhraseSearch ? "Type exact phrase to search…" : "Type keyword(s) to search…")
        : "e.g. John 3:16-18";

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
        IAppSettingsService     appSettings,
        ILogger<BibleViewModel> logger)
    {
        _bibleService      = bibleService;
        _projectionService = projectionService;
        _importService     = importService;
        _appSettings       = appSettings;
        _logger            = logger;

        _importService.StateChanged    += OnImportStateChanged;
        _importService.ImportCompleted += OnImportCompleted;
        _importService.ImportFailed    += OnImportFailed;

        _projectionService.SlideChanged += OnProjectionSlideChanged;
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

        _projectionService.SlideChanged -= OnProjectionSlideChanged;

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
            var preferredId = SelectedVersion?.Id ?? _lastVersionId;
            SelectedVersion = Versions.FirstOrDefault(v => v.Id == preferredId)
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
        _lastVersionId = value?.Id;
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
            var list = await _bibleService.GetBooksAsync(versionId, ct);
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

    // ── Dual-version (M10.3) ──────────────────────────────────────────────

    private bool IsDualVersionActive =>
        SecondaryVersion is not null && SelectedVersion is not null && SecondaryVersion.Id != SelectedVersion.Id;

    private BibleVersion? DualSecondaryVersion => IsDualVersionActive ? SecondaryVersion : null;

    // Only when browsing a loaded chapter — keyword-search results have no paired secondary.
    private IReadOnlyList<BibleVerse>? DualSecondaryVerses =>
        IsDualVersionActive && !IsKeywordMode && _secondaryChapterVerses.Count > 0 ? _secondaryChapterVerses : null;

    partial void OnSecondaryVersionChanged(BibleVersion? value)
    {
        // Reload the current chapter so the secondary text is fetched (or cleared).
        if (SelectedChapter > 0 && SelectedVersion is not null && SelectedBook is not null)
        {
            _versesCts.Cancel(); _versesCts.Dispose(); _versesCts = new();
            _ = LoadVersesAsync(SelectedVersion.Id, SelectedBook.Name, SelectedChapter, _versesCts.Token);
        }
        else
        {
            _secondaryChapterVerses = new();
        }
    }

    // ── Chapter selection ─────────────────────────────────────────────────

    partial void OnSelectedChapterChanged(int value)
    {
        foreach (var item in CheckableVerses) UnsubscribeItem(item);
        CheckableVerses.Clear();
        _chapterVerses.Clear();
        _isChapterProjection = false;
        NotifyHasSelection();
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
            var list = await _bibleService.GetVersesAsync(versionId, book, chapter, ct);
            if (ct.IsCancellationRequested) return;

            _chapterVerses = list.ToList();

            // Dual-version: load the same passage in the secondary version (empty if it
            // lacks this book — e.g. a different book-name spelling, G21 — degrades to primary-only).
            _secondaryChapterVerses = IsDualVersionActive
                ? (await _bibleService.GetVersesAsync(SecondaryVersion!.Id, book, chapter, ct)).ToList()
                : new();
            if (ct.IsCancellationRequested) return;
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

        NotifyHasSelection();
        RefreshNavState();
    }

    [RelayCommand]
    private void SelectChapter(int chapter) => SelectedChapter = chapter;

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
        if (parsed is null)
        {
            // Didn't match a reference — treat as implicit keyword search.
            _ = RunSearchAsync();
            return;
        }

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
            var mode = IsPhraseSearch
                ? OpenAdoration.Application.Common.BibleSearchMode.Phrase
                : OpenAdoration.Application.Common.BibleSearchMode.Keyword;
            var results = (await _bibleService.SearchAsync(
                SelectedVersion.Id, ReferenceInput, mode, maxResults: 200, ct: ct)).ToList();
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
        _isChapterProjection = false;

        if (!value)
            RebuildCheckableVersesFromChapter();
    }

    partial void OnIsPhraseSearchChanged(bool value)
    {
        // Re-run the active keyword search under the new interpretation.
        if (IsKeywordMode && !string.IsNullOrWhiteSpace(ReferenceInput))
            _ = RunSearchAsync();
    }

    // ── Checkbox subscription helpers ─────────────────────────────────────

    private void SubscribeItem(BibleVerseCheckItem item)
        => item.PropertyChanged += OnVerseItemPropertyChanged;

    private void UnsubscribeItem(BibleVerseCheckItem item)
        => item.PropertyChanged -= OnVerseItemPropertyChanged;

    private void OnVerseItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BibleVerseCheckItem.IsChecked) || _updatingSelection) return;
        NotifyHasSelection();
        RefreshNavState();
    }

    // ── Preview + projection ──────────────────────────────────────────────

    private void RebuildPreview()
    {
        NotifyHasSelection();
        RefreshNavState();
    }

    private void ProjectCurrentSelection()
    {
        var selected = CheckableVerses.Where(i => i.IsChecked).Select(i => i.Verse).ToList();
        if (selected.Count == 0) return;
        try
        {
            if (!IsKeywordMode && _chapterVerses.Count > 0 && selected.Count == 1)
            {
                // Single verse in chapter mode: load the full chapter as individual slides so the
                // main-window ◀/▶ can navigate verse-by-verse — same as songs navigate section-by-section.
                var secondary = DualSecondaryVerses;
                var slides   = _chapterVerses.Select(v => _bibleService.GenerateSlide(
                                   new[] { v }, version: SelectedVersion,
                                   secondaryVerses: secondary?.Where(s => s.Verse == v.Verse).ToList(),
                                   secondaryVersion: DualSecondaryVersion)).ToArray();
                var label    = $"{selected[0].Book} {selected[0].Chapter}";
                var startIdx = _chapterVerses.FindIndex(v => v.Verse == selected[0].Verse);
                if (startIdx < 0) startIdx = 0;

                _loadingProjection   = true;
                _isChapterProjection = true;
                try
                {
                    _projectionService.LoadSlides(slides, label);
                    if (startIdx > 0) _projectionService.GoTo(startIdx);
                    SlidePreviewText  = slides[startIdx].Content;
                    SlidePreviewLabel = slides[startIdx].Label;
                }
                finally { _loadingProjection = false; }
            }
            else
            {
                // Multi-verse selection or keyword search: chunk by the configured verses-per-slide.
                _isChapterProjection = false;
                var versesPerSlide = Math.Max(1, _appSettings.Current.DefaultBibleVersesPerSlide);
                var slides = _bibleService.GenerateSlides(selected, versesPerSlide, version: SelectedVersion,
                                 secondaryVerses: DualSecondaryVerses, secondaryVersion: DualSecondaryVersion);
                _projectionService.LoadSlides(slides, slides[0].Label);
                SlidePreviewText  = slides[0].Content;
                SlidePreviewLabel = slides[0].Label;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to project Bible selection"); }
    }

    private void OnProjectionSlideChanged(object? sender, Slide? slide)
    {
        // Sync the verse list UI when the main-window Prev/Next navigates within the chapter.
        if (_loadingProjection || !_isChapterProjection || slide is null) return;

        var idx = _projectionService.CurrentSlideIndex;
        if (idx < 0 || idx >= _chapterVerses.Count) return;

        var targetVerse = _chapterVerses[idx];
        var item = CheckableVerses.FirstOrDefault(i => i.Verse.Verse == targetVerse.Verse);
        if (item is null) return;

        _updatingSelection = true;
        try
        {
            foreach (var i in CheckableVerses) i.IsChecked = false;
            item.IsChecked = true;
        }
        finally { _updatingSelection = false; }

        NotifyHasSelection();
        SlidePreviewText  = slide.Content;
        SlidePreviewLabel = slide.Label;
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

        NotifyHasSelection();
        RefreshNavState();
        if (!IsFrozen && CheckableVerses.Any(i => i.IsChecked))
            ProjectCurrentSelection();
    }

    // ── Index helpers ─────────────────────────────────────────────────────

    private HashSet<int> GetCheckedVerseNumbers()
        => CheckableVerses.Where(i => i.IsChecked).Select(i => i.Verse.Verse).ToHashSet();

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
        NotifyHasSelection();
        RefreshNavState();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void ResetBooksAndBelow()
    {
        foreach (var item in CheckableVerses) UnsubscribeItem(item);
        Books.Clear(); Chapters.Clear();
        SelectedBook         = null;
        SelectedChapter      = 0;
        CheckableVerses.Clear();
        _chapterVerses.Clear();
        _searchResults.Clear();
        _isChapterProjection = false;
        SlidePreviewText     = string.Empty;
        SlidePreviewLabel    = string.Empty;
    }

    private void RefreshNavState() { }

    private void NotifyVersionState()
    {
        OnPropertyChanged(nameof(HasVersions));
        OnPropertyChanged(nameof(NoVersionsYet));
    }
}
