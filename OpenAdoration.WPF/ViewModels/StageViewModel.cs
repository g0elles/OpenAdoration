using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.Helpers;
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

/// <summary>Immutable rendering snapshot for one slide in the stage view.</summary>
public sealed record SlidePreview
{
    public static readonly SlidePreview Empty = new();

    public string  Content       { get; init; } = string.Empty;
    public string  SectionLabel  { get; init; } = string.Empty;

    // Layout flags (mutually exclusive)
    public bool IsBlank      { get; init; }
    public bool IsText       { get; init; }   // Song or Bible
    public bool IsImageMedia { get; init; }
    public bool IsVideoMedia { get; init; }

    // Media
    public string? MediaPath { get; init; }

    // Theme colours (WPF Media.Color so ColorToBrush converter works directly)
    public System.Windows.Media.Color BgColor   { get; init; } = System.Windows.Media.Colors.Black;
    public System.Windows.Media.Color FontColor { get; init; } = System.Windows.Media.Colors.White;
    public string? BgImagePath { get; init; }
    public bool    HasBgImage  { get; init; }
    public string? BgVideoPath { get; init; }
    public bool    HasBgVideo  { get; init; }

    // Theme text style
    public string                      FontFamily     { get; init; } = "Arial";
    public double                      FontSize       { get; init; } = 72;
    public System.Windows.TextAlignment TextAlignment { get; init; } = System.Windows.TextAlignment.Center;

    // Resolved header / footer
    public string HeaderText { get; init; } = string.Empty;
    public bool   HasHeader  { get; init; }
    public string FooterText { get; init; } = string.Empty;
    public bool   HasFooter  { get; init; }
}

/// <summary>One compact row in the stage view's clickable "all slides" list.</summary>
public sealed record SlideListItem(int Index, string Label, string PreviewText, bool IsCurrent);

/// <summary>Which cascade level F7's live style editor writes to. No "global/Base" level — editing
/// the app-wide default theme live from Stage View is out of scope (too risky/rare to justify).</summary>
public enum StageStyleScope { Song, ThisOccurrence }

public partial class StageViewModel : BaseViewModel, IDisposable
{
    private readonly IProjectionService     _projectionService;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly ITokenResolver         _tokenResolver;
    private readonly IAppSettingsService    _appSettings;
    private readonly ILogger<StageViewModel> _logger;

    // Per-navigation theme cache — cleared on ThemeChanged and recreated with next scope
    private readonly ConcurrentDictionary<int, Theme> _themeCache = new();
    private Theme? _defaultTheme;

    // Status
    [ObservableProperty] private bool   _isProjecting;
    [ObservableProperty] private bool   _isServiceScheduleActive;
    [ObservableProperty] private string _contextLabel  = string.Empty;
    [ObservableProperty] private string _slidePosition = string.Empty;

    // Rendering snapshots
    [ObservableProperty] private SlidePreview _currentPreview = SlidePreview.Empty;
    [ObservableProperty] private bool         _hasNextSlide;
    [ObservableProperty] private SlidePreview _nextPreview = SlidePreview.Empty;

    // Compact clickable list of every slide in the current item, alongside the up-next preview.
    [ObservableProperty] private ObservableCollection<SlideListItem> _allSlides = [];

    // Mirrors the projector's transport so the preview pauses when the operator pauses.
    [ObservableProperty] private bool _isPreviewVideoPlaying = true;

    // Announcement banner (overlays the current-slide preview)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnnouncement))]
    private string _announcementText = string.Empty;

    public bool HasAnnouncement => !string.IsNullOrEmpty(AnnouncementText);

    // Persistent lower-third mirror — replays the projector's band styling and scroll ticker.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLowerThird))]
    private string _lowerThirdText = string.Empty;

    [ObservableProperty] private bool   _lowerThirdScrollEnabled;
    [ObservableProperty] private int    _lowerThirdScrollSpeed = 90;
    [ObservableProperty] private string _lowerThirdBandColor = "#CC101018";
    [ObservableProperty] private string _lowerThirdTextColor = "#FFFFFF";
    [ObservableProperty] private int    _lowerThirdFontSize   = 40;

    public bool HasLowerThird => !string.IsNullOrEmpty(LowerThirdText);

    // ── F7: live style editor — writes into a real, per-scope Theme row (song or, when the live
    // item is service-driven, this occurrence only). Replaces the old non-persisted quick-fix. ──
    [ObservableProperty] private bool _isSongLive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScopeSong))]
    [NotifyPropertyChangedFor(nameof(IsScopeOccurrence))]
    private StageStyleScope _selectedScope = StageStyleScope.Song;

    [ObservableProperty] private bool _isOccurrenceScopeAvailable;

    public bool IsScopeSong       => SelectedScope == StageStyleScope.Song;
    public bool IsScopeOccurrence => SelectedScope == StageStyleScope.ThisOccurrence;

    [ObservableProperty] private int _editableFontSize = 72;
    [ObservableProperty] private System.Windows.Media.Color _editableFontColor = System.Windows.Media.Colors.White;
    [ObservableProperty] private System.Windows.Media.Color _editableBackgroundColor = System.Windows.Media.Colors.Black;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditableBackgroundImage))]
    private string? _editableBackgroundImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditableBackgroundVideo))]
    private string? _editableBackgroundVideoPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditableBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(IsEditableBackgroundImage))]
    [NotifyPropertyChangedFor(nameof(IsEditableBackgroundVideo))]
    private BackgroundType _editableBackgroundType = BackgroundType.Color;

    public bool IsEditableBackgroundColor => EditableBackgroundType == BackgroundType.Color;
    public bool IsEditableBackgroundImage => EditableBackgroundType == BackgroundType.Image;
    public bool IsEditableBackgroundVideo => EditableBackgroundType == BackgroundType.Video;

    public bool HasEditableBackgroundImage =>
        !string.IsNullOrWhiteSpace(EditableBackgroundImagePath) && File.Exists(EditableBackgroundImagePath);
    public bool HasEditableBackgroundVideo =>
        !string.IsNullOrWhiteSpace(EditableBackgroundVideoPath) && File.Exists(EditableBackgroundVideoPath);

    // Existing backgrounds in the managed library, for re-picking without hunting in foreign folders.
    public ObservableCollection<MediaFile> BackgroundImages { get; } = [];
    public ObservableCollection<MediaFile> BackgroundVideos { get; } = [];

    [ObservableProperty] private MediaFile? _selectedLibraryImage;
    [ObservableProperty] private MediaFile? _selectedLibraryVideo;

    partial void OnSelectedLibraryImageChanged(MediaFile? value)
    {
        if (value is not null) EditableBackgroundImagePath = value.FilePath;
    }

    partial void OnSelectedLibraryVideoChanged(MediaFile? value)
    {
        if (value is not null) EditableBackgroundVideoPath = value.FilePath;
    }

    private const double FontSizeStep = 8;
    private const double FontSizeMin  = 16;
    private const double FontSizeMax  = 220;

    // Guards SyncEditableThemeAsync's own writes from re-triggering a persist (only user edits should).
    private bool _isSyncingEditableTheme;

    // The full Theme row currently being edited (preserves fields F7 doesn't expose, e.g. HeaderTemplate)
    // and the id F7 is actively writing to this session — null until the first edit clones a dedicated
    // theme for the current (item, scope) pair. Both reset whenever either changes.
    private Theme? _workingTheme;
    private int?   _liveEditThemeId;
    private string? _lastContextKey;

    public StageViewModel(
        IProjectionService projectionService,
        IServiceScopeFactory scopeFactory,
        ITokenResolver tokenResolver,
        IAppSettingsService appSettings,
        ILogger<StageViewModel> logger)
    {
        _projectionService = projectionService;
        _scopeFactory      = scopeFactory;
        _tokenResolver     = tokenResolver;
        _appSettings       = appSettings;
        _logger            = logger;
    }

    // ── Schedule item navigation (delegated to ServiceScheduleViewModel via IProjectionService) ──

    [RelayCommand]
    private void NextItem() => _projectionService.RequestNextScheduleItem();

    [RelayCommand]
    private void PrevItem() => _projectionService.RequestPreviousScheduleItem();

    // ── Slide list navigation ─────────────────────────────────────────────────

    [RelayCommand]
    private void JumpToSlide(int index) => _projectionService.GoTo(index);

    // ── F7: live style editor ────────────────────────────────────────────────

    [RelayCommand]
    private void IncreaseFontSize() =>
        EditableFontSize = (int)Math.Clamp(EditableFontSize + FontSizeStep, FontSizeMin, FontSizeMax);

    [RelayCommand]
    private void DecreaseFontSize() =>
        EditableFontSize = (int)Math.Clamp(EditableFontSize - FontSizeStep, FontSizeMin, FontSizeMax);

    [RelayCommand]
    private void SetScope(string scope)
    {
        if (Enum.TryParse<StageStyleScope>(scope, out var parsed))
            SelectedScope = parsed;
    }

    partial void OnSelectedScopeChanged(StageStyleScope value) => _ = SyncEditableThemeAsync();

    [RelayCommand]
    private void SetBackgroundType(string type)
    {
        var parsed = type switch
        {
            "Image" => BackgroundType.Image,
            "Video" => BackgroundType.Video,
            _       => BackgroundType.Color
        };
        if (parsed == EditableBackgroundType) return;

        // Switching type "forgets" whichever type(s) are no longer active. Without this, the path
        // and library selection for the type being switched away from (e.g. a video the operator
        // picked earlier) silently survive and reappear pre-selected the next time that type is
        // chosen again -- confusing, since nothing the operator just did asked for that video back.
        // Suppress the per-field persist each clear would otherwise trigger (OnEditablePropertyChanged)
        // so this is one clean persist, not a blank/leftover frame followed by the real one.
        _isSyncingEditableTheme = true;
        try
        {
            EditableBackgroundType = parsed;
            if (parsed != BackgroundType.Video) { EditableBackgroundVideoPath = null; SelectedLibraryVideo = null; }
            if (parsed != BackgroundType.Image) { EditableBackgroundImagePath = null; SelectedLibraryImage = null; }
        }
        finally { _isSyncingEditableTheme = false; }

        // Switching type alone (no path change, if the newly active type has nothing picked yet)
        // must still re-persist -- BuildThemeFromEditableFields reads EditableBackgroundType to
        // decide which field applies, so without this explicit call the theme could keep whatever
        // was last actually persisted even though the UI now shows a different type selected.
        OnEditablePropertyChanged();
    }

    // Any user edit to a stylable field persists it — see OnEditablePropertyChanged.
    partial void OnEditableFontSizeChanged(int value) => OnEditablePropertyChanged();
    partial void OnEditableFontColorChanged(System.Windows.Media.Color value) => OnEditablePropertyChanged();
    partial void OnEditableBackgroundColorChanged(System.Windows.Media.Color value) => OnEditablePropertyChanged();
    partial void OnEditableBackgroundImagePathChanged(string? value) => OnEditablePropertyChanged();
    partial void OnEditableBackgroundVideoPathChanged(string? value) => OnEditablePropertyChanged();

    private void OnEditablePropertyChanged()
    {
        if (_isSyncingEditableTheme) return; // programmatic sync from the resolved theme, not a user edit
        _ = PersistEditableThemeAsync();
    }

    /// <summary>
    /// True when an edit at this scope level would need to clone the effective theme before mutating
    /// it — i.e. this level has no explicit theme of its own yet, or its resolved effective theme is
    /// the shared app-wide default. Editing either in place would silently repaint every other
    /// song/occurrence that also has no explicit theme. Pulled out as a pure predicate, mirroring this
    /// file's IsSongContextKey/ComputeMirrorScale convention, so it's unit-testable without a live DB.
    /// </summary>
    public static bool ShouldCloneBeforeEdit(int? scopeOwnThemeId, bool effectiveThemeIsDefault) =>
        scopeOwnThemeId is null || effectiveThemeIsDefault;

    /// <summary>Re-resolves the effective theme for <see cref="SelectedScope"/> and the live item,
    /// then syncs the editable fields from it. Called on live-item change and scope change.</summary>
    private async Task SyncEditableThemeAsync()
    {
        var contextKey     = _projectionService.ContextKey;
        var songId         = ProjectionContextKeys.TryGetSongId(contextKey);
        var scheduleItemId = ProjectionContextKeys.TryGetServiceScheduleItemId(contextKey);

        IsOccurrenceScopeAvailable = scheduleItemId is not null;
        if (!IsOccurrenceScopeAvailable && SelectedScope == StageStyleScope.ThisOccurrence)
        {
            SelectedScope = StageStyleScope.Song; // triggers a re-entrant sync; bail out of this one
            return;
        }

        if (songId is null)
        {
            _workingTheme    = null;
            _liveEditThemeId = null;
            return;
        }

        try
        {
            await using var scope     = _scopeFactory.CreateAsyncScope();
            var songService           = scope.ServiceProvider.GetRequiredService<ISongService>();
            var worshipService        = scope.ServiceProvider.GetRequiredService<IWorshipServiceService>();
            var themeService          = scope.ServiceProvider.GetRequiredService<IThemeService>();

            var song = await songService.GetByIdAsync(songId.Value);
            if (song is null) { _workingTheme = null; _liveEditThemeId = null; return; }

            int? ownThemeId = SelectedScope == StageStyleScope.ThisOccurrence && scheduleItemId is not null
                ? await worshipService.GetItemThemeIdAsync(scheduleItemId.Value)
                : song.ThemeId;

            var effectiveThemeId = ThemeCascade.ForSong(
                SelectedScope == StageStyleScope.ThisOccurrence ? ownThemeId : null,
                song.ThemeId,
                _appSettings.Current);

            var theme = effectiveThemeId.HasValue
                ? await themeService.GetByIdAsync(effectiveThemeId.Value) ?? await themeService.GetDefaultAsync()
                : await themeService.GetDefaultAsync();

            _workingTheme    = theme;
            _liveEditThemeId = ShouldCloneBeforeEdit(ownThemeId, theme.IsDefault) ? null : theme.Id;

            _isSyncingEditableTheme = true;
            try
            {
                EditableFontSize            = theme.FontSize;
                EditableFontColor           = ParseColor(theme.FontColor, System.Windows.Media.Colors.White);
                EditableBackgroundColor     = ParseColor(theme.BackgroundColor, System.Windows.Media.Colors.Black);
                EditableBackgroundImagePath = theme.BackgroundImagePath;
                EditableBackgroundVideoPath = theme.BackgroundVideoPath;
                EditableBackgroundType = !string.IsNullOrWhiteSpace(theme.BackgroundVideoPath) ? BackgroundType.Video
                    : !string.IsNullOrWhiteSpace(theme.BackgroundImagePath)                    ? BackgroundType.Image
                    :                                                                            BackgroundType.Color;
            }
            finally { _isSyncingEditableTheme = false; }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stage view failed to sync editable theme");
        }
    }

    /// <summary>
    /// Persists the current editable fields to the theme F7 owns for (live item, <see cref="SelectedScope"/>),
    /// cloning one first if this is the first edit this session (see <see cref="ShouldCloneBeforeEdit"/>).
    /// The live <see cref="Slide"/> objects on screen were built with the PREVIOUS ThemeId baked in
    /// (Slide.ThemeId is set once at generation time — see <see cref="ISongService.GenerateSlides"/>),
    /// so merely editing the Theme row and calling <see cref="IProjectionService.NotifyThemeChanged"/>
    /// (which just re-renders the SAME cached slide) would silently do nothing on the very edit that
    /// clones a new theme id. Slides must be regenerated with the new id and pushed via
    /// <see cref="IProjectionService.TryUpdateSlides"/> — the same mechanism live song-content edits
    /// use (see ServiceScheduleViewModel.ApplyEditedSongToLiveProjection).
    /// </summary>
    private async Task PersistEditableThemeAsync()
    {
        var contextKey = _projectionService.ContextKey;
        var songId     = ProjectionContextKeys.TryGetSongId(contextKey);
        if (songId is null || _workingTheme is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var themeService      = scope.ServiceProvider.GetRequiredService<IThemeService>();
            var songService       = scope.ServiceProvider.GetRequiredService<ISongService>();
            var worshipService    = scope.ServiceProvider.GetRequiredService<IWorshipServiceService>();

            var scheduleItemId = ProjectionContextKeys.TryGetServiceScheduleItemId(contextKey);
            int themeId;

            if (_liveEditThemeId is null)
            {
                var song      = await songService.GetByIdAsync(songId.Value);
                var cloneName = $"{song?.Title ?? "Song"} — live style";
                var created   = await themeService.CreateAsync(BuildThemeFromEditableFields(0, cloneName));
                _liveEditThemeId = created.Id;
                _workingTheme    = created;
                themeId          = created.Id;

                if (SelectedScope == StageStyleScope.ThisOccurrence && scheduleItemId is not null)
                    await worshipService.SetItemThemeIdAsync(scheduleItemId.Value, created.Id);
                else
                    await songService.SetThemeIdAsync(songId.Value, created.Id);
            }
            else
            {
                var updated = BuildThemeFromEditableFields(_liveEditThemeId.Value, _workingTheme.Name);
                await themeService.UpdateAsync(updated);
                _workingTheme = updated;
                themeId       = _liveEditThemeId.Value;
            }

            var freshSong = await songService.GetByIdAsync(songId.Value);
            if (freshSong is not null)
            {
                var verseOrderOverride = scheduleItemId is not null
                    ? await worshipService.GetItemVerseOrderOverrideAsync(scheduleItemId.Value)
                    : null;
                var slides = songService.GenerateSlides(freshSong, themeId, verseOrderOverride);
                if (slides.Count > 0)
                    _projectionService.TryUpdateSlides(contextKey!, slides, freshSong.Title);
            }

            // Clears ProjectionWindow's/this VM's theme-content caches — needed on a 2nd+ edit, where
            // themeId is unchanged but the row's content (e.g. FontSize) just did.
            _projectionService.NotifyThemeChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage view failed to persist live style edit");
            SetError(L("Stage_ErrStyleSave"));
        }
    }

    /// <summary>Builds a full Theme row from the editable fields plus whatever <see cref="_workingTheme"/>
    /// carries that F7 doesn't expose (font family, alignment, header/footer, transition) — matches
    /// AddEditThemeViewModel.BuildTheme's mutually-exclusive background handling.</summary>
    private Theme BuildThemeFromEditableFields(int id, string name) => new()
    {
        Id                  = id,
        Name                = name,
        FontFamily          = _workingTheme?.FontFamily ?? "Arial",
        FontSize            = EditableFontSize,
        TextAlignment       = _workingTheme?.TextAlignment ?? "Center",
        FontColor           = ColorToHex(EditableFontColor),
        BackgroundColor     = ColorToHex(EditableBackgroundColor),
        BackgroundImagePath = EditableBackgroundType == BackgroundType.Image ? NullIfEmpty(EditableBackgroundImagePath) : null,
        BackgroundVideoPath = EditableBackgroundType == BackgroundType.Video ? NullIfEmpty(EditableBackgroundVideoPath) : null,
        IsDefault           = false, // an F7-managed theme is never the shared app default
        HeaderTemplate      = _workingTheme?.HeaderTemplate,
        FooterTemplate      = _workingTheme?.FooterTemplate,
        SlideTransition     = _workingTheme?.SlideTransition
    };

    // ── F7: background library (mirrors AddEditThemeViewModel) ──────────────────

    public async Task LoadBackgroundLibraryAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
            var backgrounds  = await mediaService.GetBackgroundsAsync();
            ReplaceAll(BackgroundImages, backgrounds.Where(b => b.Type == Domain.Enums.MediaType.Image));
            ReplaceAll(BackgroundVideos, backgrounds.Where(b => b.Type == Domain.Enums.MediaType.Video));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage view failed to load background library");
        }
    }

    public async Task ImportBackgroundFileAsync(string sourcePath, bool isVideo)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
            var media = await mediaService.ImportBackgroundAsync(sourcePath);
            if (isVideo) EditableBackgroundVideoPath = media.FilePath;
            else         EditableBackgroundImagePath = media.FilePath;
            await LoadBackgroundLibraryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage view failed to import background from {Source}", sourcePath);
            SetError(L("Stage_ErrBackgroundImport"));
        }
    }

    private static void ReplaceAll(ObservableCollection<MediaFile> target, IEnumerable<MediaFile> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    /// <summary>
    /// True when <paramref name="contextKey"/> identifies a live song (standalone or service-driven)
    /// — gates the F7 style editor. Split out as pure logic, mirroring
    /// <see cref="ComputeMirrorScale"/>, so it's unit-testable without a live IProjectionService.
    /// </summary>
    public static bool IsSongContextKey(string? contextKey) => ProjectionContextKeys.TryGetSongId(contextKey) is not null;

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            _projectionService.SlideChanged                   += OnSlideChanged;
            _projectionService.ProjectionStateChanged         += OnProjectionStateChanged;
            _projectionService.ThemeChanged                   += OnThemeChanged;
            _projectionService.ServiceScheduleActiveChanged   += OnServiceScheduleActiveChanged;
            _projectionService.NextScheduleItemPreviewChanged += OnNextScheduleItemPreviewChanged;
            _projectionService.AnnouncementChanged            += OnAnnouncementChanged;
            _projectionService.LowerThirdChanged              += OnLowerThirdChanged;
            _projectionService.MediaTransportChanged          += OnMediaTransportChanged;

            AnnouncementText = _projectionService.CurrentAnnouncement ?? string.Empty;
            RefreshLowerThirdSettings();
            LowerThirdText = _projectionService.CurrentLowerThird ?? string.Empty;
            _ = LoadBackgroundLibraryAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage view load failed");
            SetError(L("Stage_ErrLoad"));
        }
        finally { IsBusy = false; }
    }

    // ── Projection event handlers ─────────────────────────────────────────────

    private async void OnSlideChanged(object? sender, Slide? slide)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view slide refresh failed"); }
    }

    private async void OnProjectionStateChanged(object? sender, bool _)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view state refresh failed"); }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _defaultTheme = null;
        _themeCache.Clear();
        _ = RefreshAsync();
    }

    private async void OnServiceScheduleActiveChanged(object? sender, EventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view schedule-active refresh failed"); }
    }

    private async void OnNextScheduleItemPreviewChanged(object? sender, EventArgs e)
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Stage view next-item preview refresh failed"); }
    }

    private void OnAnnouncementChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            AnnouncementText = _projectionService.CurrentAnnouncement ?? string.Empty);
    }

    private void OnLowerThirdChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // Settings first so the View sees fresh band/scroll state before it reacts to the text.
            RefreshLowerThirdSettings();
            LowerThirdText = _projectionService.CurrentLowerThird ?? string.Empty;
        });
    }

    // Matches the fixed design canvas in StageView.xaml (Grid Width="1920" Height="1080")
    // that the preview's outer Viewbox scales uniformly to fit the small panel.
    private const double DesignCanvasWidth  = 1920;
    private const double DesignCanvasHeight = 1080;

    /// <summary>Snapshots the lower-third band/scroll settings — mirrors ProjectionWindow.ApplyLowerThirdStyle.</summary>
    private void RefreshLowerThirdSettings()
    {
        var s = _appSettings.Current;
        var (scaleX, scaleY) = GetProjectorMirrorScale();
        LowerThirdScrollEnabled = s.LowerThirdScroll;
        LowerThirdScrollSpeed   = Math.Max(10, (int)Math.Round(s.LowerThirdScrollSpeed * scaleX));
        LowerThirdBandColor     = s.LowerThirdBandColor;
        LowerThirdTextColor     = s.LowerThirdTextColor;
        LowerThirdFontSize      = Math.Max(12, (int)Math.Round(s.LowerThirdFontSize * scaleY));
    }

    /// <summary>
    /// Font size and scroll speed are absolute values tuned for the projector's real screen.
    /// The stage preview renders everything inside a fixed 1920×1080 canvas (Viewbox-scaled to
    /// the small panel), so mirroring those values 1:1 only looks right when the real projector
    /// happens to be 1920×1080. Pre-scaling by (design ÷ real) here makes the final on-screen
    /// proportion — after the panel's own Viewbox scales the canvas down — match the real
    /// projector at any resolution. No secondary screen: <see cref="ProjectionWindow"/> falls
    /// back to its small floating preview window instead of a 1920×1080 mirror, so that window's
    /// fixed size is the "real" resolution to scale against, not 1920×1080 (which would wrongly
    /// assume a full-size mirror and leave the stage preview's lower third much smaller than
    /// what the floating preview window actually shows).
    /// </summary>
    private static (double ScaleX, double ScaleY) GetProjectorMirrorScale()
    {
        var bounds = ScreenHelper.GetSecondaryScreen()?.Bounds;
        return bounds is { } b
            ? ComputeMirrorScale(b.Width, b.Height)
            : ComputeMirrorScale(ProjectionWindow.FallbackPreviewWidth, ProjectionWindow.FallbackPreviewHeight);
    }

    /// <summary>Pure design÷real scale math, split out from the screen lookup so it's unit-testable.</summary>
    public static (double ScaleX, double ScaleY) ComputeMirrorScale(int realWidth, int realHeight)
    {
        if (realWidth <= 0 || realHeight <= 0) return (1.0, 1.0);
        return (DesignCanvasWidth / realWidth, DesignCanvasHeight / realHeight);
    }

    private void OnMediaTransportChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            IsPreviewVideoPlaying = _projectionService.MediaTransport.IsPlaying);
    }

    // ── Core refresh ──────────────────────────────────────────────────────────

    // Monotonic guard: rapid slide changes (auto-advance) fire concurrent RefreshAsync tasks.
    // Each tags itself; a task superseded by a newer one self-discards instead of writing a
    // stale preview — mirrors ProjectionWindow.OnSlideChanged (H2).
    private int _refreshSequence;

    private async Task RefreshAsync()
    {
        var seq = Interlocked.Increment(ref _refreshSequence);

        var contextKey = _projectionService.ContextKey;
        if (contextKey != _lastContextKey)
        {
            // Live item changed (or projection stopped) — re-resolve which theme F7 is editing.
            _lastContextKey = contextKey;
            _ = SyncEditableThemeAsync();
        }

        var isProjecting = _projectionService.IsProjecting;
        var slides       = _projectionService.CurrentSlides;
        var idx          = _projectionService.CurrentSlideIndex;
        var current      = _projectionService.CurrentSlide;

        var currentTheme   = await ResolveThemeAsync(current?.ThemeId);
        var currentPreview = BuildPreview(current, currentTheme);

        SlidePreview nextPreview = SlidePreview.Empty;
        bool hasNext = false;
        var nextIdx  = idx + 1;

        if (isProjecting && nextIdx < slides.Count)
        {
            // Next slide within the current schedule item
            var next      = slides[nextIdx];
            var nextTheme = await ResolveThemeAsync(next.ThemeId);
            nextPreview = BuildPreview(next, nextTheme);
            hasNext     = true;
        }
        else if (isProjecting && _projectionService.NextScheduleItemPreviewSlide is { } nextItemSlide)
        {
            // On the last slide of this item — preview the first slide of the next schedule item
            var nextTheme = await ResolveThemeAsync(nextItemSlide.ThemeId);
            nextPreview = BuildPreview(nextItemSlide, nextTheme);
            hasNext     = true;
        }

        var isScheduleActive = _projectionService.IsServiceScheduleActive;
        var allSlideItems    = BuildSlideListItems(slides, idx);

        // A newer refresh started while we awaited theme resolution — let it win.
        if (seq != _refreshSequence) return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (seq != _refreshSequence) return; // re-check on the UI thread before writing
            IsProjecting             = isProjecting;
            IsServiceScheduleActive  = isScheduleActive;
            IsSongLive    = isProjecting && IsSongContextKey(contextKey);
            ContextLabel  = _projectionService.ContextLabel;
            SlidePosition = isProjecting && slides.Count > 0
                ? $"{idx + 1} / {slides.Count}"
                : string.Empty;

            CurrentPreview = currentPreview;
            HasNextSlide   = hasNext;
            NextPreview    = nextPreview;
            AllSlides      = allSlideItems;
        });
    }

    // ── Slide list builder ───────────────────────────────────────────────────

    private const int PreviewTextMaxLength = 40;

    /// <summary>Pure mapping from the projected slides to compact list rows — split out (like
    /// <see cref="ComputeMirrorScale"/>) so it's unit-testable without a live Dispatcher.</summary>
    public static ObservableCollection<SlideListItem> BuildSlideListItems(IReadOnlyList<Slide> slides, int currentIndex)
    {
        var items = new ObservableCollection<SlideListItem>();
        for (var i = 0; i < slides.Count; i++)
            items.Add(new SlideListItem(i, slides[i].Label, TruncatePreview(slides[i].Content), i == currentIndex));
        return items;
    }

    private static string TruncatePreview(string content)
    {
        var firstLine = content.Split('\n')[0].Trim();
        return firstLine.Length > PreviewTextMaxLength
            ? firstLine[..PreviewTextMaxLength] + "…"
            : firstLine;
    }

    // ── Preview builder ───────────────────────────────────────────────────────

    private SlidePreview BuildPreview(Slide? slide, Theme? theme)
    {
        if (slide is null) return SlidePreview.Empty;

        var fontFamily  = theme?.FontFamily ?? "Arial";
        var fontSize    = (double)(theme?.FontSize ?? 72);
        var fontColor   = ParseColor(theme?.FontColor, System.Windows.Media.Colors.White);
        var textAlign   = ParseAlignment(theme?.TextAlignment);
        var bgColor     = ParseColor(theme?.BackgroundColor, System.Windows.Media.Colors.Black);
        var bgImagePath = ValidPath(theme?.BackgroundImagePath);
        var bgVideoPath = ValidPath(theme?.BackgroundVideoPath);

        var headerText = ResolveZone(theme?.HeaderTemplate, slide.Context);
        var footerText = ResolveZone(theme?.FooterTemplate, slide.Context);

        var isImageMedia = slide.Type == SlideType.Media && MediaFormats.IsImage(slide.MediaPath);
        var isVideoMedia = slide.Type == SlideType.Media && MediaFormats.IsVideo(slide.MediaPath);

        return new SlidePreview
        {
            Content       = slide.Content,
            SectionLabel  = slide.Label,
            IsBlank       = slide.Type == SlideType.Blank,
            IsText        = slide.Type is SlideType.Song or SlideType.Bible,
            IsImageMedia  = isImageMedia,
            IsVideoMedia  = isVideoMedia,
            MediaPath     = slide.MediaPath,
            FontFamily    = fontFamily,
            FontSize      = fontSize,
            FontColor     = fontColor,
            TextAlignment = textAlign,
            BgColor       = bgColor,
            BgImagePath   = bgImagePath,
            HasBgImage    = bgImagePath is not null,
            BgVideoPath   = bgVideoPath,
            HasBgVideo    = bgVideoPath is not null,
            HeaderText    = headerText,
            HasHeader     = !string.IsNullOrEmpty(headerText),
            FooterText    = footerText,
            HasFooter     = !string.IsNullOrEmpty(footerText)
        };
    }

    private string ResolveZone(string? template, SlideContext context)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var resolved = _tokenResolver.Resolve(template, context);
        return resolved.Any(char.IsLetterOrDigit) ? resolved : string.Empty;
    }

    // ── Theme resolution ──────────────────────────────────────────────────────

    private async Task<Theme?> ResolveThemeAsync(int? themeId)
    {
        if (themeId.HasValue)
        {
            if (_themeCache.TryGetValue(themeId.Value, out var cached)) return cached;
        }
        else if (_defaultTheme is not null)
        {
            return _defaultTheme;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var svc = scope.ServiceProvider.GetRequiredService<IThemeService>();

            Theme theme;
            if (themeId.HasValue)
            {
                theme = await svc.GetByIdAsync(themeId.Value) ?? await svc.GetDefaultAsync();
                _themeCache[themeId.Value] = theme;
            }
            else
            {
                theme = await svc.GetDefaultAsync();
                _defaultTheme = theme;
            }
            return theme;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stage view failed to resolve theme (ThemeId={ThemeId})",
                themeId?.ToString() ?? "default");
            return null;
        }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static System.Windows.Media.Color ParseColor(string? hex, System.Windows.Media.Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try   { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static string ColorToHex(System.Windows.Media.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static System.Windows.TextAlignment ParseAlignment(string? s) => s switch
    {
        "Left"  => System.Windows.TextAlignment.Left,
        "Right" => System.Windows.TextAlignment.Right,
        _       => System.Windows.TextAlignment.Center
    };

    private static string? ValidPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _projectionService.SlideChanged                   -= OnSlideChanged;
        _projectionService.ProjectionStateChanged         -= OnProjectionStateChanged;
        _projectionService.ThemeChanged                   -= OnThemeChanged;
        _projectionService.ServiceScheduleActiveChanged   -= OnServiceScheduleActiveChanged;
        _projectionService.NextScheduleItemPreviewChanged -= OnNextScheduleItemPreviewChanged;
        _projectionService.AnnouncementChanged            -= OnAnnouncementChanged;
        _projectionService.LowerThirdChanged              -= OnLowerThirdChanged;
        _projectionService.MediaTransportChanged          -= OnMediaTransportChanged;
    }
}
