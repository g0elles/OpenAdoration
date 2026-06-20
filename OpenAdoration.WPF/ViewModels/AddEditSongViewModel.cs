using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.ViewModels;

public partial class AddEditSongViewModel : BaseViewModel
{
    private readonly ISongService _songService;
    private readonly IThemeService _themeService;
    private readonly ILogger<AddEditSongViewModel> _logger;

    private int _songId; // 0 = new song

    [ObservableProperty] private string _title          = string.Empty;
    [ObservableProperty] private string _author         = string.Empty;
    [ObservableProperty] private string _classification = string.Empty;
    [ObservableProperty] private string _copyright      = string.Empty;
    [ObservableProperty] private string _ccliNumber     = string.Empty;
    [ObservableProperty] private string _verseOrder     = string.Empty;

    public ObservableCollection<SongSectionViewModel> Sections { get; } = [];

    /// <summary>Theme picker entries; index 0 is the "use default" (null) sentinel.</summary>
    public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];

    [ObservableProperty] private ThemeOption? _selectedTheme;

    public bool   IsNew     => _songId == 0;
    public string FormTitle => IsNew ? L("SongEdit_FormNew") : L("SongEdit_FormEdit");

    public event EventHandler<Song>? Saved;
    public event EventHandler?       Cancelled;

    public AddEditSongViewModel(
        ISongService songService,
        IThemeService themeService,
        ILogger<AddEditSongViewModel> logger)
    {
        _songService  = songService;
        _themeService = themeService;
        _logger       = logger;
    }

    /// <summary>
    /// Loads the theme picker (default sentinel + all themes) and selects the one matching
    /// <paramref name="selectedThemeId"/>. Called after Initialise* since it hits the DB.
    /// </summary>
    public async Task LoadThemesAsync(int? selectedThemeId)
    {
        AvailableThemes.Clear();
        AvailableThemes.Add(new ThemeOption(null, L("SongEdit_ThemeInherit")));
        foreach (var theme in await _themeService.GetAllAsync())
            AvailableThemes.Add(new ThemeOption(theme.Id, theme.Name));
        SelectedTheme = AvailableThemes.FirstOrDefault(o => o.Id == selectedThemeId) ?? AvailableThemes[0];
    }

    public void InitialiseNew()
    {
        _songId        = 0;
        Title          = string.Empty;
        Author         = string.Empty;
        Classification = string.Empty;
        Copyright      = string.Empty;
        CcliNumber     = string.Empty;
        VerseOrder     = string.Empty;
        ClearError();
        ClearSections();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    public void InitialiseEdit(Song song)
    {
        _songId        = song.Id;
        Title          = song.Title;
        Author         = song.Author         ?? string.Empty;
        Classification = song.Classification ?? string.Empty;
        Copyright      = song.Copyright      ?? string.Empty;
        CcliNumber     = song.CcliNumber     ?? string.Empty;
        VerseOrder     = song.VerseOrder     ?? string.Empty;
        ClearError();
        ClearSections();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
        // Edit in definition order — VerseOrder is edited separately and may repeat sections.
        foreach (var s in song.Sections.OrderBy(s => s.Order))
            Sections.Add(CreateSectionVm(s.Type, s.SectionNumber, s.Lyrics, s.Order));
    }

    [RelayCommand]
    private void AddSection(string? sectionTypeName)
    {
        if (!Enum.TryParse<SectionType>(sectionTypeName, out var type))
            return;

        var number = Sections.Count(s => s.Type == type) + 1;
        var order  = Sections.Count;
        Sections.Add(CreateSectionVm(type, number, string.Empty, order));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Title))
        {
            SetError(L("SongEdit_ErrTitleRequired"));
            return;
        }

        if (!Sections.Any())
        {
            SetError(L("SongEdit_ErrSectionRequired"));
            return;
        }

        if (Sections.Any(s => string.IsNullOrWhiteSpace(s.Lyrics)))
        {
            SetError(L("SongEdit_ErrLyricsRequired"));
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            RecalculateOrder();
            var song = BuildSong();

            if (IsNew)
            {
                var created = await _songService.CreateAsync(song);
                _logger.LogInformation("Song created: {Title}", created.Title);
                Saved?.Invoke(this, created);
            }
            else
            {
                await _songService.UpdateAsync(song);
                _logger.LogInformation("Song updated: {SongId}", _songId);
                Saved?.Invoke(this, song);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save song");
            SetError(L("Common_SaveFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearError();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Song BuildSong() => new()
    {
        Id             = _songId,
        Title          = Title.Trim(),
        Author         = string.IsNullOrWhiteSpace(Author)         ? null : Author.Trim(),
        Classification = string.IsNullOrWhiteSpace(Classification) ? null : Classification.Trim(),
        Copyright      = string.IsNullOrWhiteSpace(Copyright)      ? null : Copyright.Trim(),
        CcliNumber     = string.IsNullOrWhiteSpace(CcliNumber)     ? null : CcliNumber.Trim(),
        VerseOrder     = string.IsNullOrWhiteSpace(VerseOrder)     ? null : VerseOrder.Trim(),
        ThemeId        = SelectedTheme?.Id,
        Sections       = [.. Sections.Select(vm => new SongSection
        {
            Type          = vm.Type,
            SectionNumber = vm.SectionNumber,
            Lyrics        = vm.Lyrics,
            Order         = vm.Order
        })]
    };

    private SongSectionViewModel CreateSectionVm(SectionType type, int sectionNumber, string lyrics, int order)
    {
        var vm = new SongSectionViewModel
        {
            Type          = type,
            SectionNumber = sectionNumber,
            Lyrics        = lyrics,
            Order         = order
        };
        vm.MoveUpRequested   += OnMoveUp;
        vm.MoveDownRequested += OnMoveDown;
        vm.DeleteRequested   += OnDelete;
        return vm;
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
        vm.MoveUpRequested   -= OnMoveUp;
        vm.MoveDownRequested -= OnMoveDown;
        vm.DeleteRequested   -= OnDelete;
        Sections.Remove(vm);
        RecalculateOrder();
        RenumberSections();
    }

    private void RecalculateOrder()
    {
        for (int i = 0; i < Sections.Count; i++)
            Sections[i].Order = i;
    }

    private void RenumberSections()
    {
        foreach (var grp in Sections.GroupBy(s => s.Type))
        {
            int n = 1;
            foreach (var vm in grp)
                vm.SectionNumber = n++;
        }
    }

    private void ClearSections()
    {
        foreach (var vm in Sections)
        {
            vm.MoveUpRequested   -= OnMoveUp;
            vm.MoveDownRequested -= OnMoveDown;
            vm.DeleteRequested   -= OnDelete;
        }
        Sections.Clear();
    }
}
