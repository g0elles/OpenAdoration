using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Localization;

namespace OpenAdoration.WPF.ViewModels;

public partial class SongSectionViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [NotifyPropertyChangedFor(nameof(Token))]
    [NotifyPropertyChangedFor(nameof(TypeColorHex))]
    private SectionType _type;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [NotifyPropertyChangedFor(nameof(Token))]
    private int _sectionNumber = 1;

    [ObservableProperty]
    private string _lyrics = string.Empty;

    public int Order { get; set; }

    public string Label => Type switch
    {
        SectionType.Verse  => $"{TranslationSource.Instance["Section_Verse"]} {SectionNumber}",
        SectionType.Bridge => $"{TranslationSource.Instance["Section_Bridge"]} {SectionNumber}",
        _                  => TranslationSource.Instance[$"Section_{Type}"]
    };

    /// <summary>Verse-order token for this section (e.g. "V1", "C", "B"). Matches the VerseOrder string syntax.</summary>
    public string Token => Type switch
    {
        SectionType.Verse     => $"V{SectionNumber}",
        SectionType.Chorus    => $"C{SectionNumber}",
        SectionType.PreChorus => $"P{SectionNumber}",
        SectionType.Bridge    => $"B{SectionNumber}",
        SectionType.Intro     => "I",
        SectionType.Outro     => "O",
        SectionType.Tag       => "T",
        _                     => "?"
    };

    /// <summary>Badge colour by section type — gives the editor a visual cue (VP-style).</summary>
    public string TypeColorHex => Type switch
    {
        SectionType.Verse     => "#7C6AF7",
        SectionType.Chorus    => "#E0A052",
        SectionType.PreChorus => "#52B0E0",
        SectionType.Bridge    => "#9B59B6",
        SectionType.Intro     => "#4CAF50",
        SectionType.Outro     => "#3E8E41",
        SectionType.Tag       => "#E05252",
        _                     => "#A0A0B8"
    };

    public event EventHandler? MoveUpRequested;
    public event EventHandler? MoveDownRequested;
    public event EventHandler? DeleteRequested;

    [RelayCommand]
    private void MoveUp() => MoveUpRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void MoveDown() => MoveDownRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this, EventArgs.Empty);
}
