using CommunityToolkit.Mvvm.ComponentModel;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public partial class BibleVerseCheckItem : ObservableObject
{
    public BibleVerse Verse { get; init; } = null!;

    [ObservableProperty] private bool _isChecked;
}
