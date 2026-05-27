# Session Status — OpenAdoration WPF

_Last updated: 2026-05-27_

---

## Current build state

```
Build:    SUCCEEDED  (dotnet build OpenAdoration.sln --configuration Debug)
Errors:   0
Warnings: 0
Tests:    8/8 pass  (dotnet test OpenAdoration.Tests.Infrastructure)
Last commit: afc5476  feat(live): edit service queue while live
Branch: master (11 commits ahead of origin)
```

---

## Completed milestones

| Milestone | Status |
|---|---|
| M0 — Songs (CRUD + projection + section validation) | DONE |
| M0b — Projection UX (hidden window, corner label, preview thumbnail, Open/Close toggle) | DONE |
| M1 — Themes (full CRUD + BackgroundType + ColorPicker + projection applies theme) | DONE |
| M2 — Bible Browser (3-column, click-to-project, FTS search) | DONE |
| M2b — Bible importer hardening (cancel, summary, exception catches, 5 tests) | DONE |
| M2c — BibleSuperSearch JSON/ZIP/SQLite parsers; 8/8 tests | DONE |
| M3 — Service Schedule (service list + builder + live mode) | DONE |
| Bible UI round-2 (chapter highlight, preview tracks projection, live theme refresh, blank keeps theme bg, version persistence, main-window Prev/Next works for Bible via full-chapter slides) | DONE |
| M4 — Media (import, thumbnail cards, project image/video, delete) | DONE |
| M4b — Video media slides play with audio; theme bg video muted | DONE |
| Service schedule — Add media items; visual Bible verse range picker | DONE |
| Live editing — Add songs / Bible / media to queue while service is running | DONE |
| M5 — Keyboard Shortcuts (Space/→/PageDown=Next, ←/PageUp=Prev, B=Blank, Esc=Stop, 1–9=GoTo, Ctrl+1–5=nav tabs) | DONE |
| M6a — App icon (Assets/openadoration.ico, purple cross design) + About screen (version, shortcuts ref, license) in Help menu bar | DONE |
| M6b — Unhandled exception handler (DispatcherUnhandledException + TaskScheduler + AppDomain) | DONE |
| Runtime bug fixes — same-page nav guard (no reload on Ctrl+N×2); live service scope/VM preserved on navigation; About moved from sidebar to Help menu | DONE |
| QA/Security round 1 — XML hardening (DtdProcessing.Prohibit, XmlResolver=null, MaxChars); ZIP bomb guards (entry count, per-entry size, line count, compression ratio, MaxInfoJsonBytes, MaxDepth, MaxTotalUncompressed, MaxLineLength, MaxVerseTextLength); BibleJsonImporter.cs dead file deleted; MediaViewModel path traversal guard; WorshipServiceRepository.ReorderItemsAsync ID set validation; BibleViewModel CancellationToken propagation; DispatcherProgress threading fix | DONE |
| QA/Security round 2 — ARCHITECTURE.md blank-slide docs corrected; DispatcherUnhandledException now resets projection on non-fatal; MediaViewModel extension + size validation on import; WorshipServiceRepository.GetWithItemsAsync TryGetValue with descriptive InvalidOperationException | DONE |

---

## What exists now (full feature map)

### Songs
- Full CRUD with section editor (Verse / Chorus / Bridge / Pre-Chorus / Intro / Outro / Tag)
- Search by title (live filter)
- Project: loads all sections as slides; main-window ◀/▶ navigates between sections
- Corner label shows song title + section name on projection
- Classification field (e.g. Worship, Hymn)

### Themes
- Full CRUD with live preview
- Background: solid color | image | video (looping, muted — ambient only)
- Font family (8 choices), font size ±2 stepper, text alignment (L/C/R)
- xctk:ColorPicker for font and background color
- Default theme flag; projection resolves theme per slide via IServiceScopeFactory + per-session cache
- Theme changes propagate instantly to active projection (ThemeChanged event pipeline)

### Bible Browser
- 3-column layout: book tree (OT/NT grouped) → chapter tiles → verse list
- Click chapter tile → loads full chapter as individual slides; main-window ◀/▶ navigates verses
- Selected chapter tile highlighted (IntEqualityConverter MultiBinding)
- Projection syncs verse highlight and preview pane as operator navigates
- Multi-verse selection: Ctrl+↓/↑ extends/shrinks selection; projects as combined slide
- Reference bar: type "John 3" or keyword; autocomplete book suggestions; Enter to search
- Keyword mode: FTS5 full-text search across all verses in the selected version
- Freeze mode: holds current slide on screen while operator browses to the next
- + Schedule: adds selected verse(s) to the open service
- 8-format import (Zefania, OSIS, USFX, thiagobodruk JSON, OpenAdoration JSON, BibleSuperSearch JSON/ZIP/SQLite)
- Localized book names preserved from source files
- Selected Bible version persists across navigations (static field survives transient VM disposal)

### Media
- Import: validates extension and file size (≤ 1 GB); copies to `%LocalAppData%\OpenAdoration\Media\` (conflict resolution with `(n)` suffix)
- 180px cards in wrap layout: image thumbnail | 🎬 overlay for videos | filename | ▶ Project | ✕ Delete
- Selected card highlighted with AccentBrush border
- Image projection: BitmapImage decoded at 1920px max
- Video projection: MediaElement plays with audio at system volume (stops at end, no loop)
- Theme background video: always muted (ambient loop only)

### Service Schedule
- Service list: create, open, delete services
- Builder: add songs (search picker), Bible passages (visual verse range picker), media files
  - Bible picker: loads verse list for selected chapter; click to set start, click further verse to extend range
  - Reorder items (▲/▼); delete items
- Live mode:
  - Left panel: clickable item list (accent highlight on current item)
  - Right panel: "Now projecting" title
  - Queue toolbar: add Song / Bible / Media on the fly during the service
  - Prev Item / Next Item navigation; "Item X of Y" counter
  - Main-window ◀/▶ navigates slides within the current item
  - Stopping projection from main bar exits live mode cleanly

### Projection Engine
- Singleton ProjectionService: LoadSlides, Next, Previous, GoTo, ShowBlank, Stop
- Events: SlideChanged, ProjectionStateChanged, ThemeChanged
- ProjectionWindow: secondary monitor fullscreen; single monitor = floating 800×450 preview
- Layers (bottom to top): ThemeBackground (color) → ThemeBackgroundImage → ThemeBackgroundVideo → BackgroundImage (media image) → ContentVideo (media video) → TextViewbox → BlankOverlay → CornerLabel
- Per-session theme cache (ConcurrentDictionary); clears on ThemeChanged or Stop
- Blank slide: fully black screen — BlankOverlay (opaque black fill) covers all layers including theme background

---

## Remaining milestones (to beta)

| # | Feature | Beta-blocking? | Notes |
|---|---|---|---|
| M6c | Packaging | **Yes** | MSIX or Setup; users shouldn't run via `dotnet run` |
| M6d | Video playback controls | Recommended | Pause/seek/mute toggle for media video slides in live mode |
| M6e | Presenter view | Nice to have | Operator sees current + next slide; audience sees projection only |
| M6f | Per-song theme assignment | Nice to have | Song carries its own theme; overrides default at projection time |

---

## Open backlog (deferred, not beta-blocking)

| # | Item | Notes |
|---|---|---|
| B1 | Logging privacy policy | Decide what is/isn't logged; user-facing export policy |
| B2 | Song list performance | GetAll loads full Sections; needs lightweight summary query (ISongRepository change) |
| B3 | ListView virtualization | Media / ServiceSchedule / Bible / AddEditSong use ItemsControl-in-ScrollViewer; no UI recycling |
| B4 | User-facing error messages | Scattered "Could not load…" strings; needs consistent wording and copy review |

---

## Key technical gotchas (active)

- **G1** UseWindowsForms=true — always fully-qualify `System.Windows.Controls.UserControl`, `Microsoft.Win32.OpenFileDialog`, `System.Windows.Media.Color`, `System.Windows.Input.KeyEventArgs`
- **G4** IsBusy guard — `LoadAsync` opens with `if (IsBusy) return;`. Never set IsBusy before calling it.
- **G10** FieldComboBox needs full ControlTemplate on ItemContainerStyle — do not simplify
- **G11** Scope-per-navigation — always resolve page VMs via `NavigateTo<T>()`, never from root IServiceProvider
- **G14** Testament enum — values are `Old` / `New`, NOT `OldTestament` / `NewTestament`
- **G16** BibleViewModel.SelectedChapter — int, 0 = sentinel for "none selected"
- **G17** AlignToggleButton IsChecked must be `Mode=OneWay`
- **G18** `Run.Text` on read-only properties must be `Mode=OneWay`
- **Live service nav** — `MainViewModel` stores both `_liveServiceScope` and `_liveServiceVm` (the actual Transient instance); restores by setting `CurrentView = _liveServiceVm` directly, never via `GetRequiredService` (which would create a new instance)
- **IntEqualityConverter** — used for chapter tile highlight and media card selected state (MultiBinding on two ints)
- **BibleVersePickerItem** — used in service schedule Bible picker; `_versePickerAnchor`/`_versePickerEnd` int fields in VM track selection
- **ThemeChanged pipeline** — `AddEditThemeViewModel.SaveAsync` → `IProjectionService.NotifyThemeChanged` → `ProjectionWindow.OnThemeChanged` (clears cache, calls `RefreshCurrentSlide`)
- **ContentVideo vs ThemeBackgroundVideo** — `ContentVideo` MediaElement is for media slide videos (plays with audio); `ThemeBackgroundVideo` is for theme background (always muted, loops via `MediaEnded`)
- **DispatcherUnhandledException policy** — non-fatal: calls `IProjectionService.Stop()` (best-effort reset), then marks handled; fatal (OOM/AV): shows dialog, lets WPF terminate
