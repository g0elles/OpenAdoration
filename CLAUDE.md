# OpenAdoration — Agent Context (Optimized)
# Last updated: 2026-05-29
# Read before any code change.

## MANDATORY RULES
- SOLID strictly enforced: VMs = state+commands only; Services = business logic; Repos = data access.
- No dead code, no TODO/FIXME. Methods <20 lines. No magic strings/numbers.
- All IDisposable implement Dispose() and unsubscribe events.
- UI updates from non‑UI thread → `Dispatcher.Invoke`.
- `CancellationToken` through all async chains.
- EF Core `SaveChangesAsync` is transactional – never call twice per operation.
- Never inject concrete classes across layer boundaries.
- No error handling for impossible paths, no backwards‑compat shims.

## Tech Stack
- .NET 10 WPF, Entity Framework Core 9 + SQLite, CommunityToolkit.Mvvm 8.4 (source gen)
- Microsoft.Extensions.Hosting (generic host DI), Serilog (file+debug)
- Multi‑monitor: `System.Windows.Forms.Screen.AllScreens` → requires `UseWindowsForms=true`

## Clean Architecture

Domain (entities, enums, BaseEntity)
   ↑
Application (service+repo interfaces, DTOs, Slide, SlideContext)
   ↑
Infrastructure (DbContext, repos, migrations, logging, TokenResolver, AppSettingsService)
   ↑
WPF (App, Windows, VMs, Views, Converters, Styles)

- **Dependency rule**: WPF → Application only. Infrastructure → Application only. Domain → nothing.

## DI Lifetimes (CRITICAL)
- **Singletons**: `MainViewModel`, `MainWindow`, `ProjectionWindow`, `IProjectionService`, `ITokenResolver`, `IAppSettingsService`
- **Transients**: all page VMs (`SongsViewModel`, `BibleViewModel`, `ServiceScheduleViewModel`, `MediaViewModel`, `ThemeViewModel`, `AddEditSongViewModel`, `AddEditThemeViewModel`, `StageViewModel`, `SettingsViewModel`)
- **Scoped**: all services + all repos
- **Factory**: `IDbContextFactory<AppDbContext>` (singleton factory → scoped context)

## Scope‑per‑Navigation (G11)
- `MainViewModel._currentScope` disposed + replaced on every `NavigateTo<T>()`.
- **NEVER** call `GetRequiredService<T>()` on root provider for page VMs.
- Views fire `LoadCommand` from `Loaded` event in code‑behind.

## Database Essentials
- SQLite, migrations applied at startup via `MigrateAsync()`.
- **Timestamp pattern**: `StampTimestamps()` sets `CreatedAt`+`UpdatedAt` on add, only `UpdatedAt` on modify.
- **Song update**: load tracked entity → `RemoveRange(sections)` → add new sections with `Id=0` (forces INSERT).
- **Bible import**: batch 1000 rows + `ChangeTracker.Clear()` (full Bible ≈31k verses).
- **Song search**: title/author `LIKE` first; if 0 results → FTS5 (`SongSectionsFts`) with prefix matching: each word + `*` (e.g. `"cura*"` matches `"curará"`).
- **Bible search**: two modes – `Keyword` (per‑word `*` prefix, implicit AND) and `Phrase` (exact quoted). `BibleRepository.BuildFtsTerm` builds `MATCH` expression.

## Projection Engine (`IProjectionService`, singleton)
- **API**:
  - `LoadSlides(slides, contextLabel)` – starts projection, fires `SlideChanged`
  - `Next/Previous/GoTo(index)` – change slide, fires `SlideChanged`
  - `ShowBlank()` – shows `Slide.Blank()` without stopping
  - `ShowAnnouncement(text)` – sets `CurrentAnnouncement` + `AnnouncementChanged` (banner overlay, not a slide)
  - `ClearAnnouncement()` – clears banner
  - `Stop()` – clears all state (including `IsServiceScheduleActive`, `NextScheduleItemPreviewSlide`, announcement)
  - `RequestNextScheduleItem()` / `RequestPreviousScheduleItem()` – message bus for `ServiceScheduleViewModel`
  - `SetServiceScheduleActive(bool)` – called by `ServiceScheduleViewModel`
  - `SetNextScheduleItemPreview(Slide?)` – used by `StageViewModel`
- **Events**: `SlideChanged`, `ProjectionStateChanged`, `ThemeChanged`, `NextScheduleItemRequested`, `PreviousScheduleItemRequested`, `ServiceScheduleActiveChanged`, `NextScheduleItemPreviewChanged`, `AnnouncementChanged`
- **Safety**: each subscriber wrapped in `try/catch` – exceptions logged, engine never stops.
- **Subscriber cleanup (G9)**: every class subscribing must unsubscribe in `OnClosed()` or `Dispose()` – else GC leak + dead dispatcher.

## Projection Window Layout
- 3‑zone `Grid`: Header (Auto), Body (`Viewbox`), Footer (Auto). Zones auto‑hide if resolved text contains no letter/digit (G20).
- Corner label (top‑left) shown only when no `HeaderTemplate` set.
- Media: image → `BitmapImage` as `BackgroundImage`; video → `MediaElement` with audio.
- Transition: fade opacity 0→1 on `ContentLayers` (configurable `SlideTransitionMilliseconds`).
- Theme resolved per slide via `IServiceScopeFactory` → `IThemeService`, cached per session.

## Token System (`ITokenResolver`, singleton)
- Pattern `\[(\w+)\]` via `[GeneratedRegex]`. Resolves with `SlideContext` + `IAppSettingsService` for church tokens.
- **Tokens**:
  - Church: `[ChurchName]`, `[SiteLicense]` (from `settings.json`)
  - Song: `[SongTitle]`, `[SongAuthor]`, `[SongVerseTag]`, `[SongCopyright]`, `[SongCCLI]`
  - Bible: `[BibleBookName]`, `[BibleChapterID]`, `[BibleVerseID]`, `[BibleReference]`, `[BibleDescription]`
- Unknown tokens left unchanged.
- **Zone auto‑hide (G20)**: after resolving, check `resolved.Any(char.IsLetterOrDigit)`. If false → hide zone. (Whitespace trim alone fails for e.g. `"  :"`.)

## Settings System
- File: `%LOCALAPPDATA%\OpenAdoration\settings.json` (not in DB).
- `IAppSettingsService` (singleton): loads once at construction, `SaveAsync` rewrites.
- Fields: `ChurchName`, `ChurchCcliNumber`, `DefaultAutoAdvanceSeconds`, `DefaultBibleVersesPerSlide` (min 1), `AnnouncementDurationSeconds` (min 1, default 25), `SlideTransitionMilliseconds` (0=off, default 300).
- UI: `SettingsViewModel` (transient) + `SettingsView`. Save notifies `IProjectionService.NotifyThemeChanged()` to re‑resolve church tokens.

## Stage View
- `StageViewModel` (transient, `IDisposable`) + `StageView`.
- Layout: left panel (2/3) = current slide preview; right panel (1/3) = UP NEXT preview.
- Preview rendering matches ProjectionWindow 7‑layer stack (bg color, image/video, 3‑zone grid, media, blank overlay, “Not projecting” dim).
- **Cross‑item UP NEXT**: when next index >= slides.Count, `NextScheduleItemPreviewSlide` (first slide of next schedule item) is shown.
- Schedule Prev/Next buttons visible only when `IsServiceScheduleActive` → commands call `RequestPreviousScheduleItem`/`RequestNextScheduleItem` on `IProjectionService`.
- `SlidePreview` immutable record – swapping whole object triggers WPF re‑evaluation.
- Video sync: code‑behind subscribes to `VM.PropertyChanged` → calls `SyncVideo()`.
- Subscribes to 5 events; unsubscribes in `Dispose()`.

## Auto‑Advance
- `ScheduleItem.AutoAdvanceSeconds` (int?): NULL/0 = manual, positive = seconds.
- UI: `[−] [⏱ Manual/Ns] [+]` (+5s increments, max 300s).
- **Timer pattern (G19)**: `DispatcherTimer` – always one‑shot. Stop before advancing; `SlideChanged` restarts it.
- `ServiceScheduleViewModel` subscribes to `SlideChanged` → resets timer on every slide change.
- On tick: if not last slide → `Next()`; else if `CanNextItem` → `NextItem()`; else stop.
- Stop conditions: `StopLive()`, `OnProjectionStateChanged(false)`, `Dispose()`.

## Critical Gotchas (G1–G20)
- **G1** `UseWindowsForms=true` → fully qualify: `System.Windows.Controls.UserControl`, `System.Windows.MessageBox`, `System.Windows.Media.Color`, `Microsoft.Win32.OpenFileDialog`, `System.Windows.Input.KeyEventArgs`, `System.Windows.Controls.MediaElement`.
- **G2** EF Core `Design` package must be in **both** `Infrastructure` and `WPF` with `PrivateAssets="all"`.
- **G3** Seed data: use `static readonly DateTime SeedDate = new(2025,1,1,0,0,0,DateTimeKind.Utc)`, never `DateTime.UtcNow`.
- **G4** `LoadAsync()` must open with `if (IsBusy) return;` – never set `IsBusy` before call.
- **G5** `ProjectionService` subscriber exceptions caught+logged – if projection stops, check log for `[ERR]`.
- **G6** `SongRepository.UpdateAsync` replaces all sections via `RemoveRange` + add new with `Id=0`.
- **G7** Bible import batch 1000 + `ChangeTracker.Clear()` – do not remove.
- **G8** Every navigated ViewModel **must** have `DataTemplate` in `App.xaml` – otherwise blank content with no error.
- **G9** Every subscriber to `IProjectionService` events must unsubscribe in `OnClosed()`/`Dispose()`.
- **G10** Dark‑theme `ComboBox` → `FieldComboBox` requires full `ControlTemplate` on `ItemContainerStyle` + use `ItemTemplate` (not `DisplayMemberPath`).
- **G11** Scope‑per‑navigation is mandatory – use `NavigateTo<T>()` only.
- **G12** `[ObservableProperty]`, `[RelayCommand]` require **partial** class (and all containing classes must be partial).
- **G13** XAML `CommandParameter` for enums must be string – parse with `Enum.TryParse<SectionType>`.
- **G14** `Testament` enum values are `Old` and `New` – not `OldTestament`/`NewTestament`.
- **G15** `CollectionViewSource` `SortDescription` requires `xmlns:scm="clr-namespace:System.ComponentModel;assembly=WindowsBase"`.
- **G16** `BibleViewModel.SelectedChapter` uses `0` as sentinel – guard with `if (value > 0)`.
- **G17** `AlignToggleButton` `IsChecked` must be `Mode=OneWay` – TwoWay breaks command binding.
- **G18** `Run.Text` bindings on read‑only properties must be `Mode=OneWay` – default TwoWay causes runtime error.
- **G19** `DispatcherTimer` must be stopped in `StopLive()`, `OnProjectionStateChanged(false)`, **and** `Dispose()`.
- **G20** Token zone auto‑hide uses `resolved.Any(char.IsLetterOrDigit)`, not whitespace trim.

## Key Files (read first when debugging)
**WPF**:
- `App.xaml.cs` – DI root, host startup, DB init
- `MainWindow.xaml` + `MainViewModel.cs` – shell, scope‑per‑nav, `NavigateToStageCommand`
- `ProjectionWindow.xaml.cs` – 3‑zone rendering, `ITokenResolver`, theme cache
- `StageViewModel.cs` + `StageView.xaml` – preview records, cross‑item UP NEXT, video sync
- `SongsViewModel.cs` – two‑step search, projection, import
- `BibleViewModel.cs` – cascade, import, chapter sentinel
- `ServiceScheduleViewModel.cs` – service list, builder, live mode, auto‑advance timer
- `AddEditThemeViewModel.cs` – background type, text alignment, header/footer templates
- `BibleFormatDispatcher.cs` – auto‑detection of 8 Bible formats
- `Styles/Base.xaml` – all control styles, `FieldComboBox` full template
**Application**:
- `Slide.cs` + `SlideContext.cs` – projection DTO, token fields
- `IProjectionService.cs` + `ProjectionService.cs` – full event bus
- `ITokenResolver.cs` + `TokenResolver.cs` – regex token resolution
**Infrastructure**:
- `AppDbContext.cs` – `StampTimestamps()`, configurations
- `SongRepository.cs` – FTS lyrics search, replace‑all‑sections
- `BibleRepository.cs` – batched import, FTS sync
- `ScheduleItemConfiguration.cs` – TPH discriminator
- `InfrastructureServiceExtensions.cs` – `AddInfrastructure()`, registers `ITokenResolver`
**Domain**:
- `Song.cs` – `GetOrderedSections(string? verseOrderOverride)`
- `ScheduleItem.cs` – `AutoAdvanceSeconds`, `ThemeId`
- `Theme.cs` – `HeaderTemplate`, `FooterTemplate`
- `Testament.cs` – `Old`, `New` (G14)

## Essential Code Patterns

**ViewModel load + IsBusy guard (G4)**:

[RelayCommand]
private async Task LoadAsync() {
    if (IsBusy) return;
    IsBusy = true;
    try { ... }
    finally { IsBusy = false; }
}


**Auto‑advance timer (G19)**:

private DispatcherTimer? _autoAdvanceTimer;
private void StartAutoAdvanceTimer(int seconds) {
    StopAutoAdvanceTimer();
    _autoAdvanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
    _autoAdvanceTimer.Tick += OnAutoAdvanceTick;
    _autoAdvanceTimer.Start();
}
private void StopAutoAdvanceTimer() {
    if (_autoAdvanceTimer == null) return;
    _autoAdvanceTimer.Stop();
    _autoAdvanceTimer.Tick -= OnAutoAdvanceTick;
    _autoAdvanceTimer = null;
}
// Stop in StopLive(), OnProjectionStateChanged(false), and Dispose()


**Text alignment with OneWay binding (G17)**:

[ObservableProperty] private TextAlignment _textAlignment = TextAlignment.Center;
public bool IsAlignLeft => TextAlignment == TextAlignment.Left;
[RelayCommand] private void SetAlignment(string alignment) { ... }
// XAML: IsChecked="{Binding IsAlignCenter, Mode=OneWay}"


**Token zone visibility (G20)**:

var resolved = _tokenResolver.Resolve(template, context);
if (resolved.Any(char.IsLetterOrDigit)) { /* show zone */ }


**RelayCommand with enum string parameter (G13)**:

[RelayCommand]
private void AddSection(string? sectionTypeName) {
    Enum.TryParse<SectionType>(sectionTypeName, out var type);
}


**Bible chapter sentinel (G16)**:

partial void OnSelectedChapterChanged(int value) {
    if (value > 0 && SelectedVersion != null && SelectedBook != null)
        _ = LoadVersesAsync(...);
}


**SlidePreview immutable record**:

public sealed record SlidePreview { ... init-only properties ... }
[ObservableProperty] private SlidePreview _currentPreview = SlidePreview.Empty;


## Song Import
- `SongFormatDispatcher.Import(filePath)` (WPF/Helpers/SongImport) auto-detects format and parses to a `Song`.
- Routes by extension/content: `.txt`→PlainText; `.xml`/`.openlyrics`/`.opensong`→XML (OpenLyrics vs OpenSong by namespace — `openlyrics.info` ns = OpenLyrics, else OpenSong); unknown ext → sniff first content byte (`<`=XML).
- **OpenLyricsParser** – namespaced `<song>`; title/author/copyright/ccliNo/verseOrder/`v{n}`,`c`,`p`,`b`,`i`,`e`,`t` verses.
- **OpenSongParser** – non-namespaced `<song>`; `title`/`author`/`copyright`/`ccli`/`presentation`→VerseOrder; `[tag]` headers, skips `.`chord/`;`comment lines, leading single digit 1-9 splits stacked verses under one `[tag]`.
- **PlainTextParser** – label lines (`Verse 1`/`Chorus`/`V1`/`Pre-Chorus`…) via `LabelRegex` start sections; no labels → blank-line-separated blocks become sequential verses; title = filename.
- **SongSectionTokens** (shared, internal) – `TryParse(label)`→type+number; `NormalizeOrder(raw)`→canonical "V1 C B" tokens (preserves source digit presence; I/O/T never numbered).
- Tests: `OpenAdoration.Tests.Infrastructure/SongImport/SongParserTests.cs` (6) + fixtures.

## Feature Status (brief)
Songs (CRUD+search+projection+import: OpenLyrics/OpenSong/plain-text) – DONE  
Themes (CRUD+3‑zone+token chips) – DONE  
Bible (3‑col browser+FTS search+8‑format import+verses‑per‑slide) – DONE  
ServiceSchedule (list+builder+live+auto‑advance+verse order override) – DONE  
Media (import+project+delete) – DONE  
Projection (3‑zone+tokens+announcements+fade transition) – DONE  
StageView (previews+UP NEXT+Prev/Next Item+video) – DONE  
TokenSystem (12 tokens+auto‑hide+chip insertion) – DONE  
Settings (JSON+church tokens+default auto‑advance) – DONE  
AutoAdvance (per‑item seconds+persist+timer reset) – DONE  
Packaging (M7.5: self‑contained single‑file exe via win‑x64.pubxml + WiX v5 MSI in installer/) – DONE  

## Packaging / Release (M7.5)
- Publish profile `OpenAdoration.WPF/Properties/PublishProfiles/win-x64.pubxml` — self-contained, single-file, win-x64, ReadyToRun, compressed, native libs self-extracted (SQLite). Publish-only settings live here, NOT in the csproj (a global RuntimeIdentifier/SelfContained would break `dotnet build`/`dotnet ef`).
- `dotnet publish OpenAdoration.WPF -c Release -p:PublishProfile=win-x64` → single `OpenAdoration.exe` (~82 MB), runs on Win10+ with no .NET prerequisite.
- Installer: `installer/OpenAdoration.wxs` (WiX **v5** — v6/v7 require the paid OSMF EULA). Per-machine MSI, Program Files, Start Menu + Desktop shortcuts, ARP metadata, MajorUpgrade. Fixed UpgradeCode 94340D83-8ACA-413F-A3C8-3B71C73D8D5C.
- One-command build: `pwsh installer/build.ps1 [-Version x.y.z]` → `installer/out/OpenAdoration-<ver>-win-x64.msi` (~76 MB, gitignored).
- Requires `dotnet tool install --global wix --version 5.0.2`.

## Versioning & Roadmap
- **v1.0 shipped 2026-06-01** — Milestones 0–7 complete.
- **v2.0 in planning** — see `ROADMAP.md` (canonical) + `CHANGELOG.md`. Three milestones:
  - **M8 Reliability & Releases** — local backup/restore (`.oabak` zip; `IBackupService`/`ZipBackupService`), opt-in auto-update from GitHub releases (`IUpdateService`/`GitHubUpdateService` → `msiexec`), release infra (`CHANGELOG.md`, `docs/RELEASE.md`).
  - **M9 Content & Imports** — more song importers (ChordPro/SongPro, EasyWorship, best-effort ProPresenter) via `SongFormatDispatcher`; image-folder + PDF deck import; Bible quick-reference jump box.
  - **M10 Presentation Richness** — transition library (cut/fade/slide/zoom); persistent lower-third overlays; dual-version scripture; clean output for livestream; **media transport controls (play/pause/seek/back) for projected video** (M10.5).
  - **M11 Internationalization** — multi-language UI: `.resx` (Strings.resx + Strings.es.resx), `ILocalizationService`, `AppSettings.UiCulture` + Settings language dropdown, Spanish first locale. Tokens + Bible book names stay untranslated. Terminology reference: `docs/GUIA-USUARIO.md`.
- End-user docs: **`docs/GUIA-USUARIO.md`** (Spanish operator guide). UI is English until M11.
- New work enters as Application interface + Infrastructure impl + WPF VM/View — never cross layers.
- Release flow: bump csproj `<Version>` → update CHANGELOG → `installer/build.ps1` → tag `vX.Y.Z` → GitHub release with `OpenAdoration-<ver>-win-x64.msi` asset (auto-update parses this).

## Media / video (current state + v2.0 gap)
- Video plays via `System.Windows.Controls.MediaElement` in **ProjectionWindow** (`ContentVideo`, with audio) and **StageView** (muted preview, `LoadedBehavior=Manual`, synced in code-behind `SyncVideo()`).
- **Gap (M10.5):** projected video has **no transport controls** — no play/pause, seek, or restart. Plan: add Play/Pause/SeekRelative/Restart to `IProjectionService` (or a media-control sub-API) that drives the ProjectionWindow `MediaElement` (`Play()/Pause()/Position`), surface a transport bar in the projection control bar + Stage View when the current slide `IsVideoMedia`, and keep Stage preview position in sync.
