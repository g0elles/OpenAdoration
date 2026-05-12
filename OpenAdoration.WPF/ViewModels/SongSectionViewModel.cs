using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.ViewModels;

public partial class SongSectionViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private SectionType _type;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private int _sectionNumber = 1;

    [ObservableProperty]
    private string _lyrics = string.Empty;

    public int Order { get; set; }

    public string Label => Type switch
    {
        SectionType.Verse  => $"Verse {SectionNumber}",
        SectionType.Bridge => $"Bridge {SectionNumber}",
        _                  => Type.ToString()
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
