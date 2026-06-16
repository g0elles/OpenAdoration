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

    /// <summary>True only for song items — gates the verse-order override field in the builder.</summary>
    public bool IsSongItem => Item is SongScheduleItem;

    /// <summary>
    /// A scripture item whose version isn't installed (e.g. imported from VideoPsalm as a
    /// reference only — verse text is licensed). The operator must point it at a Bible they
    /// have, or it projects as a bare reference. Drives the builder's "no Bible" warning.
    /// </summary>
    public bool NeedsBibleVersion => Item is BibleScheduleItem { BibleVersionId: null };

    /// <summary>
    /// Operator's chosen replacement version for a reference-only Bible item. Setting it persists
    /// the version on this item in place (same position, book/chapter/verse range) and clears the
    /// "no installed Bible" warning. Bound to the inline picker shown when <see cref="NeedsBibleVersion"/>.
    /// </summary>
    [ObservableProperty] private BibleVersion? _selectedBibleVersion;

    partial void OnSelectedBibleVersionChanged(BibleVersion? value)
    {
        if (value is null || Item is not BibleScheduleItem bibleItem) return;
        bibleItem.BibleVersionId = value.Id;
        bibleItem.BibleVersion = value;
        OnPropertyChanged(nameof(NeedsBibleVersion));
        BibleVersionChangeRequested?.Invoke(this, value.Id);
    }

    // Per-service section order; empty = use the song's own VerseOrder.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVerseOrderHint))]
    [NotifyPropertyChangedFor(nameof(IsVerseOrderReduced))]
    [NotifyPropertyChangedFor(nameof(VerseOrderHint))]
    private string _verseOrderOverride = string.Empty;

    // ── Verse-order override feedback ─────────────────────────────────────────
    // Surfaces how many sections an override actually resolves to, so a token that
    // collapses a multi-verse song to one slide (e.g. "V1") is obvious instead of
    // looking like a dead Next button during projection.
    private Song? OverrideSong => (Item as SongScheduleItem)?.Song;
    private int TotalSectionCount => OverrideSong?.Sections.Count ?? 0;
    private int ResolvedSectionCount => OverrideSong?.GetOrderedSections(VerseOrderOverride).Count ?? 0;

    public bool HasVerseOrderHint =>
        IsSongItem && TotalSectionCount > 0 && !string.IsNullOrWhiteSpace(VerseOrderOverride);

    public bool IsVerseOrderReduced => HasVerseOrderHint && ResolvedSectionCount < TotalSectionCount;

    public string VerseOrderHint =>
        HasVerseOrderHint ? $"Shows {ResolvedSectionCount} of {TotalSectionCount} sections" : string.Empty;

    public event EventHandler? MoveUpRequested;
    public event EventHandler? MoveDownRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? Selected;
    public event EventHandler<int?>? AutoAdvanceChangeRequested;
    public event EventHandler<string?>? VerseOrderOverrideChangeRequested;
    public event EventHandler<int?>? BibleVersionChangeRequested;

    public ScheduleItemViewModel(ScheduleItem item)
    {
        Item = item;
        _autoAdvanceSeconds = item.AutoAdvanceSeconds ?? 0;
        if (item is SongScheduleItem songItem)
            _verseOrderOverride = songItem.VerseOrderOverride ?? string.Empty;
    }

    [RelayCommand]
    private void MoveUp() => MoveUpRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void MoveDown() => MoveDownRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Select() => Selected?.Invoke(this, EventArgs.Empty);

    // Bound with UpdateSourceTrigger=LostFocus, so this fires once the operator
    // finishes editing — persisting the override without a separate Apply button.
    partial void OnVerseOrderOverrideChanged(string value) =>
        VerseOrderOverrideChangeRequested?.Invoke(this, string.IsNullOrWhiteSpace(value) ? null : value.Trim());

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
