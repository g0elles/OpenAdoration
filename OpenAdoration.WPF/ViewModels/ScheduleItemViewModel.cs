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

    // 0 = manual (off); positive value = seconds between slide advances
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutoAdvance))]
    [NotifyPropertyChangedFor(nameof(AutoAdvanceLabel))]
    private int _autoAdvanceSeconds;

    public bool   HasAutoAdvance    => AutoAdvanceSeconds > 0;
    public string AutoAdvanceLabel  => AutoAdvanceSeconds > 0 ? $"{AutoAdvanceSeconds}s" : "Manual";

    public event EventHandler? MoveUpRequested;
    public event EventHandler? MoveDownRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? Selected;
    public event EventHandler<int?>? AutoAdvanceChangeRequested;

    public ScheduleItemViewModel(ScheduleItem item)
    {
        Item = item;
        _autoAdvanceSeconds = item.AutoAdvanceSeconds ?? 0;
    }

    [RelayCommand]
    private void MoveUp() => MoveUpRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void MoveDown() => MoveDownRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Select() => Selected?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void IncreaseAutoAdvance()
    {
        AutoAdvanceSeconds = AutoAdvanceSeconds == 0 ? 5 : Math.Min(AutoAdvanceSeconds + 5, 300);
        AutoAdvanceChangeRequested?.Invoke(this, AutoAdvanceSeconds);
    }

    [RelayCommand]
    private void DecreaseAutoAdvance()
    {
        if (AutoAdvanceSeconds <= 0) return;
        AutoAdvanceSeconds = Math.Max(AutoAdvanceSeconds - 5, 0);
        AutoAdvanceChangeRequested?.Invoke(this, AutoAdvanceSeconds == 0 ? null : AutoAdvanceSeconds);
    }
}
