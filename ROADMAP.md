# OpenAdoration — Development Roadmap

> Honest baseline: the app does not work in its current state.  
> Every milestone below starts by making the stated feature actually usable  
> before adding anything new. No milestone assumes the previous one was correct.

---

## Guiding principle

A church operator opens this app 15 minutes before a service and uses it under pressure, in front of a congregation. Every feature must answer: **"does this make the operator's job easier or safer?"** If a feature isn't stable enough to trust live, it doesn't ship.

---

## Known bugs (confirmed in code, not yet run)

These are defects that exist in the source right now, independent of anything the operator does:

| # | Location | Bug | Impact |
|---|---|---|---|
| B1 | `Slide.cs` constructor | Validates `!IsNullOrWhiteSpace(content)` for ALL non-Media types — but `Slide.Blank()` also passes `string.Empty`. **Always throws `ArgumentException`.** | "Blank" button crashes the app |
| B2 | `ProjectionWindow.xaml` | Font family, size, and colour are hardcoded (`Arial`, `72pt`, `White`). `Slide.ThemeId` is never read. | Theme system has no effect on output |
| B3 | `SongsViewModel` + `DeleteSongCommand` | `RelayCommand` for async void raises an unobserved exception on the task if `LoadAsync` throws after delete — no await path for the command. | Silent data loss on delete failure |
| B4 | `MainViewModel` navigation | Resolves `SongsViewModel` (Transient) via root `IServiceProvider`. Scoped dependencies (`ISongService`) get captured in the root scope and live for the app's entire lifetime. | Memory accumulation; stale service state |

---

## Milestone 0 — Make songs actually work (current priority)

**Acceptance criteria:** operator can open the app, create a song with multiple sections, save it, see it in the list, edit it, delete it, and project it to the screen — without a crash or silent failure at any step.

Nothing else starts until this passes completely.

### 0.1 — Fix `Slide.Blank()` crash (B1)

**File:** `OpenAdoration.Application/Common/Slide.cs`

The content validation must also exempt `SlideType.Blank`:

```csharp
// Before (broken):
if (type != SlideType.Media && string.IsNullOrWhiteSpace(content))

// After (correct):
if (type is not SlideType.Media and not SlideType.Blank && string.IsNullOrWhiteSpace(content))
```

### 0.2 — Fix navigation scope leak (B4)

**Files:** `App.xaml.cs`, `MainViewModel.cs`

`MainViewModel` is Singleton and holds `IServiceProvider`. Calling `GetRequiredService<SongsViewModel>()` from the root provider captures Scoped dependencies (`ISongService`, `ISongRepository`) in the root scope — they live forever and accumulate state across navigations.

Fix: create a new scope per navigation.

```csharp
// MainViewModel.cs — replace the navigation helper:
private T ResolveView<T>() where T : BaseViewModel
{
    // Each navigation gets a fresh scope — scoped services are properly short-lived
    var scope = _services.CreateScope();
    return scope.ServiceProvider.GetRequiredService<T>();
    // Note: scope is intentionally not disposed here — the ViewModel owns its lifetime.
    // Track scope alongside CurrentView and dispose when navigating away.
}
```

Better approach — store the active scope and dispose it on navigation:

```csharp
private IServiceScope? _activeScope;

private void NavigateTo<T>() where T : BaseViewModel
{
    _activeScope?.Dispose();
    _activeScope = _services.CreateScope();
    CurrentView  = _activeScope.ServiceProvider.GetRequiredService<T>();
}
```

### 0.3 — Fix delete command exception handling (B3)

**File:** `ViewModels/SongsViewModel.cs`

`DeleteSongCommand` is generated from `async Task DeleteSongAsync(Song song)` — CommunityToolkit.Mvvm wraps it in `AsyncRelayCommand` which does handle exceptions properly. **Re-verify** by inspecting the generated code in `obj/`. If there is no unhandled exception path, close this item; otherwise add explicit try/catch around `LoadAsync()` call.

### 0.4 — End-to-end manual test checklist for Songs

Work through every item below. Log the app, watch for `[ERR]` and `[WRN]` lines. Fix whatever fails before moving on.

**App launch**
- [ ] App opens without crash
- [ ] Projection window appears on secondary monitor (or primary as fallback)
- [ ] Songs view loads (log shows `[DBG] Fetching all songs`)
- [ ] Empty state shows "No songs found." with no error banner

**Create song**
- [ ] Click "+ New Song" → edit panel appears
- [ ] Title field accepts text; "Save Song" button enables when title is not empty
- [ ] "Save Song" disabled when title is empty
- [ ] Click "+ Verse" → section card appears with label "Verse 1"
- [ ] Click "+ Chorus" → section card appears with label "Chorus"
- [ ] Click "+ Verse" again → second card appears with label "Verse 2"
- [ ] ▲ / ▼ buttons reorder sections; labels renumber correctly
- [ ] ✕ button removes a section; remaining labels renumber correctly
- [ ] Lyrics text box accepts multi-line input
- [ ] Click "Save Song" → returns to list, new song appears
- [ ] Log shows `[INF] Song created with ID X: Title`

**Edit song**
- [ ] Click "Edit" on a song → edit panel pre-populated with existing data
- [ ] Existing sections appear in correct order
- [ ] Modify title, change a section, delete a section, add a section
- [ ] Click "Save Song" → list refreshes with updated data
- [ ] Log shows `[INF] Song X updated successfully`

**Cancel**
- [ ] Click "Cancel" mid-edit → returns to list, no changes saved

**Delete song**
- [ ] Click "Delete" → confirmation dialog appears
- [ ] Click "No" → song remains
- [ ] Click "Yes" → song removed from list
- [ ] Log shows `[INF] Song X deleted successfully`

**Search**
- [ ] Type in search box → list filters in real time by title and author
- [ ] Clear search → full list returns

**Project song**
- [ ] Click "▶ Project" on a song with sections → projection window shows first section lyrics
- [ ] Log shows `[INF] Loading N slide(s) for 'Song Title'`
- [ ] "PROJECTING" label appears in the bottom bar of main window
- [ ] "Next ▶" advances through sections
- [ ] "◀ Previous" goes back
- [ ] "Blank" shows black screen; section position is preserved
- [ ] "Stop" clears projection window; "PROJECTING" label disappears

**Edge cases**
- [ ] Project a song with no sections → MessageBox says "no sections to project"; no crash
- [ ] Click "Blank" when not projecting → no crash (was broken before B1 fix)
- [ ] Rapid-click "Next" past the last slide → stays on last slide, no crash
- [ ] Rapid-click "Previous" before the first slide → stays on first slide, no crash

**Recovery**
- [ ] Force-close and reopen app → all songs still present in the list

Once every checkbox passes, Milestone 0 is done.

---

## Milestone 1 — Themes (make projected content look right)

**Why before Bible or Schedule:** Every projection feature (songs, Bible, schedule) needs theming. Songs already project but ignore the theme system entirely (B2). Fixing this now means Bible and Schedule get correct styling for free. Doing it after means retrofitting all of them.

**What an operator needs:**
- Create named themes (e.g. "Sunday Morning", "Christmas", "Hymns")
- Set font family, size, and colour
- Set background — solid colour or a background image
- Mark one theme as the default for all slides
- Override the theme for individual schedule items
- See a live preview before using it

### 1.1 — Fix `ProjectionWindow` to apply themes (B2)

**Files:** `ProjectionWindow.xaml`, `ProjectionWindow.xaml.cs`

Currently all rendering properties are hardcoded in XAML. `Slide.ThemeId` is ignored.

Changes needed:
- Inject `IServiceScopeFactory` into `ProjectionWindow` (it is Singleton — cannot directly inject Scoped `IThemeService`)
- In `RenderSlide()`, resolve a short-lived `IThemeService` scope, call `GetDefaultAsync()` or `GetByIdAsync(slide.ThemeId)` as appropriate
- Apply to `SlideTextBlock`: `FontFamily`, `FontSize`, `Foreground` (parse hex → `SolidColorBrush`)
- Apply background: if `BackgroundImagePath` set → load `BitmapImage`; else set `Grid.Background`
- Cache the last resolved `ThemeId` — only re-fetch when the ThemeId actually changes

Remove hardcoded values from `ProjectionWindow.xaml`; all styling must come from the resolved theme at runtime.

### 1.2 — Pass `ThemeId` through `GenerateSlides`

**File:** `OpenAdoration.Application/Services/ISongService.cs`, `SongService.cs`

Add `int? themeId = null` parameter to `GenerateSlides`. Pass it to each `Slide` constructor. `SongsViewModel.ProjectSong` continues to pass `null` (default theme). Schedule playback (Milestone 3) will pass `scheduleItem.ThemeId`.

### 1.3 — `ThemeViewModel` — full implementation

State: list of themes, selected theme, `IsEditing` flag.

Commands:
- `LoadCommand`
- `AddThemeCommand` — open edit panel with blank theme, FontFamily "Arial", FontSize 48, black background, white text
- `EditThemeCommand(Theme)` — open edit panel pre-populated
- `SetDefaultCommand(Theme)` — marks selected as default; service clears the old default atomically
- `DeleteThemeCommand(Theme)` — confirm dialog; the service already rejects deletion of the default theme, so display that message if `InvalidOperationException` is thrown
- `SaveCommand` (in inner `AddEditThemeViewModel`) — create or update

`AddEditThemeViewModel` properties:
- `string Name`
- `string FontFamily` — free text; validate against `System.Windows.Media.Fonts.SystemFontFamilies`
- `int FontSize` — numeric; valid range 12–200
- `string FontColor` — hex string (#RRGGBB); input via WinForms `ColorDialog` (already available via `UseWindowsForms=true`)
- `string BackgroundColor` — hex string + colour picker button
- `string? BackgroundImagePath` — file browse (`OpenFileDialog`, filter `*.jpg;*.jpeg;*.png;*.bmp`) + clear button
- `bool IsDefault`

### 1.4 — `ThemesView.xaml` + `AddEditThemeView.xaml`

Same two-panel pattern as Songs.

**List panel:** theme name, font summary ("Arial 48 · #FFFFFF on #000000"), "Default" badge, Edit / Delete / Set Default buttons.

**Edit panel:** all fields from 1.3. Crucially: a **live preview rectangle** — a `Border` + `TextBlock` bound directly to the form's properties. The operator sees what the projector will look like before saving. This is the single most important UX element of the Themes feature.

**Milestone 1 done when:** operator creates a theme named "Sunday Morning" (custom font, background image), sets it as default, projects a song, and the projection window shows the correct font and background.

---

## Milestone 2 — Bible Browser

**What an operator needs:**
- Import a Bible translation from a JSON file (once, before the service)
- Browse: choose version → book → chapter → see all verses
- Select one or more consecutive verses and project them
- Search for a phrase across the whole translation
- Delete a translation

### 2.1 — Bible JSON import format + parser

Nothing currently reads an external file and produces the `(BibleVersion, books, verses)` triple that `BibleRepository.ImportVersionAsync` expects.

**File format** (JSON — simple to find online for most translations):
```json
{
  "name": "King James Version",
  "abbreviation": "KJV",
  "language": "en",
  "books": [
    { "name": "Genesis", "abbreviation": "Gen", "testament": "Old", "bookNumber": 1, "chapterCount": 50 }
  ],
  "verses": [
    { "book": "Genesis", "chapter": 1, "verse": 1, "text": "In the beginning..." }
  ]
}
```

**New files:**
- `OpenAdoration.Application/Bible/BibleImportData.cs` — record holding the parsed output
- `OpenAdoration.Application/Bible/IBibleImporter.cs` — interface: `Task<BibleImportData> ParseAsync(string filePath, CancellationToken ct)`
- `OpenAdoration.Infrastructure/Bible/BibleJsonImporter.cs` — implementation using `System.Text.Json`

Register `IBibleImporter` → `BibleJsonImporter` as Transient in `AddInfrastructure()`.

Import is slow (~31K verses). Show a progress label ("Importing 12,000 of 31,102 verses...") and expose a Cancel button that passes a `CancellationToken` to `ImportVersionAsync`.

### 2.2 — `BibleViewModel` — version management

Commands: `LoadCommand`, `ImportVersionCommand` (file picker → parse → import), `DeleteVersionCommand`.

### 2.3 — `BibleViewModel` — browse tab

State: `SelectedVersion`, `SelectedBook`, `SelectedChapter`, `Verses`, `SelectedVerses` (multi-select), `ProjectVersesCommand`.

Browse flow: version picker → book list → chapter list (1..ChapterCount) → verse list.

Multi-verse selection: checkboxes on verse rows. Slide content joins selected verse texts; label shows range ("John 3:16" or "John 3:16–18").

### 2.4 — `BibleViewModel` — search tab

State: `SearchTerm`, `SearchResults`. Debounce 400ms. Max 50 results. Each result row has a "▶ Project" button.

### 2.5 — `BibleView.xaml`

Two-tab `TabControl`: Browse and Search. Browse tab: left column = books, middle = chapters, right = verses. Version selector at top. "▶ Project Selected" button at bottom right, enabled only when ≥1 verse selected.

**Milestone 2 done when:** operator imports KJV, browses to John 3:16, projects it, and sees the correct verse text on the projector with the active theme applied.

---

## Milestone 3 — Service Schedule

**What an operator needs:**
- Create a named service with a date (e.g. "Sunday 11am — 18 May 2026")
- Build a schedule by adding songs, Bible passages, and media in order
- Reorder items freely
- On the day: navigate the schedule live — each item automatically loads its slides into the projector

### 3.1 — Extend `IWorshipServiceService` — schedule item management

The current interface only has WorshipService CRUD. The following methods need adding:

```csharp
Task<WorshipService> GetWithItemsAsync(int serviceId, CancellationToken ct = default);
Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, CancellationToken ct = default);
Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, CancellationToken ct = default);
Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, CancellationToken ct = default);
Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default);
Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default);
```

Implement in `WorshipServiceRepository`. `ReorderItemsAsync` assigns `Order = index` for each ID in the provided list, then saves in one `SaveChangesAsync` call.

### 3.2 — `ServiceScheduleViewModel` — service picker

List all services. Commands: Create (name + date), Delete (confirm), Open (load schedule builder).

### 3.3 — `ServiceScheduleViewModel` — schedule builder

State: selected service with ordered items, `SelectedItem`.

**Adding items** — three buttons in the toolbar:
- **Add Song** — compact searchable song picker (list of songs → select → `AddSongItemAsync`)
- **Add Bible** — compact Bible reference input (book, chapter, verse range → `AddBibleItemAsync`)
- **Add Media** — pick from the media library → `AddMediaItemAsync`

Each schedule row: type icon (🎵 / 📖 / 🖼) + title/reference + optional Theme Override badge + ▲ ▼ reorder + Delete.

### 3.4 — Live mode

"▶ Start Service" switches the view to live mode.

Layout: compact schedule list on the left (current item highlighted) + slide info on the right + "Next Item →" / "← Prev Item" / "Blank" / "Stop" controls at the bottom.

Navigation: selecting a schedule item loads its slides into `ProjectionService`:
- `SongScheduleItem` → `SongService.GenerateSlides(song, item.ThemeId)`
- `BibleScheduleItem` → fetch verses → `BibleService.GenerateSlide(verses)`
- `MediaScheduleItem` → `MediaService.GenerateSlide(file)`

Within-item prev/next → `ProjectionService.Previous()` / `Next()`.

**Milestone 3 done when:** operator builds a 3-song + 2-Bible-reading service, starts it, presses Next through the whole service, and the projector shows the right content at every step with no manual intervention.

---

## Milestone 4 — Media

**What an operator needs:**
- Register image files in the app's media library
- Browse the library with thumbnails
- Project an image to the secondary screen
- Remove files from the library

`ProjectionWindow` already handles `SlideType.Media` — the backend is complete.

### 4.1 — Copy-on-import

When a file is imported: copy it to `%LocalAppData%\OpenAdoration\Media\` and store the copied path in `MediaFile.FilePath`. This makes the app self-contained — file references never break if the operator moves the original.

### 4.2 — `MediaViewModel` — full implementation

Commands: `LoadCommand`, `ImportFileCommand` (OpenFileDialog, `*.jpg;*.jpeg;*.png;*.bmp`), `DeleteFileCommand` (confirm + delete copied file), `ProjectFileCommand`.

### 4.3 — `MediaView.xaml`

WrapPanel of image cards: thumbnail (160×90) + filename + Project / Delete buttons. "Import Image" button in the toolbar.

Video files are out of scope for MVP — too complex to thumbnail and play reliably. Images cover 90% of church use cases (logos, announcement slides, background images).

**Milestone 4 done when:** operator imports a church logo image, projects it, sees it full-screen on the projector with the active theme background behind it.

---

## Milestone 5 — Keyboard Shortcuts

Every important action needs a keyboard shortcut. An operator running a live service cannot reach for the mouse.

| Key | Action |
|---|---|
| `Space` / `→` / `Page Down` | Next slide |
| `←` / `Page Up` / `Backspace` | Previous slide |
| `B` | Blank screen |
| `Escape` | Stop projection |
| `1`–`9` | Jump to slide N within current item |
| `Ctrl+1` | Navigate to Songs |
| `Ctrl+2` | Navigate to Bible |
| `Ctrl+3` | Navigate to Schedule |

Implementation: `KeyDown` handler in `MainWindow.xaml.cs` (not XAML bindings — some keys get consumed by focused controls before bindings fire). Guard slide-navigation shortcuts with `IsProjecting` check so they don't fire during data entry.

---

## Milestone 6 — Projection Preview Panel

An operator on a single monitor (laptop) cannot see what the projector shows. Even with a secondary monitor, a preview helps prepare the next slide.

- Small preview thumbnail in the main window, bound to `ProjectionService.CurrentSlide`, styled to mirror the active theme
- Slide navigator: list of all slides in the current item, current one highlighted, click to jump (`ProjectionService.GoTo(index)`)
- Collapsible — operators who trust the projector can hide it to reclaim screen space

---

## Milestone 7 — Polish & Release

### 7.1 — Input validation across all views
- Song title: max 200 characters enforced in UI
- Theme: font size range (12–200); invalid hex colour shows inline error
- Bible import: schema validation with clear error messages on malformed JSON

### 7.2 — Error banners in all views
Every feature view must render the `ErrorMessage` / `HasError` banner from `BaseViewModel`. Currently only `SongsView` does this.

### 7.3 — First-run experience
- No songs → "Add your first song →" call-to-action instead of empty list
- No Bible version → "Import a Bible translation to get started" prompt in BibleView
- No theme beyond Default → note in ThemesView

### 7.4 — About & keyboard shortcut reference
Accessible from a `?` button in the toolbar.

### 7.5 — Publish
```bash
dotnet publish OpenAdoration.WPF -c Release -r win-x64 --self-contained true
```
Package with NSIS or WiX installer. Target: single `.exe`, no .NET prerequisite.

---

## What is explicitly out of scope for MVP

| Feature | Reason deferred |
|---|---|
| Video playback | `MediaElement` state management is complex; images cover 90% of need |
| Multi-user / network sync | One PC per service is universal in small churches |
| Cloud backup | Violates offline-first principle |
| CCLI licence tracking | Useful but not blocking |
| Remote control (phone app) | Requires a local HTTP server — future feature |
| OSIS/Sword/USFM Bible import | JSON covers the practical need at far lower complexity |
| Drag-to-reorder in schedule | Up/Down buttons are sufficient for MVP |

---

## Build sequence

```
Milestone 0           Milestone 1       Milestone 2       Milestone 3
Songs stable    →     Themes applied →  Bible working  →  Full schedule
(nothing works        (projection        (covers 80%       (pre-planned
right now)             looks right)       of services)      service workflow)

Milestone 4       Milestone 5       Milestone 6       Milestone 7
Media        →    Shortcuts    →    Preview      →    Polish + ship
(complete          (live-safe)       (single-           (production
 feature set)                         monitor UX)         ready)
```

Each milestone leaves the app in a better, shippable state than before it. No milestone introduces new features on top of broken ones.

---

## Summary

| Milestone | Goal | Effort |
|---|---|---|
| **0 — Songs stable** | Fix known bugs; verify every songs action works end-to-end | Medium |
| **1 — Themes** | Apply themes to projection; Themes CRUD UI + live preview | Medium |
| **2 — Bible** | JSON importer; browse + search + project UI | Large |
| **3 — Schedule** | Service builder; live navigation mode | Large |
| **4 — Media** | Import images; project to screen | Small |
| **5 — Shortcuts** | Keyboard navigation for live use | Small |
| **6 — Preview** | In-window projection thumbnail + slide list | Small |
| **7 — Polish** | Validation, error states, first-run UX, installer | Medium |
