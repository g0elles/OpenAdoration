using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public partial class AddEditNoteViewModel : BaseViewModel
{
    private readonly INoteService _noteService;
    private readonly IThemeService _themeService;
    private readonly ILogger<AddEditNoteViewModel> _logger;

    private int _noteId;

    public bool IsNew => _noteId == 0;
    public string FormTitle => IsNew ? L("NoteEdit_FormNew") : L("NoteEdit_FormEdit");

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _content = string.Empty;

    public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];
    [ObservableProperty] private ThemeOption? _selectedTheme;

    public event EventHandler<Note>? Saved;
    public event EventHandler?       Cancelled;

    public AddEditNoteViewModel(
        INoteService noteService,
        IThemeService themeService,
        ILogger<AddEditNoteViewModel> logger)
    {
        _noteService  = noteService;
        _themeService = themeService;
        _logger       = logger;
    }

    /// <summary>Loads the theme picker (default sentinel + all themes) and selects the one matching
    /// <paramref name="selectedThemeId"/>. Called after Initialise* since it hits the DB.</summary>
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
        _noteId = 0;
        Title   = string.Empty;
        Content = string.Empty;
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    public void InitialiseEdit(Note note)
    {
        _noteId = note.Id;
        Title   = note.Title;
        Content = note.Content;
        ClearError();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(FormTitle));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Title))
        {
            SetError(L("NoteEdit_ErrTitleRequired"));
            return;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            SetError(L("NoteEdit_ErrContentRequired"));
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var note = BuildNote();

            if (IsNew)
            {
                var created = await _noteService.CreateAsync(note);
                _logger.LogInformation("Note created: {Title}", created.Title);
                Saved?.Invoke(this, created);
            }
            else
            {
                await _noteService.UpdateAsync(note);
                _logger.LogInformation("Note updated: {NoteId}", _noteId);
                Saved?.Invoke(this, note);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save note");
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

    private Note BuildNote() => new()
    {
        Id      = _noteId,
        Title   = Title.Trim(),
        Content = Content.Trim(),
        ThemeId = SelectedTheme?.Id
    };
}
