using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers.SongImport;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class SongsViewModel : BaseViewModel, IDisposable
{
    private readonly ISongService            _songService;
    private readonly IProjectionService      _projectionService;
    private readonly IDialogService          _dialogService;
    private readonly ISongLibraryNotifier    _songNotifier;
    private readonly ILogger<SongsViewModel> _logger;

    // Child VM -- shares the same DI scope, created once per navigation to Songs
    public AddEditSongViewModel EditViewModel { get; }

    [ObservableProperty] private ObservableCollection<Song> _songs = [];
    [ObservableProperty] private Song?   _selectedSong;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;
    [ObservableProperty] private bool _isEditing;

    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    // Debounce token -- replaced on every keystroke; old delay task cancels itself (R5)
    private CancellationTokenSource? _searchDebounceCts;

    public SongsViewModel(
        ISongService            songService,
        IProjectionService      projectionService,
        IDialogService          dialogService,
        ISongLibraryNotifier    songNotifier,
        AddEditSongViewModel    editViewModel,
        ILogger<SongsViewModel> logger)
    {
        _songService       = songService;
        _projectionService = projectionService;
        _dialogService     = dialogService;
        _songNotifier      = songNotifier;
        _logger            = logger;
        EditViewModel      = editViewModel;

        EditViewModel.Saved     += OnSongSaved;
        EditViewModel.Cancelled += OnEditCancelled;
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce: wait 300 ms after the last keystroke before firing the search.
        // Cancels the previous pending delay each time a new character arrives (R5).
        // Cancel and dispose the old CTS before replacing it.  Not disposing is a
        // small resource leak that accumulates per keystroke (P3).
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchDebounceCts.Token);
    }

    private async Task DebounceSearchAsync(CancellationToken ct)
    {
        await Task.Delay(300);
        if (!ct.IsCancellationRequested)
            SearchCommand.Execute(null);
    }

    // -- Commands --------------------------------------------------------------

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            IReadOnlyList<Song> list;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                list = await _songService.GetAllAsync();
            }
            else
            {
                // Two-step: title/author match first (fast); fall back to lyrics FTS only when empty.
                list = await _songService.SearchByTitleAsync(SearchText);
                if (list.Count == 0)
                    list = await _songService.SearchByLyricsAsync(SearchText);
            }

            Songs = new ObservableCollection<Song>(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load songs");
            SetError(L("Songs_ErrLoad"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        SelectedSong = null;
        await LoadAsync();
    }

    [RelayCommand]
    private void NewSong()
    {
        SelectedSong = null;
        EditViewModel.InitialiseNew();
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSong(Song song)
    {
        SelectedSong = song;
        EditViewModel.InitialiseEdit(song);
        IsEditing = true;
    }

    [RelayCommand]
    private async Task DeleteSongAsync(Song song)
    {
        if (!_dialogService.Confirm(
                L("Songs_ConfirmDelete", song.Title),
                L("Songs_DeleteTitle")))
            return;

        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            await _songService.DeleteAsync(song.Id);
            _logger.LogInformation("Song deleted: {SongId}", song.Id);
            if (SelectedSong?.Id == song.Id) SelectedSong = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete song {SongId}", song.Id);
            SetError(L("Songs_ErrDelete"));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        // Only reached on success (IsBusy already reset by finally)
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ImportSongAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = L("Songs_ImportTitle"),
            Filter = SongFormatDispatcher.FileDialogFilter,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        if (IsBusy) return;
        IsBusy = true;
        ClearError();

        int imported;
        try
        {
            var songs = SongFormatDispatcher.ImportMany(dialog.FileName);
            foreach (var song in songs)
                await _songService.CreateAsync(song);
            imported = songs.Count;
            _logger.LogInformation("Imported {Count} song(s) from {File}", imported, dialog.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import song from {File}", dialog.FileName);
            _dialogService.Inform(
                L("Songs_ImportFailed"),
                L("Songs_ImportResultTitle"));
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // Reached only on success — IsBusy already reset by finally, so LoadAsync can run (G4).
        await LoadAsync();
        _dialogService.Inform(
            imported == 1 ? L("Songs_ImportedOne") : L("Songs_ImportedMany", imported),
            L("Songs_ImportResultTitle"));
    }

    [RelayCommand]
    private void ProjectSong(Song song)
    {
        var slides = _songService.GenerateSlides(song);
        if (slides.Count == 0)
        {
            SetError(L("Sched_ErrNoLyrics"));
            return;
        }
        _projectionService.LoadSlides(slides, song.Title, ProjectionContextKeys.Song(song.Id));
        _logger.LogInformation("Projecting song: {Title}", song.Title);
    }

    // -- Event handlers from EditViewModel -------------------------------------

    private async void OnSongSaved(object? sender, Song song)
    {
        IsEditing = false;
        UpdateLiveProjection(song);
        // Tell other live consumers (e.g. a service projecting this song) to re-render too.
        _songNotifier.NotifySongSaved(song.Id);
        await LoadAsync();
    }

    // If this exact song is on the projector standalone right now, push the edits live
    // (G9 subscribers re-render). Service-driven projection is handled by ServiceScheduleViewModel
    // via ISongLibraryNotifier, since it must re-apply the item's theme + verse-order override.
    private void UpdateLiveProjection(Song song)
    {
        if (!_projectionService.IsProjecting) return;
        var slides = _songService.GenerateSlides(song);
        if (_projectionService.TryUpdateSlides(ProjectionContextKeys.Song(song.Id), slides, song.Title))
            _logger.LogInformation("Live-updated projection for edited song {SongId}", song.Id);
    }

    private void OnEditCancelled(object? sender, EventArgs e)
    {
        IsEditing = false;
    }

    public void Dispose()
    {
        EditViewModel.Saved     -= OnSongSaved;
        EditViewModel.Cancelled -= OnEditCancelled;
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
    }
}
