using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public partial class SongsViewModel : BaseViewModel
{
    private readonly ISongService       _songService;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<SongsViewModel> _logger;

    // Child VM — shares the same DI scope, created once per navigation to Songs
    public AddEditSongViewModel EditViewModel { get; }

    [ObservableProperty] private ObservableCollection<Song> _songs = [];
    [ObservableProperty] private Song?   _selectedSong;
    [ObservableProperty] private string  _searchText = string.Empty;
    [ObservableProperty] private bool    _isEditing;

    public SongsViewModel(
        ISongService            songService,
        IProjectionService      projectionService,
        AddEditSongViewModel    editViewModel,
        ILogger<SongsViewModel> logger)
    {
        _songService       = songService;
        _projectionService = projectionService;
        _logger            = logger;
        EditViewModel      = editViewModel;

        EditViewModel.Saved     += OnSongSaved;
        EditViewModel.Cancelled += OnEditCancelled;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            var list = string.IsNullOrWhiteSpace(SearchText)
                ? await _songService.GetAllAsync()
                : await _songService.SearchByTitleAsync(SearchText);

            Songs = new ObservableCollection<Song>(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load songs");
            SetError("Failed to load songs.");
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
            SetError("Failed to delete song.");
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
    private void ProjectSong(Song song)
    {
        var slides = _songService.GenerateSlides(song);
        if (slides.Count == 0)
        {
            SetError("This song has no lyrics to project.");
            return;
        }
        _projectionService.LoadSlides(slides, song.Title);
        _logger.LogInformation("Projecting song: {Title}", song.Title);
    }

    // ── Event handlers from EditViewModel ─────────────────────────────────────

    private async void OnSongSaved(object? sender, Song song)
    {
        IsEditing = false;
        await LoadAsync();
    }

    private void OnEditCancelled(object? sender, EventArgs e)
    {
        IsEditing = false;
    }
}
