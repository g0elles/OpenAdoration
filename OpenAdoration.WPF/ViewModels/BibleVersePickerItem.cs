using CommunityToolkit.Mvvm.ComponentModel;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public partial class BibleVersePickerItem : ObservableObject
{
    public BibleVerse Verse { get; init; } = null!;
    public int    Number  => Verse.Verse;
    public string Preview => Verse.Text.Length > 80 ? Verse.Text[..80] + "…" : Verse.Text;

    [ObservableProperty] private bool _isInRange;
}
