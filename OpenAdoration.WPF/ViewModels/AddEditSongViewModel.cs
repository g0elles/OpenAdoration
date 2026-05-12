using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using System.Collections.ObjectModel;

namespace OpenAdoration.WPF.ViewModels;

public partial class AddEditSongViewModel : ObservableObject
{
    private readonly ILogger<AddEditSongViewModel> _logger;

    // 0 = new song
    private int _songId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    public ObservableCollection<SongSectionViewModel> Sections { get; } = new();

    public event EventHandler<Song>? Saved;
    public event EventHandler?       Cancelled;

    public AddEditSongViewModel(ILogger<AddEditSongViewModel> logger)
    {
        _logger = logger;
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    public void LoadForCreate()
    {
        _songId = 0;
        Title   = string.Empty;
        Author  = string.Empty;
        Sections.Clear();
    }

    public void LoadForEdit(Song song)
    {
        _songId = song.Id;
        Title   = song.Title;
        Author  = song.Author ?? string.Empty;

        Sections.Clear();
        foreach (var section in song.GetOrderedSections())
        {
            var vm = SongSectionViewModel.FromEntity(section);
            SubscribeSectionEvents(vm);
            Sections.Add(vm);
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    // CommandParameter from each "Add X" button is the SectionType enum name as a string.
    [RelayCommand]
    private void AddSection(string? sectionTypeName)
    {
        if (!Enum.TryParse<SectionType>(sectionTypeName, out var type))
        {
            _logger.LogWarning("Unknown section type name '{Name}' — defaulting to Verse", sectionTypeName);
            type = SectionType.Verse;
        }

        var vm = new SongSectionViewModel { Type = type };
        SubscribeSectionEvents(vm);
        Sections.Add(vm);
        RecalculateOrder();

        _logger.LogDebug("Added {Type} section to song editor", type);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var song = BuildEntity();

        if (_songId == 0)
            _logger.LogInformation("Saving new song: {Title}", Title);
        else
            _logger.LogInformation("Saving updated song {SongId}: {Title}", _songId, Title);

        Saved?.Invoke(this, song);
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    // ── Section event handlers ────────────────────────────────────────────────

    private void SubscribeSectionEvents(SongSectionViewModel vm)
    {
        vm.MoveUpRequested   += OnMoveUp;
        vm.MoveDownRequested += OnMoveDown;
        vm.DeleteRequested   += OnDelete;
    }

    private void UnsubscribeSectionEvents(SongSectionViewModel vm)
    {
        vm.MoveUpRequested   -= OnMoveUp;
        vm.MoveDownRequested -= OnMoveDown;
        vm.DeleteRequested   -= OnDelete;
    }

    private void OnMoveUp(object? sender, EventArgs e)
    {
        if (sender is not SongSectionViewModel vm) return;
        var idx = Sections.IndexOf(vm);
        if (idx <= 0) return;

        Sections.Move(idx, idx - 1);
        RecalculateOrder();
    }

    private void OnMoveDown(object? sender, EventArgs e)
    {
        if (sender is not SongSectionViewModel vm) return;
        var idx = Sections.IndexOf(vm);
        if (idx < 0 || idx >= Sections.Count - 1) return;

        Sections.Move(idx, idx + 1);
        RecalculateOrder();
    }

    private void OnDelete(object? sender, EventArgs e)
    {
        if (sender is not SongSectionViewModel vm) return;
        UnsubscribeSectionEvents(vm);
        Sections.Remove(vm);
        RecalculateOrder();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Re-assigns Order and SectionNumber so labels stay correct after moves/deletes
    private void RecalculateOrder()
    {
        var counters = new Dictionary<SectionType, int>();

        for (var i = 0; i < Sections.Count; i++)
        {
            var vm = Sections[i];
            vm.Order = i;

            counters.TryGetValue(vm.Type, out var count);
            count++;
            counters[vm.Type] = count;
            vm.SectionNumber  = count;
        }
    }

    private Song BuildEntity()
    {
        var sections = Sections
            .Select(vm => vm.ToEntity())
            .ToList();

        return new Song
        {
            Id       = _songId,
            Title    = Title.Trim(),
            Author   = string.IsNullOrWhiteSpace(Author) ? null : Author.Trim(),
            Sections = sections
        };
    }
}
