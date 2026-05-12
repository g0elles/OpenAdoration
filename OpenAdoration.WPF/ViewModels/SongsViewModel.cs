using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using System.Collections.ObjectModel;
using System.Windows;

namespace OpenAdoration.WPF.ViewModels;

public partial class SongsViewModel : BaseViewModel
{
    private readonly ISongService _songService;
    private readonly IProjectionService _projectionService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SongsViewModel> _logger;

    // Full list returned from the database — never filtered in place
    private List<Song> _allSongs = new();

    public ObservableCollection<Song> Songs { get; } = new();

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private AddEditSongViewModel? _editViewModel;

    public SongsViewModel(
        ISongService songService,
        IProjectionService projectionService,
        ILoggerFactory loggerFactory,
        ILogger<SongsViewModel> logger)
    {
        _songService       = songService;
        _projectionService = projectionService;
        _loggerFactory     = loggerFactory;
        _logger            = logger;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            _allSongs = (await _songService.GetAllAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load songs");
            SetError("Could not load songs. Check the log for details.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    partial void OnSearchTermChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Songs.Clear();

        var term = SearchTerm.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? _allSongs
            : _allSongs.Where(s => s.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                                || (s.Author?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var song in filtered)
            Songs.Add(song);
    }

    // ── Add / Edit ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddSong()
    {
        var vm = CreateEditViewModel();
        vm.LoadForCreate();
        EditViewModel = vm;
        IsEditing     = true;
    }

    [RelayCommand]
    private void EditSong(Song song)
    {
        var vm = CreateEditViewModel();
        vm.LoadForEdit(song);
        EditViewModel = vm;
        IsEditing     = true;
    }

    private AddEditSongViewModel CreateEditViewModel()
    {
        var vm = new AddEditSongViewModel(_loggerFactory.CreateLogger<AddEditSongViewModel>());
        vm.Saved     += OnSongSaved;
        vm.Cancelled += OnEditCancelled;
        return vm;
    }

    private async void OnSongSaved(object? sender, Song song)
    {
        if (sender is AddEditSongViewModel vm)
        {
            vm.Saved     -= OnSongSaved;
            vm.Cancelled -= OnEditCancelled;
        }

        IsEditing     = false;
        EditViewModel = null;
        ClearError();

        // Persist first — let LoadAsync manage IsBusy independently so its
        // guard (if IsBusy return) is never tripped by our own state here.
        try
        {
            if (song.Id == 0)
                await _songService.CreateAsync(song);
            else
                await _songService.UpdateAsync(song);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save song '{Title}'", song.Title);
            SetError("Could not save the song. Check the log for details.");
            return; // Don't refresh if the write itself failed
        }

        await LoadAsync();
    }

    private void OnEditCancelled(object? sender, EventArgs e)
    {
        if (sender is AddEditSongViewModel vm)
        {
            vm.Saved     -= OnSongSaved;
            vm.Cancelled -= OnEditCancelled;
        }

        IsEditing     = false;
        EditViewModel = null;
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteSongAsync(Song song)
    {
        var result = System.Windows.MessageBox.Show(
            $"Delete \"{song.Title}\"? This cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        ClearError();

        try
        {
            await _songService.DeleteAsync(song.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete song {SongId}", song.Id);
            SetError("Could not delete the song. Check the log for details.");
            return;
        }

        await LoadAsync();
    }

    // ── Projection ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ProjectSong(Song song)
    {
        var slides = _songService.GenerateSlides(song);

        if (slides.Count == 0)
        {
            System.Windows.MessageBox.Show(
                $"\"{song.Title}\" has no sections to project.",
                "Nothing to Project",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _projectionService.LoadSlides(slides, song.Title);
        _logger.LogInformation("Started projection for song {SongId}: {Title}", song.Id, song.Title);
    }
}
