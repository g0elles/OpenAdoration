using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.ViewModels;

public partial class ScheduleItemViewModel : ObservableObject
{
    public ScheduleItem Item { get; }

    public string TypeIcon => Item switch
    {
        SongScheduleItem  => "♪",
        BibleScheduleItem => "✦",
        MediaScheduleItem => "▣",
        _                 => "?"
    };

    public string DisplayTitle => Item switch
    {
        SongScheduleItem s  => s.Song?.Title ?? $"Song #{s.SongId}",
        BibleScheduleItem b => b.Reference,
        MediaScheduleItem m => m.MediaFile?.FileName ?? $"Media #{m.MediaFileId}",
        _                   => "Unknown"
    };

    [ObservableProperty] private bool _canMoveUp;
    [ObservableProperty] private bool _canMoveDown;
    [ObservableProperty] private bool _isCurrentLiveItem;

    public event EventHandler? MoveUpRequested;
    public event EventHandler? MoveDownRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? Selected;

    public ScheduleItemViewModel(ScheduleItem item)
    {
        Item = item;
    }

    [RelayCommand]
    private void MoveUp() => MoveUpRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void MoveDown() => MoveDownRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Select() => Selected?.Invoke(this, EventArgs.Empty);
}
