using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

/// <summary>Notes/Sermon library: browse, search, create/edit/delete, and project notes.
/// Mirrors <see cref="SongsViewModel"/> — Notes is a real library like Songs, not a bare
/// reference like Bible.</summary>
public partial class NotesViewModel : BaseViewModel, IDisposable
{
    private readonly INoteService           _noteService;
    private readonly IProjectionService     _projectionService;
    private readonly IDialogService         _dialogService;
    private readonly INoteLibraryNotifier   _noteNotifier;
    private readonly IAppSettingsService    _appSettings;
    private readonly IStageNavigationService _stageNavigation;
    private readonly ILogger<NotesViewModel> _logger;

    [ObservableProperty] private ObservableCollection<Note> _notes = [];
    [ObservableProperty] private Note? _selectedNote;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    public AddEditNoteViewModel EditViewModel { get; }

    private CancellationTokenSource? _searchDebounceCts;

    public NotesViewModel(
        INoteService             noteService,
        IProjectionService       projectionService,
        IDialogService           dialogService,
        INoteLibraryNotifier     noteNotifier,
        AddEditNoteViewModel     editViewModel,
        IAppSettingsService      appSettings,
        IStageNavigationService  stageNavigation,
        ILogger<NotesViewModel>  logger)
    {
        _noteService       = noteService;
        _projectionService = projectionService;
        _dialogService     = dialogService;
        _noteNotifier      = noteNotifier;
        _appSettings       = appSettings;
        _stageNavigation   = stageNavigation;
        _logger            = logger;
        EditViewModel      = editViewModel;

        EditViewModel.Saved     += OnNoteSaved;
        EditViewModel.Cancelled += OnEditCancelled;
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchDebounceCts.Token);
    }

    private async Task DebounceSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        SearchCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var list = string.IsNullOrWhiteSpace(SearchText)
                ? await _noteService.GetAllAsync()
                : await _noteService.SearchByTitleAsync(SearchText);
            Notes = new ObservableCollection<Note>(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notes");
            SetError(L("Notes_ErrLoad"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        SelectedNote = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NewNote()
    {
        SelectedNote = null;
        EditViewModel.InitialiseNew();
        IsEditing = true;
        await EditViewModel.LoadThemesAsync(null);
    }

    [RelayCommand]
    private async Task EditNote(Note note)
    {
        SelectedNote = note;
        EditViewModel.InitialiseEdit(note);
        IsEditing = true;
        await EditViewModel.LoadThemesAsync(note.ThemeId);
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(Note note)
    {
        if (!_dialogService.Confirm(L("Notes_ConfirmDelete", note.Title), L("Notes_DeleteTitle")))
            return;

        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            await _noteService.DeleteAsync(note.Id);
            _logger.LogInformation("Note deleted: {NoteId}", note.Id);
            if (SelectedNote?.Id == note.Id) SelectedNote = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete note {NoteId}", note.Id);
            SetError(L("Notes_ErrDelete"));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private void ProjectNote(Note note)
    {
        var slides = _noteService.GenerateSlides(note, ThemeCascade.ForNotes(null, note.ThemeId, _appSettings.Current));
        if (slides.Count == 0)
        {
            SetError(L("Sched_ErrNoNotesContent"));
            return;
        }
        _projectionService.LoadSlides(slides, note.Title, ProjectionContextKeys.Notes(note.Id));
        UpdateStandaloneQueue(note);
        _logger.LogInformation("Projecting note: {Title}", note.Title);
        _stageNavigation.NavigateToStage();
    }

    // Standalone (non-service) projection has no built-in "next/previous item" — feed the projector
    // the full displayed list as a browsable queue, mirrors SongsViewModel.UpdateStandaloneQueue.
    private void UpdateStandaloneQueue(Note current)
    {
        if (_projectionService.IsServiceScheduleActive) return;

        var items = BuildStandaloneQueueItems();
        var currentIndex = Math.Max(items.FindIndex(i => i.ContextKey == ProjectionContextKeys.Notes(current.Id)), 0);
        _projectionService.SetStandaloneQueue(items, currentIndex);

        var nextIndex = currentIndex + 1;
        _projectionService.SetNextScheduleItemPreview(
            nextIndex < items.Count ? items[nextIndex].Slides[0] : null);
    }

    private List<StandaloneQueueItem> BuildStandaloneQueueItems() =>
        Notes.Select(n => new StandaloneQueueItem(
                _noteService.GenerateSlides(n, ThemeCascade.ForNotes(null, n.ThemeId, _appSettings.Current)),
                n.Title,
                ProjectionContextKeys.Notes(n.Id)))
             .Where(i => i.Slides.Count > 0)
             .ToList();

    private async void OnNoteSaved(object? sender, Note note)
    {
        try
        {
            IsEditing = false;
            UpdateLiveProjection(note);
            _noteNotifier.NotifyNoteSaved(note.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Note-saved handler failed for {NoteId}", note.Id);
        }
    }

    // If this exact note is on the projector standalone right now, push the edits live.
    // Service-driven projection is handled by ServiceScheduleViewModel via INoteLibraryNotifier.
    private void UpdateLiveProjection(Note note)
    {
        if (!_projectionService.IsProjecting) return;
        var slides = _noteService.GenerateSlides(note, ThemeCascade.ForNotes(null, note.ThemeId, _appSettings.Current));
        if (_projectionService.TryUpdateSlides(ProjectionContextKeys.Notes(note.Id), slides, note.Title))
            _logger.LogInformation("Live-updated projection for edited note {NoteId}", note.Id);
    }

    private void OnEditCancelled(object? sender, EventArgs e)
    {
        IsEditing = false;
    }

    public void Dispose()
    {
        EditViewModel.Saved     -= OnNoteSaved;
        EditViewModel.Cancelled -= OnEditCancelled;
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
    }
}
