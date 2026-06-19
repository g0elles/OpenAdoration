# OpenAdoration — Development Roadmap

> This roadmap reflects the **actual state of the codebase**.
> **v1.0 shipped 2026-06-01** — Milestones 0–7 are complete (see the version log below).
> **v2.0 is now in planning** — Milestones 8–13 (Reliability & Releases, Content & Imports, Presentation Richness, Internationalization, VideoPsalm Migration, Plugins).
> Every milestone starts by verifying the feature truly works before adding anything new.

---

## v2.0 progress snapshot — real state (verified against code 2026-06-18)

Milestones were **not** done in order — church priorities pulled M12 (VideoPsalm),
M10.5 (video transport) and FFME forward; M8/M9/most of M10 were leapfrogged.
All v2.0 work below is integrated on `master` (PR #16, `ebbc386`) but **intentionally unshipped**
— no version tag / MSI / GitHub release until the remaining roadmap items are done.

| Item | Verdict |
|---|---|
| M8.1 Backup/Restore (`.oabak`) | ✅ Done — `IBackupService`/`ZipBackupService`, Settings UI, staged DB swap on restart |
| M8.2 Auto-update (`IUpdateService`) | ✅ Done — `GitHubUpdateService` (releases/latest → SemVer → MSI), opt-in startup check, Settings UPDATES |
| M8.3 Release infra + CI/CD | ✅ Done (CHANGELOG, RELEASE.md, build.ps1, GitHub Actions: ci/release/codeql) |
| M9.1 More song importers | 🔶 ChordPro done (`.cho/.crd/.chopro/.chordpro`); EasyWorship + ProPresenter **moved to backlog** (blocked — need real export samples; see Backlog below) |
| M9.2 Image-folder / PDF / pptx decks | 🔶 Image-folder import done; PDF + pptx decks deferred (native dep: Docnet/PDFium) |
| M9.3 Bible quick-reference jump | ✅ Done (`ParseReference` + `BibleReferenceParser`; consolidated to one smart search box 2026-06-18) |
| M10.1 Transition library | ✅ Done — Cut / Fade / Slide / Zoom (`SlideTransitionKind`) |
| M10.2 Persistent lower-thirds | ✅ Done — `ShowLowerThird`/`ClearLowerThird`, flush-bottom bar, operator controls |
| M10.3 Dual-version scripture | 🚫 Dropped (2026-06-18 QA — operator found the secondary picker confusing; built then removed) |
| M10.4 Clean livestream output | ❌ Not started (stretch — clean output window, optional NDI) |
| M10.5 Media transport controls | ✅ Done (v1.1) + FFME any-codec engine (bonus) |
| M11 i18n | ✅ en/es done — full externalization, `MultiLanguageEnabled` ON, Settings language picker; more languages = add a resx |
| M12 VideoPsalm migration | ✅ Done (GUI-verified 2026-06-16) |
| M13 Plugins | 🔶 Core DONE (13.1–13.3: contract, loader, Settings→Plugins UX, GUI-verified); **13.4 api.bible connector NOT started (separate repo)** |
| M14 Content-level theming | 🔶 In progress — **14.1–14.4 + per-theme `SlideTransition` done** (`Song.ThemeId` + per-content-type defaults + migration; `ThemeCascade` resolver everywhere; song-editor + Settings "Content themes" pickers; VP import folded into the cascade, guarded; `Theme.SlideTransition` nullable override → projection falls back to global, theme-editor picker, e2e round-trip verified; M14.5 color-emoji → Segoe Fluent Icons (🔍🖼🎬📖, GUI-verified)). Remaining: DynamicResource (G27); M14.5 optional full geometric-glyph unification (✕✎★▲▼◀▶+− render consistently already — deferred as churn/risk) |

---

## Guiding principle

A church operator opens this app 15 minutes before a service and uses it under pressure, in front of a congregation. Every feature must answer: **"does this make the operator's job easier or safer?"** If a feature isn't stable enough to trust live, it doesn't ship.

---

## Known bugs — status

| # | Location | Bug | Status |
|---|---|---|---|
| B1 | `Slide.cs` constructor | `Slide.Blank()` always threw `ArgumentException` because content validation did not exempt `SlideType.Blank` | **Fixed** — `type is not SlideType.Media and not SlideType.Blank` |
| B2 | `ProjectionWindow` | Font, size, and colour were hardcoded; `Slide.ThemeId` was ignored | **Fixed** — default theme is loaded and applied; per-slide `ThemeId` override now resolved from the in-session cache |
| B3 | `SongsViewModel.DeleteSongCommand` | Risk of unobserved exception on async delete failure | **Fixed** — `AsyncRelayCommand` catches command-level exceptions; `LoadAsync()` has its own try/catch and never throws to the caller. Manual UI test still required (M0 checklist). |
| B4 | `MainViewModel` navigation | Root-scope resolution of Transient ViewModels captured Scoped services permanently | **Fixed** — `NavigateTo<T>()` now creates a fresh `IServiceScope` per navigation and disposes the previous one |
| B6 | `MainWindow.xaml.cs` | Projection window auto-opened on every app launch | **Fixed** — no auto-open; window shown only when operator clicks "Open Screen" or projects the first slide. `EnsureShown()` handles dual/single screen. |
| B7 | `ProjectionWindow.xaml` | Song title and section label not shown on projection screen | **Fixed** — `CornerLabel` (top-left overlay) shows `ContextLabel` + `slide.Label` via `UpdateCornerLabel()` |

---

## Current feature state (v1.0 — shipped)

> Updated 2026-06-01. Every feature below is implemented end-to-end and builds clean (43/43 tests green).

| Feature | Status | Notes |
|---|---|---|
| Songs | ✅ Done | CRUD, two-step search, projection, VerseOrder, Copyright/CCLI, import (OpenLyrics / OpenSong / plain text) |
| Bible | ✅ Done | 3-column browser, FTS keyword+phrase search, 8-format import, verses-per-slide |
| Themes | ✅ Done | CRUD, 3-zone Header/Body/Footer, token chips, background colour/image/video |
| Service Schedule | ✅ Done | Builder + live mode + per-item auto-advance + per-item verse-order override |
| Media | ✅ Done | Import, project, delete (images **and** video) |
| Keyboard shortcuts | ✅ Done | Space/arrows/B/Esc/1–9/Ctrl+1–5 |
| Stage View | ✅ Done | Themed previews, cross-item UP NEXT, Prev/Next Item (fulfils the M6 operator-preview goal) |
| Projection | ✅ Done | 3-zone tokens, announcement banner, configurable fade transition |
| Settings | ✅ Done | settings.json; church tokens; default auto-advance / verses-per-slide |
| Packaging | ✅ Done | Self-contained single-file exe + WiX v5 MSI (M7.5) |

---

## Milestone 0 — Songs: verify and stabilise

**Goal:** operator can open the app, create a song with multiple sections, save it, see it in the list, edit it, delete it, and project it — without a crash or silent failure at any step.

Nothing else starts until this passes completely.

### 0.1 — End-to-end manual test checklist for Songs

Work through every item below. Watch the log for `[ERR]` and `[WRN]` lines. Fix whatever fails.

**App launch**
- [ ] App opens without crash
- [ ] Projection window is NOT shown at startup (B6 fix — window stays hidden until "Open Screen" is clicked or first projection fires)
- [ ] Songs view loads; empty state shows "No songs yet" with no error banner

**Create song**
- [ ] Click "+ New" → edit panel appears
- [ ] Click "Save Song" with empty title → inline error "Song title is required." (button is always enabled; validation fires on click)
- [ ] "+ Verse" adds a section card labelled "Verse 1"
- [ ] "+ Chorus" adds a section labelled "Chorus"
- [ ] Second "+ Verse" adds "Verse 2"
- [ ] ▲ / ▼ reorder sections; labels renumber correctly
- [ ] ✕ removes a section; remaining labels renumber
- [ ] Lyrics text box accepts multi-line input
- [ ] "Save" returns to list; new song appears
- [ ] Log shows `[INF] Song created with ID X: Title`

**Edit song**
- [ ] Click "✎" → edit panel pre-populated with existing data
- [ ] Modify title, sections; "Save" updates the list
- [ ] Log shows `[INF] Song X updated successfully`

**Cancel**
- [ ] Click "Cancel" mid-edit → returns to list, no changes saved

**Delete song**
- [ ] Click "✕" → confirmation dialog appears
- [ ] "No" → song remains; "Yes" → song removed
- [ ] Log shows `[INF] Song X deleted successfully`

**Search**
- [ ] Typing filters the list in real time
- [ ] Clearing the search restores the full list

**Project song**
- [ ] Click "▶" → projection window shows first section
- [ ] Log shows `[INF] Loading N slide(s) for 'Title'`
- [ ] "PROJECTING" indicator appears in the main window bottom bar
- [ ] "Next ▶" and "◀ Prev" navigate slides correctly
- [ ] "Blank" shows black screen; position is preserved
- [ ] "Stop" clears projection window; indicator disappears

**Edge cases**
- [ ] Project a song with no sections → inline error banner "This song has no lyrics to project."; no crash
- [ ] Click "Blank" when not projecting → no crash
- [ ] Rapid "Next" past the last slide → stays on last slide, no crash
- [ ] Rapid "Previous" before the first slide → stays on first slide, no crash
- [ ] Force-close and reopen → all songs still in the list

**Milestone 0 done when:** every checkbox above passes.

---

## Milestone 1 — Themes and Bible: verify and stabilise

**Why together:** Both features are built. The goal is to verify they work end-to-end, then ensure the projection window applies the operator's chosen theme correctly.

### 1.1 — Verify theme application on projection

`ProjectionWindow` now resolves the active theme per slide:
- Default theme: loaded once per session, cached
- Per-slide override: `slide.ThemeId` → `IThemeService.GetByIdAsync()`, cached by ID
- Session caches are cleared on Stop so the next service picks up any theme edits

**Test checklist:**
- [ ] Create a theme "Sunday Morning" — Arial, 48pt, white text, dark background
- [ ] Set it as default
- [ ] Project a song → projection window uses that font and background
- [ ] Change the default theme name/colour → Stop and re-project → new theme applies
- [ ] Create a second theme "Christmas" — different font and colour
- [ ] (Schedule will pass `themeId` for per-item overrides in M3 — verify the plumbing compiles for now)

### 1.2 — End-to-end manual test checklist for Themes

**Create and set default**
- [ ] Click "+ New" → edit panel appears with default values
- [ ] Fill in name, font family, font size, font colour, background colour
- [ ] "Save" → theme appears in list
- [ ] "Set Default" → "DEFAULT" badge appears; previous default loses it
- [ ] Log shows theme created/updated/default changed

**Edit theme**
- [ ] Click "✎" → panel pre-populated; modify and save → list updates

**Delete theme**
- [ ] Delete a non-default theme → removed from list
- [ ] Delete the default theme → error message shown (service rejects it)

**Background image**
- [ ] Browse to an image → preview shows it; projection window uses it as background

### 1.3 — End-to-end manual test checklist for Bible

The Bible feature has five format importers (Zefania XML, OSIS XML, USFX XML, Thiagobodruk JSON, OpenAdoration JSON) and a full Browse + Search UI.

**Import**
- [ ] Click "Import" → file picker opens (XML/JSON filter)
- [ ] Select a valid Zefania XML file → import completes; version appears in list
- [ ] Select a file larger than 100 MB → blocked with a clear error message *(already implemented in `BibleFormatDispatcher`; this is a verification step)*
- [ ] Select a file with invalid XML → error message shown; no crash
- [ ] Log shows import start and completion with book/verse counts

**Browse**
- [ ] Select a version → books appear, grouped by Testament
- [ ] Select a book → chapters appear
- [ ] Select a chapter → verses appear
- [ ] Click "▶ Project" on a verse → projection window shows verse text with active theme

**Search**
- [ ] Type a phrase → results appear (debounced)
- [ ] Click "▶ Project" on a result → correct verse projected

**Delete**
- [ ] Delete a version → removed from list; log confirms

**Milestone 1 done when:** all Themes and Bible checklists above pass with the active theme applied on projection.

---

## Milestone 2 — Bible importer hardening ✅ DONE (2026-05-19)

### Delivered

**2.1 — Import progress and cancellation**
- `CancelImportCommand` in `BibleViewModel` calls `_importCts?.Cancel()` to abort the DB-write phase
- Cancel button added to the busy overlay in `BibleView.xaml` (visible only when `IsImporting = true`)
- `ImportSummary` / `HasImportSummary` properties drive a green success bar below the toolbar: *"Imported 31,102 verses (KJV)"* — set after `LoadVersionsCoreAsync` completes on success

**2.2 — Schema validation with clear error messages**

Named `catch` blocks in `BibleImportService.GetUserMessage` before the generic fallback:

| Exception | Message shown |
|---|---|
| `FileNotFoundException` | The selected file could not be opened. |
| `InvalidOperationException` | The exception's message shown verbatim |
| `System.Xml.XmlException` | The file could not be read as XML. It may be corrupted or in an unsupported encoding. |
| `System.Text.Json.JsonException` | The file could not be read as JSON. It may be corrupted or in an unsupported format. |
| `InvalidDataException` | Invalid file format: {dispatcher message} |
| `OperationCanceledException` | Silent clear — no error banner (operator chose to cancel) |

**2.3 — Parser test fixtures**
- `OpenAdoration.Tests.Infrastructure` project created (`net10.0-windows`, xunit 2.9.3)
- 5 fixture files: `zefania_minimal.xml`, `osis_minimal.xml`, `usfx_minimal.xml`, `thiagobodruk_minimal.json`, `openadoration_minimal.json` (1 book, 1 chapter, 3 verses each)
- `BibleParserTests.cs`: one `[Fact]` per format via `BibleFormatDispatcher.Import()` — **5/5 pass**
- Added to `OpenAdoration.sln`

---

## Milestone 2 Addendum — BibleSuperSearch format support

**What this is:** User provided real-world Bible files from biblesupersearch.com in three container formats. All three are currently unsupported. Analysis done 2026-05-19.

### Format inventory

| Format | Extension | Structure | Book numbering |
|---|---|---|---|
| BSS ZIP | `.zip` | `info.json` (metadata) + `verses.txt` (pipe-delimited) | Integer 1–66 (OSIS canonical) |
| BSS JSON | `.json` | `{"metadata":{...}, "verses":[...]}` flat verse array | Integer 1–66; `book_name` string also present |
| BSS SQLite | `.sqlite` | `verses(id,book,chapter,verse,text)` + `meta(field,value)` tables | Integer 1–66 (OSIS canonical) |

All three use the same 1–66 book numbering as `OsisBookCatalog`.

### 2.4 — BibleSuperSearch JSON parser

**Detection:** root object with both `"metadata"` and `"verses"` keys (add branch in `ClassifyAndParseJsonObject` before the existing `"books"` check).

**Parser:** `BibleSuperSearchJsonParser` — stream `verses` array, group by book to build `BibleBook` list (using `book_name` + `book` integer for `BookNumber`), derive `ChapterCount` from the max chapter seen per book.

Version metadata from `metadata.name`, `metadata.shortname`, `metadata.lang_short`.

### 2.5 — BibleSuperSearch ZIP parser

**Detection:** `.zip` extension — add branch in dispatcher before `TryAll`.

**Parser:** `BibleSuperSearchZipParser` — open with `System.IO.Compression.ZipFile` (BCL, no new dependency). Read `info.json` for version metadata. Read `verses.txt` skipping `#` lines, split on `|`, take columns 0–3 (book, chapter, verse, text). Use `OsisBookCatalog.GetByNumber(int)` reverse lookup for book names.

**New helper:** `OsisBookCatalog.GetByNumber(int bookNumber)` — build a `Dictionary<int, BookInfo>` lazily from the existing catalog. Add alongside `GetOrFallback()`.

### 2.6 — BibleSuperSearch SQLite parser

**Detection:** `.sqlite` extension — add branch in dispatcher.

**Parser:** `BibleSuperSearchSqliteParser` — use `Microsoft.Data.Sqlite` (already transitively available via EF Core). Open connection, query `meta` for version fields, query `verses ORDER BY book, chapter, verse`. Use `OsisBookCatalog.GetByNumber(int)` for book names.

**Note:** Parser opens its own `SqliteConnection` — does not use EF Core or the app's `AppDbContext`. This is a read-only import path; no DI required.

### File dialog and test updates

- `BibleFormatDispatcher.FileDialogFilter`: add `|BibleSuperSearch ZIP|*.zip|BibleSuperSearch SQLite|*.sqlite`
- Three new fixture files + three new test methods in `OpenAdoration.Tests.Infrastructure`

**Milestone 2 Addendum done when:** all three BSS formats import successfully, 8/8 parser tests pass.

---

## Milestone 3 — Service Schedule

**What an operator needs:**
- Create a named service with a date (e.g. "Sunday 11am — 18 May 2026")
- Build a schedule by adding songs, Bible passages, and media in order
- Reorder items freely
- On the day: navigate the schedule live — each item automatically loads its slides

### 3.1 — Extend `IWorshipServiceService` — schedule item management

The current interface has WorshipService CRUD only. Add:

```csharp
Task<WorshipService> GetWithItemsAsync(int serviceId, CancellationToken ct = default);
Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, CancellationToken ct = default);
Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, CancellationToken ct = default);
Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, CancellationToken ct = default);
Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default);
Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default);
```

`ReorderItemsAsync` assigns `Order = index` for each ID in the provided list, then saves in one `SaveChangesAsync` call.

### 3.2 — `ServiceScheduleViewModel` — service picker

List all services. Commands: Create (name + date), Delete (confirm), Open (load schedule builder).

### 3.3 — `ServiceScheduleViewModel` — schedule builder

State: selected service with ordered items, `SelectedItem`.

**Adding items** — three toolbar buttons:
- **Add Song** — searchable song picker (list → select → `AddSongItemAsync`)
- **Add Bible** — reference input (book, chapter, verse range → `AddBibleItemAsync`)
- **Add Media** — pick from media library → `AddMediaItemAsync`

Each schedule row: type icon (🎵 / 📖 / 🖼) + title/reference + optional Theme Override badge + ▲ ▼ + Delete.

### 3.4 — Live mode

"▶ Start Service" switches to live mode.

Layout: compact schedule list on the left (current item highlighted) + slide info on the right + "Next Item →" / "← Prev Item" / "Blank" / "Stop" at the bottom.

Selecting a schedule item loads its slides into `ProjectionService`:
- `SongScheduleItem` → `SongService.GenerateSlides(song, item.ThemeId)` ← themeId wired
- `BibleScheduleItem` → fetch verses → `BibleService.GenerateSlide(verses, item.ThemeId)` ← themeId wired
- `MediaScheduleItem` → `MediaService.GenerateSlide(file, item.ThemeId)` ← themeId wired

Within-item prev/next → `ProjectionService.Previous()` / `Next()`.

**Milestone 3 done when:** operator builds a 3-song + 2-Bible service, starts it, presses Next through the whole service, and the projector shows the correct content at every step — with per-item theme overrides working.

---

## Milestone 4 — Media

**What an operator needs:**
- Register image files in the media library
- Browse the library with thumbnails
- Project an image to the secondary screen
- Remove files from the library

`ProjectionWindow` already handles `SlideType.Media`.

### 4.1 — Copy-on-import

When a file is imported: copy it to `%LocalAppData%\OpenAdoration\Media\` and store the copied path in `MediaFile.FilePath`. File references never break if the operator moves the original.

### 4.2 — `MediaViewModel` — full implementation

Commands: `LoadCommand`, `ImportFileCommand` (OpenFileDialog, `*.jpg;*.jpeg;*.png;*.bmp`), `DeleteFileCommand` (confirm + delete copied file), `ProjectFileCommand`.

### 4.3 — `MediaView.xaml`

WrapPanel of image cards: thumbnail (160×90) + filename + Project / Delete buttons. "Import Image" button in the toolbar.

Video files are out of scope for MVP — images cover 90% of church use cases.

**Milestone 4 done when:** operator imports a church logo, projects it, sees it full-screen on the projector.

---

## Milestone 5 — Keyboard Shortcuts

Every important live action needs a keyboard shortcut. An operator cannot reach for the mouse mid-service.

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

**Implementation:** `KeyDown` handler in `MainWindow.xaml.cs` (not XAML bindings — some keys are consumed by focused controls before bindings fire). Guard slide-navigation shortcuts with `IsProjecting` so they don't fire during data entry.

**Milestone 5 done when:** operator navigates an entire service using only the keyboard.

---

## Milestone 6 — Projection Preview Panel

An operator on a single monitor cannot see what the projector shows. A preview helps prepare the next slide.

- Small preview thumbnail in the main window, bound to `ProjectionService.CurrentSlide`, styled to mirror the active theme
- Slide navigator: list of all slides in the current item, current one highlighted, click to jump (`ProjectionService.GoTo(index)`)
- Collapsible — operators who trust the projector can hide it

The data (`CurrentSlide`, `ContextLabel`, `PreviewText`, `PreviewIsBlank`) already flows from `ProjectionService` into `MainViewModel` — this milestone is the UI layer on top.

**Milestone 6 done when:** a single-monitor operator can see what the projector displays and navigate slides without looking at the projection screen.

---

## Milestone 7 — Polish & Release

### 7.1 — Input validation across all views
- Song title: max 200 characters enforced in UI
- Theme: font size range 12–200; invalid hex colour shows inline error
- Bible import: schema validation with clear error messages (see M2.2)

### 7.2 — Error banners in all views
Every feature view must show the `ErrorMessage` / `HasError` banner from `BaseViewModel`.

### 7.3 — First-run experience
- No songs → "Add your first song →" call-to-action instead of empty list
- No Bible version → "Import a Bible translation to get started" prompt
- No theme beyond Default → note in ThemesView

### 7.4 — About & keyboard shortcut reference
Accessible from a `?` button in the toolbar.

### 7.5 — Publish ✅ DONE (2026-06-01)

- **Publish profile** `OpenAdoration.WPF/Properties/PublishProfiles/win-x64.pubxml`:
  self-contained, single-file, win-x64, ReadyToRun, compressed, native libs self-extracted.
  `dotnet publish OpenAdoration.WPF -c Release -p:PublishProfile=win-x64` →
  one `OpenAdoration.exe` (~82 MB) that runs on Windows 10+ with no .NET prerequisite.
- **Installer** authored with **WiX v5** (`installer/OpenAdoration.wxs`): per-machine MSI,
  Program Files install, Start Menu + Desktop shortcuts, Add/Remove Programs metadata,
  `MajorUpgrade` for in-place upgrades. Fixed `UpgradeCode` 94340D83-…-8D5C.
- **One-command build**: `pwsh installer/build.ps1 [-Version x.y.z]` publishes then builds
  `installer/out/OpenAdoration-<version>-win-x64.msi` (~76 MB).
- WiX v5 chosen over v6/v7 (those require accepting the paid OSMF EULA).

---
---

# Version 2.0 — planning

> v1.0 is feature-complete and shipping for church testing. v2.0 adds four themes:
> **Reliability & Releases**, **Content & Imports**, **Presentation Richness**, and
> **Internationalization** (multi-language UI).
> Same rules apply: offline-first, operator-safe, nothing ships unless it's trustworthy live.
> Each milestone respects Clean Architecture — new behaviour enters as Application interfaces +
> Infrastructure implementations + WPF VMs/Views, never by crossing layer boundaries.

## Milestone 8 — Reliability & Releases

**Why:** Now that there's an installer, the next safety nets are *not losing data* and *staying current* — both without violating offline-first (no cloud, no telemetry; the only network call is an opt-in update check).

### 8.1 — Backup & Restore ✅ DONE (2026-06-17)
**Goal:** the operator exports everything to a single portable file and can restore it on any machine.

**Built:** `IBackupService`/`ZipBackupService` (online-backup DB snapshot + media + settings → `.oabak`; restore gates on the manifest migration being known to this app, stages the DB as `<db>.restore`, swapped in by `App.ApplyPendingRestore` on next launch). Settings → "Create Backup…"/"Restore Backup…" with confirm + restart prompt. Tests: `BackupArchiveTests` (gate + pack/unpack round-trip).


- **Application:** `IBackupService { Task CreateAsync(string path, CancellationToken ct); Task<RestoreResult> RestoreAsync(string path, CancellationToken ct); }`. Define `BackupManifest` (app version, created-at UTC, current EF migration id) and `RestoreResult` (compatible / needs-newer-app / corrupt).
- **Infrastructure:** `ZipBackupService` — bundles the SQLite DB (via SQLite Online Backup API or a connection-closed file copy), the media folder, and `settings.json` into one `.oabak` (zip) plus `manifest.json`. Restore validates the manifest migration id ≤ current before overwriting; never restore a backup from a newer schema.
- **WPF:** Settings → "Create Backup…" / "Restore Backup…" with file dialogs + a confirm dialog. Restore writes the files then prompts an app restart (DB swapped before `AppDbContext` is in heavy use).
- **Done when:** export on PC A, restore on a fresh PC B, all songs/Bibles/themes/services/media present.

### 8.2 — Auto-update (opt-in)
**Goal:** the app can tell the operator a newer version exists and install it in one click.

- **Application:** `IUpdateService { Task<UpdateInfo?> CheckAsync(CancellationToken ct); Task DownloadAndApplyAsync(UpdateInfo info, CancellationToken ct); }`. `UpdateInfo` = version, notes URL, MSI asset URL, size.
- **Infrastructure:** `GitHubUpdateService` — `HttpClient` GET `releases/latest`, SemVer-compare against the assembly version, download the `.msi` asset to temp, then launch `msiexec /i` and exit. Fails silently when offline.
- **WPF:** Settings → "Check for updates" + an opt-in "check on startup" toggle (default off). When found, a non-blocking banner: *"v2.1 available — Update"*.
- **Offline-first note:** this is the **only** outbound network feature; it is opt-in, update-only, and sends no data. Document the tension explicitly.
- **Done when:** a published GitHub release with a higher version is detected, downloaded, and the installer launches.

### 8.3 — Release infrastructure ✅ DONE
- `CHANGELOG.md` (Keep a Changelog + SemVer); update on every release.
- `docs/RELEASE.md` — the tag → MSI → GitHub release flow that 8.2 will consume.
- Single source of version truth (`Version` in the WPF csproj) flowed into the MSI and the update check.
- **CI/CD (2026-06-17):** GitHub Actions — `ci.yml` (build+test on push/PR), `release.yml` (tag `vX.Y.Z` → build MSI via `installer/build.ps1` → publish release; guards csproj `<Version>` == tag), `codeql.yml` (C# scanning), grouped `dependabot.yml`. `master` is branch-protected (PR + `build-test`/`analyze` required).

**Milestone 8 done when:** an operator can back up and restore their whole library, and update the app from within it. **(8.1 ✅, 8.3 ✅; 8.2 auto-update still pending.)**

---

## Milestone 9 — Content & Imports

**Why:** churches arrive with existing libraries in other tools and slide decks. The less retyping, the faster adoption.

### 9.1 — More song importers
Extend `SongFormatDispatcher` with new parsers (same pattern as OpenSong/plain text):
- **ChordPro / SongPro** (`.cho`, `.crd`, `.pro`) — text-based, directive `{title}`/`{c:}`; strip chords to lyrics. *(Easy — do first.)*
- **EasyWorship** — EW7 stores songs in a bundled SQLite DB; read songs + slides. *(Medium.)*
- **ProPresenter** — best-effort text extraction from `.pro` bundles (RTF inside). *(Hard — best-effort, clearly labelled.)*
- Each new format → a fixture + a `SongParserTests` case; the dispatcher's file filter grows.

### 9.2 — Media: decks & folders
- **Image-folder import** — pick a folder, register every image as one ordered media set / schedule item. *(Easy — do first.)*
- **PDF → image slides** — render pages to images via a BCL-friendly renderer (e.g. `Docnet.Core`/PDFium), store as a media set. *(Medium.)*
- **PowerPoint (`.pptx`)** — render slides to images. *(Hard; needs a converter — scope as stretch, may require LibreOffice headless or a library. Defer if no clean dependency.)*

### 9.3 — Bible quick-reference jump ✅ DONE
- A reference box ("John 3:16", "Jn 3:16-18") that parses book/chapter/verse and jumps/projects instantly, alongside the existing browser. Book-name matching reuses `OsisBookCatalog` + localized names.
- **Built** (during the VP-parity batch): `BibleViewModel.ParseReference` + `BibleReferenceParser.TryParse`/`GetSuggestions` — reference mode parses and navigates to book/chapter + verse range, with keyword/phrase fallback when the input isn't a reference.

**Milestone 9 done when:** an operator imports songs from at least one other app, projects a folder of images, and jumps to any verse by typing a reference.

---

## Milestone 10 — Presentation Richness

**Why:** stronger visuals and livestream support without compromising the operator's live reliability.

### 10.1 — Transition library 🔶 PARTIAL
- Extend the single Fade into a small, named set: **Cut** (instant) ✅, **Fade** ✅ (done), **Slide/Push** ❌, **Zoom** ❌. WPF animations on the existing `ContentLayers`; pick per-theme or global; `0 ms`/Cut always available as the safe default.

### 10.2 — Lower-thirds / persistent overlays
- Beyond the announcement banner: named overlays (speaker, sermon title, scripture ref) that **persist across slide changes** until cleared, rendered by `ProjectionWindow` + Stage View. Managed from a small overlays panel. Builds on the existing `AnnouncementChanged` overlay plumbing.

### 10.3 — Dual-version scripture 🚫 DROPPED (2026-06-18)
- Was built (stacked secondary verses + secondary version picker) but **removed during the v2.0 QA pass**: the operator found the extra picker/toggles confusing and didn't want the feature. Service params, VM members, the picker UI, and the dual-version tests were deleted. Revisit only if a church actually requests it — likely as a proper two-zone theme layout, not a stacked body.

### 10.4 — Clean output for livestream *(stretch)*
- A second **clean** output (slide content only, no operator overlays) for OBS capture; optional **NDI** sender if a clean managed/native path exists. Start with a clean borderless output window before committing to NDI (native SDK).

### 10.5 — Media transport controls *(operator-requested)* ✅ DONE (v1.1.0)
**Problem (solved):** projected video used to auto-play with **no controls**. Shipped: restart / −10s / play-pause / +10s / progress / time on the operator bar, driven through the `IProjectionService` media sub-API → `ProjectionWindow` (FFME). Stage preview position kept in sync.

- **Application:** extend `IProjectionService` with media transport: `PlayMedia()`, `PauseMedia()`, `RestartMedia()`, `SeekMedia(TimeSpan delta)`, plus state (`IsMediaPlaying`, `MediaPosition`, `MediaDuration`) and a `MediaPositionChanged` event. No-ops unless the current slide is video.
- **Infrastructure/WPF:** `ProjectionWindow` already hosts the playing `MediaElement` (`LoadedBehavior=Manual`) — wire these to `Play()/Pause()/Position`. The Stage View preview `MediaElement` mirrors position via the existing `SyncVideo()` path.
- **UI:** a transport bar (⏮ ⏯ ⏩ + scrub slider + time) shown in the projection control bar **and** Stage View, visible only when the current slide is video. Add keyboard shortcuts (e.g. `K`/`,`/`.` or reuse Space carefully — Space is "next slide", so use a dedicated key for play/pause to avoid collisions).
- **Auto-advance interaction:** a paused/seeking video must not be yanked by the auto-advance timer; pause should pause the countdown too.

**Milestone 10 done when:** the operator can choose a transition, keep a speaker lower-third up across slides, show two scripture versions at once, feed a clean output to the livestream, and **pause / restart / scrub a projected video**.

---

## Milestone 11 — Internationalization (multi-language UI)

**Why:** the app ships English-only, but the primary congregations are Spanish-speaking (and others may follow). Operators should run the whole app in their own language. The Spanish **user guide** (`docs/GUIA-USUARIO.md`) already exists and is the terminology reference for the translation.

> **Status — in progress (2026-06-01):** the localization **foundation is built and verified**
> (resx en+es, `TranslationSource`, `{loc:Loc}` extension, `ILocalizationService`,
> `AppSettings.UiCulture`, startup culture, live language dropdown in Settings).
> Localized so far: the app chrome (nav, projection bar, Help/About menu), the About window,
> and the Settings page. **Remaining:** externalize strings in the Songs, Bible, Themes, Media,
> Service Schedule and Stage views, the dialogs, and ViewModel error/validation messages.
> Adding a language later = drop in a new `Strings.<code>.resx` + register it in `LocalizationService`.

### 11.1 — Localization infrastructure ✅ done
- Externalize **every** user-facing WPF string into `.resx` resource files: `Strings.resx` (English, neutral) + `Strings.es.resx` (Spanish). No hard-coded UI strings left in XAML or ViewModels.
- A shared lookup usable from both XAML and code — a generated `Strings` class plus a `{loc:Str Key}` markup extension (or `WPFLocalizeExtension`) so views and VMs read the same keys.
- **Application:** `ILocalizationService` exposing `CurrentCulture` + `AvailableCultures`; the WPF implementation sets `CultureInfo.CurrentUICulture` / `CurrentCulture`. Stays behind an interface — no culture-switching logic leaks into ViewModels beyond the service.

### 11.2 — Language setting + runtime switch ✅ done
- `AppSettings.UiCulture` (in `settings.json`); applied at startup. Default = OS culture when supported, else English.
- Settings page: a language dropdown (English / Español). Switching applies **live** via `TranslationSource` (no restart).

### 11.3 — Spanish translation (first locale) 🔶 in progress
- Done: navigation, projection bar, Help/About menu, the About window, and Settings labels.
- Remaining: Songs, Bible, Themes, Media, Service Schedule, Stage views; dialogs; empty-state CTAs; ViewModel validation/error messages.
- **Do not** translate template **tokens** (`[SongTitle]`…) or Bible book names (those come from the imported Bible data, already localized per version).
- Use `docs/GUIA-USUARIO.md` as the canonical Spanish terminology.

**Milestone 11 done when:** an operator can switch the entire UI to Spanish in Settings and run a full service without seeing any English text.

---

## Milestone 12 — VideoPsalm Migration ✅ DONE (2026-06-16)

**Why:** the launch church runs **VideoPsalm** and wants *all* their data moved into OpenAdoration — songs, scripture, media, and the full service structure — across **many `.vpagd` service files** (one per service type, growing over time). The goal is a faithful, lossless, centralized migration, not a one-off paste.

### Format facts (reverse-engineered 2026-06-15)
- **`.vpagd` = a ZIP** of relaxed-JSON files (unquoted keys + literal newlines — illegal standard JSON; parsed by `VpJsonReader`). Song import already shipped (`VideoPsalmParser`).
- **Agenda order = the ZIP central-directory order** (NOT alphabetical, NOT the per-type `_{n}` index). `AgendaItemProperties.json` is a parallel array (per-item `AutoAdvance`/`Interval`/`HiddenSlides`/`VerseOrderIndex`). Cross-validated: songs carry `HiddenSlides:[]`; the image was the lone `FlowType:2`.
- **Scripture in a `.vpagd` = reference + the full text of only the *used* verses** (never the whole Bible). Book `ID` is 0-indexed canonical (`+1` = OA `BookNumber`); chapter `ID` = chapter number; first verse omits its `ID` (=1).
- **Media bytes are embedded** (`Images/`, `Videos/`, `Users/.../`); `FileName` is a foreign absolute path — match by **basename** to the ZIP entry. iPhone HEVC `.MOV` plays via the FFME engine.
- **DRM blocker (do not circumvent):** VideoPsalm's *complete* Bibles live in `…\Public\Documents\VideoPsalm\Bibles\*.vpc` — each a ZIP holding one ~4.5 MB JSON = the whole Bible — but the entry is **AES-encrypted (ZIP method 99)**. *Every* module is encrypted uniformly, **even public-domain ASV** — it's structural DRM, not per-copyright. The full NVI therefore **cannot be lawfully extracted** from the `.vpc`; the agenda export is the only plaintext VideoPsalm produces. Cracking it is out of scope for a public OSS tool.

### 12.1 — Foundations (one EF migration)
- `Song.SourceGuid` (cross-file song identity/dedup — VideoPsalm `Guid`), `MediaFile.ContentHash` (cross-file media dedup), `WorshipService.SourceGuid` + `SourceArchivePath` (service identity + the retained original `.vpagd`).
- **Application:** `IBibleService.UpsertVersionVersesAsync(versionIdentity, books, verses, progress, ct)` — idempotent find-or-create version by abbreviation, ensure book rows, insert only missing verses (batched 1000 + `ChangeTracker.Clear()`). The single sink every scripture source feeds.
- Dedup lookups: song-by-`SourceGuid`, media-by-`ContentHash`, service-by-`SourceGuid`.

### 12.2 — Parsers
- `VideoPsalmAgendaParser` → ordered `VpAgenda` model (items in ZIP order + their `AgendaItemProperties`, media refs, styles).
- `VideoPsalmBibleDetector` → **DRM detector only.** Every VideoPsalm `.vpc` is AES-encrypted (method 99) uniformly — there is *no* unencrypted VideoPsalm Bible to parse — so OA never imports VP Bible text; it only detects the encryption (`IsDrmProtected`) so the "Import Bible…" UI can refuse with a clear message. Never decrypt. (Legal Bibles arrive via the existing OSIS/USFX/JSON/sqlite importers, not VideoPsalm.)
- Unit-tested against synthetic files (existing test infra builds `.vpagd` zips at runtime).

### 12.3 — Core import (content + structure)
Orchestrator builds a `WorshipService` in true order: songs (dedup by `SourceGuid`, else create), scripture (**reference only** — text resolves from installed versions at projection time; nothing harvested from the agenda), media (hash-dedup + byte extract → Media store, HEVC via FFME), order + `AutoAdvance`. Archive the original `.vpagd` (`SourceArchivePath`); skip/refresh if its `SourceGuid` is already imported. Summary dialog; single-file **"Import VideoPsalm service…"** button in `ServiceScheduleView`.

### 12.4 — Themes (faithful look)
Reconstruct OA `Theme`s from VP styles (background image/video + body font/size/color/alignment + header/footer templates), dedup by style signature, assign per item. *Exact white-fill/gold-stroke text needs new `Theme.FontStrokeColor`/`FontStrokeWidth` + projection rendering — optional sub-task; approximate (no stroke) otherwise.*

### 12.5 — Batch + centralized, enrichable Bible
- **Batch folder import** of many `.vpagd` with cross-file dedup + aggregate summary.
- **Centralized scripture, source-agnostic:** one `BibleVersion` per abbreviation (e.g. `NVI-S`), enriched by any **legal** source through `UpsertVersionVersesAsync` — a CC BY-SA / public-domain download, or OA's existing OSIS/USFX/JSON importers (never the agenda, never the DRM'd `.vpc`). Because items store *references* resolved at projection time, importing the version later completes already-imported services with **zero rework**.
- **"Import Bible…" entry point** that accepts VideoPsalm Bible files (with AES detection) — the drop-in hook for adding a legally-obtained full version whenever the church has one.

### 12.6 — Bibles are the church's responsibility; OA just informs
OA is an open-source **tool**, not a Bible licensee. It ships no copyrighted text, embeds no API keys, and acquires no licenses on anyone's behalf. Our only obligation is to tell the operator clearly where text can and can't come from. We do **not** scrape verse text out of `.vpagd` agendas and we do **not** crack the `.vpc` DRM.

- **From a `.vpagd` we import only what's legal — Bible verse text is omitted.** Songs, media, schedule order, themes come in; scripture items come in as **references only** (book/chapter/verse). Verse text renders from whatever Bible version the church has legally imported into OA; a reference with no matching installed version shows the reference plus a "version not installed" note. The import summary **tells the operator plainly** that Bible text was omitted because it's licensed/protected and must come from a version they install.
- **`.vpc` Bibles are DRM-protected (AES, ZIP method 99) — never imported, never cracked.** "Import Bible…" detects the encryption and shows a plain message: *this VideoPsalm Bible is encrypted and can't be imported; obtain the version from a legal source.*
- **Legal sourcing is the church's call.** OA imports what they legally hold. Two paths OA actively supports: **public-domain** (Reina-Valera 1909, Sagradas Escrituras 1569 — imported today) and **CC BY-SA modern Spanish** (e.g. Nueva Biblia Viva from Open.Bible — attribution to Biblica required; kept as separate CC-licensed data, not mingled into the MIT code). Licensed versions (NVI, etc.) are the church's to obtain (direct Biblica permission or their own api.bible account) — **not OA's job**; OA just states this clearly.
- **api.bible connector is NOT core — it ships as a plugin (see M13).** A church that wants a licensed version installs the bring-your-own-key api.bible plugin from OA's GitHub; it feeds `UpsertVersionVersesAsync` under *their own* account/terms. Keeping it out of core means the DRM/telemetry/redistribution concerns never touch the MIT codebase. **YouVersion Platform and any OA-as-licensee model are ruled out** (no shared keys, no redistribution, mandated telemetry/DRM).

**OA features implied:** per-version **copyright/attribution** footer token; CC BY-SA Bible data packaged separately from MIT code; clear "version not installed / format is protected" notices. *(Tracked: adding AI/ML features anywhere forfeits Biblica Express Licensing eligibility — only relevant if a church goes the api.bible route.)*

**Milestone 12 done when:** the operator imports a folder of `.vpagd` files and gets faithful services (correct order, songs/scripture/media, themed), shared content is deduped across files, the central scripture version is enrichable from a legal full Bible later with no rework, and copyrighted versions can display their required attribution.

---

## Milestone 13 — Plugins (extensible add-ons via GitHub)

**Why:** core OA stays MIT and ships **zero** third-party-licensed connectors. Optional capabilities that carry licensing, telemetry, or DRM strings (first one: the **api.bible Bible connector**) ship as **separate plugins** released on OA's GitHub. A church downloads a plugin and adds it inside OA — so those concerns never touch the core codebase and never burden installs that don't opt in.

### 13.1 — Plugin contract ✅ DONE (2026-06-17)
- `IPlugin` (id, name, version, lifecycle) + capability interfaces. First capability: `IBibleSourcePlugin` — fetches `(version, books, verses)` and feeds the existing `IBibleService.UpsertVersionVersesAsync` sink. No plugin gets DB or filesystem access beyond the capability surface it's handed.
- Contract lives in a small `OpenAdoration.Plugins.Abstractions` package so a plugin repo references *only* that, not the whole app.
- **Built:** `OpenAdoration.Plugins.Abstractions` (net10.0, only deps `Microsoft.Extensions.Logging.Abstractions`): `IPlugin`, `IPluginHost` (settings + logger, nothing else), `IBibleSourcePlugin`, plugin-side DTOs (`PluginBibleData`/`PluginBibleBook`/`PluginBibleVerse`/`PluginBibleVersionInfo`/`PluginTestament`) so plugins never reference Domain, `PluginCapabilities`. Plus `OpenAdoration.Plugins.Sample` (`EchoBibleSourcePlugin`) — the loader fixture for 13.2. Test: `PluginContractTests`. Mapping DTO→Domain stays in WPF (`PluginBibleImporter`, 13.2).

### 13.2 — Discovery & loading ✅ DONE (2026-06-17)
- A plugin = `.oaplugin` (a ZIP of `manifest.json` + the assembly + its deps). Manifest: `id`, `name`, `version`, `capability`, `minOaVersion`, `entryAssembly`, optional `settings`.
- Installed to `%LOCALAPPDATA%\OpenAdoration\plugins\<id>\`; discovered + loaded at startup in a **collectible `PluginLoadContext`** (shares Abstractions from the default context so `IPlugin` types match across the boundary).
- Version gate: skip + warn if `minOaVersion` exceeds the running app.
- **Built (WPF/Plugins):** `PluginManifest`, `PluginLoadContext`, `PluginHost`, `LoadedPlugin`, `PluginManager` (`LoadAll`/`LoadFrom`, gate, per-plugin plaintext `settings.json`), `PluginBibleImporter` (maps plugin DTOs → Domain → `UpsertVersionVersesAsync`). Assemblies load via `LoadFromStream` (no on-disk lock, so a plugin can be removed). Registered in `App` DI; `LoadAll()` at startup. Tests: `PluginManagerTests` (load/gate/no-manifest) + `PluginBibleImporterTests` (mapping). **Remove = delete dir + restart (no live unload).**

### 13.3 — Settings UX ✅ DONE (2026-06-17)
- Settings → **Plugins**: list installed (name/version/capability), **Add plugin…** (pick a `.oaplugin`), remove. Link out to the GitHub plugin catalog.
- Plugin-provided settings (e.g. the api.bible key field) render from the manifest's `settings`.
- **Built:** `🧩 Plugins` nav page (`PluginsViewModel` + `PluginsView`, scope-per-nav); `PluginManager.Install` (guarded extract + live load), `Remove` (delete + restart to unload), `GetSettings`/`UpdateSettings` (re-inits the plugin so a new key is live). For a Bible-source plugin: **Fetch versions** → list → **Import** via `PluginBibleImporter`. Settings root is injectable for tests. `+2` tests (install/remove round-trip, settings persist). 55/55.
- **Deferred (ponytail):** enable/disable toggle — `remove` covers "stop using it"; add a disabled-marker + restart only if a church wants to keep a plugin installed-but-inactive. Secret fields render as plain text (keys are stored plaintext in v1 anyway).

### 13.4 — First plugin: api.bible connector (separate repo)
- Bring-your-own-key: the church pastes *its own* api.bible key, picks versions, syncs into the local DB via `IBibleSourcePlugin` → `UpsertVersionVersesAsync` (≥30-day refresh). The church is the licensee and accepts api.bible's terms; OA core ships no key and no copyrighted text.
- Built and released in its own repo so the licensed-source code never lands in the MIT core.

**Security ceiling:** plugins run **in-process at full trust** — only install OA-published plugins (the trusted default catalog). *(ponytail: no sandboxing; revisit only if untrusted third parties start publishing plugins.)*

**Milestone 13 done when:** a church downloads the api.bible plugin from OA's GitHub, adds it via Settings → Plugins, pastes its own key, and syncs a Bible version into the local DB — with zero api.bible (or any licensed-source) code in the core repo.

---

## Milestone 14 — Content-level theming (theme cascade)

**Why:** OA today attaches a theme only at the **schedule-item** level (`ScheduleItem.ThemeId`); a song or Bible passage projected **standalone** always uses the single app-wide default. VideoPsalm instead themes by **content**: every song, songbook, and Bible version carries its own style, resolved through a most-specific-wins cascade (Base → all-songbooks → one-songbook → one-song, plus per-content-type defaults like `RootStyle`/`BibleStyle`). M12.4 has to flatten that into a throwaway per-item theme. To migrate faithfully **and** make themes useful outside a service, OA needs the theme to live on the content, not just the agenda line.

### 14.1 — Content-level theme assignment
- Add nullable `ThemeId` where styling belongs to the content: `Song.ThemeId`; a per-**content-type** default theme (Songs / Scripture / Media); keep the existing app-wide default as the root. *(Bible-version-level theme can come later if a church needs different looks per version — YAGNI until asked.)*
- One EF migration; all `ThemeId`s nullable so existing data is untouched (null = inherit from the level above).

### 14.2 — Resolution cascade ✅
- Single resolver `ThemeCascade` (Application/Common), most-specific wins: `ScheduleItem.ThemeId → content's own ThemeId (Song.ThemeId) → content-type default → null`. The final `null` rung means "no explicit theme", which `ProjectionWindow.ResolveThemeAsync` already maps to the app-wide default — so app default is not duplicated in the resolver.
- Applied at **every** slide-generation call site: service items (`ServiceScheduleViewModel`, incl. UP-NEXT previews + live song-edit refresh) **and** standalone `SongsViewModel` / `BibleViewModel` / `MediaViewModel`. `SongsViewModel` + `MediaViewModel` gained `IAppSettingsService`. Unit-tested (`ThemeCascadeTests`, +3 → 67 tests).

### 14.3 — UI ✅
- Theme picker in the song editor (`AddEditSongView`): "Use default theme" sentinel + all themes; persists `Song.ThemeId` (also fixed `SongRepository.UpdateAsync`, which wasn't copying `ThemeId`).
- "Content themes" section in Settings → General: Songs / Scripture / Media default-theme pickers ("App default" sentinel) → `AppSettings.Default{Song,Scripture,Media}ThemeId`. Save fires `NotifyThemeChanged()` so a changed default refreshes a live projection. App-wide default stays the existing Themes-page "Set default".
- Shared `ThemeOption` record drives both pickers (null Id = inherit sentinel). New en/es resx keys (`SongEdit_Theme*`, `Settings_*Theme*`). Build 0/0, tests 67/67.

### 14.4 — Fold M12.4 into the cascade ✅
- VideoPsalm import now targets the right cascade level instead of minting a per-schedule-item theme: a song's style → its own `Song.ThemeId` (new songs only; reused songs keep theirs); `BibleStyle` → `DefaultScriptureThemeId`; `RootStyle` → the app-default theme. Schedule items are left theme-null and inherit. Collapses theme proliferation and matches VP's model.
- **Guarded ("set defaults only if unset", agreed 2026-06-19):** Scripture default applied only when `DefaultScriptureThemeId is null`; RootStyle→app-default applied only when the current default theme was never hand-edited (`UpdatedAt == CreatedAt`) — so an import never clobbers an operator's deliberate theme choices. Parser now surfaces `VpAgenda.RootStyle` (was internal-only); `VideoPsalmServiceImporter` gained `IAppSettingsService`. +1 parser test (68 total).

### 14.5 — Icon system: emoji → Fluent font, decoupled from resx
- Replace button emoji/Unicode glyphs (`▶▶ Project`, `■ Stop`, `🔒 Freeze`, `◀ Back`, `📢 Announce`…) with the **Segoe Fluent Icons / MDL2** font (`FontFamily="Segoe Fluent Icons" Content="&#xE768;"`) — not hand-drawn `Path` geometries (far less code, same crispness). Emoji render inconsistently across DPI / Windows version / font stack (ui_ux review Rec 2).
- **Decouple icon from text:** the M11.3 i18n pass fused glyph + label into ~25 resx values in *both* en and es (`Nav_*`, `Projection_*`, `Bible_Freeze/Project`, `Sched_Stop/Back/...`). Move the glyph into XAML; keep only the label in resx, so an icon change no longer edits localized strings. Leave *status* glyphs (`● LIVE`, `⚠`, `✓ Saved`) as inline text. **Guard meanwhile:** don't add new icon-in-resx strings.

**Milestone 14 done when:** a song carries its own theme that shows whether projected standalone or in a service; per-content-type defaults exist; the projection theme is chosen by one cascade everywhere; and VideoPsalm import assigns themes at content level rather than per schedule item.

---

## Backlog (deferred — not blocking v2.0)

Pulled out of the active plan 2026-06-18; revisit when the blocker clears or a church asks.

| Item | Why parked |
|---|---|
| M9.1 EasyWorship import | Needs a **real EW7 export** to validate the SQLite schema — can't build blind. |
| M9.1 ProPresenter import | Needs a real `.pro`/bundle sample. |
| M9.2 PDF / pptx deck import | Native dependency decision (Docnet/PDFium for PDF; pptx unzip) — heavyweight, defer. |
| M10.4 Clean output / NDI *(stretch)* | Clean borderless output is doable later; NDI needs a native SDK. |
| ChordPro in import tooltip/format string | 2-line cosmetic copy fix (both langs) — fold into the next i18n touch. |
| **NU1903** — `SQLitePCLRaw.lib.e_sqlite3 2.1.11` high-severity advisory (transitive via `EFCore.Sqlite 10.0.9`) | Fix is a **major** SQLitePCLRaw 2.x→3.x bump that changes native SQLite loading (single-file publish risk). Needs a dedicated bump + GUI/publish verification — let dependabot propose it on `dev` and verify, don't blind-bump. Surfaced 2026-06-18. |

---

## Out of scope (and why)

| Feature | Status / reason |
|---|---|
| Media-library video playback | ✅ **Now shipped** (M4) — images and video both project. |
| Remote control (phone/tablet) | Considered for v2.0, **deferred** by product decision. Revisit in a later version; needs a local LAN HTTP server. |
| Multi-user / network sync | Out — one PC per service is universal in small churches. |
| **Cloud** backup / sync | Out — violates offline-first. (Note: **local** backup/restore is in scope as M8.1 — a portable file, no cloud.) |
| CCLI licence tracking / reporting | Out for now — useful but not blocking. |
| Drag-to-reorder in schedule | Out — Up/Down buttons are sufficient. |

---

## Build sequence

```
── v1.0 (shipped 2026-06-01) ───────────────────────────────────────────────
Milestone 0     Milestone 1     Milestone 2     Milestone 3
Songs      →    Themes+Bible →  Bible import →  Schedule  →
                                hardening       (builder+live)

Milestone 4     Milestone 5     Milestone 6     Milestone 7
Media      →    Shortcuts   →   Preview     →   Polish + ship
                                (Stage View)    (installer)

── v2.0 (planning) ─────────────────────────────────────────────────────────
Milestone 8         Milestone 9          Milestone 10         Milestone 11
Reliability    →    Content &       →    Presentation    →    i18n
& Releases          Imports              Richness             (multi-language)
(backup/restore,    (song formats,       (transitions, overlays,  (resx, language
 auto-update)        decks, ref-jump)     dual scripture, video)    setting, Spanish)

Milestone 12         Milestone 13         Milestone 14
VideoPsalm      →    Plugins         →    Content-level
Migration            (GitHub add-ons,      theming
(legal-only import,   api.bible connector  (theme cascade;
 references only)     as 1st plugin)        folds in M12.4)
```

Each milestone leaves the app in a better, shippable state than before it. No milestone introduces new features on top of unverified ones.

---

## Summary

### v1.0 — shipped
| Milestone | Goal | Status |
|---|---|---|
| **0 — Songs stable** | Verify songs end-to-end; fix B3 | ✅ |
| **1 — Themes + Bible stable** | Theme projection; Bible browse/search/project | ✅ |
| **2 — Bible hardening** | Progress UI, cancellation, errors, parser tests | ✅ |
| **3 — Schedule** | Builder; live nav with ThemeId overrides | ✅ |
| **4 — Media** | Import + project (images and video) | ✅ |
| **5 — Shortcuts** | Keyboard navigation for live use | ✅ |
| **6 — Preview** | Operator preview (delivered as Stage View) | ✅ |
| **7 — Polish & Release** | Validation, error states, first-run UX, installer | ✅ |

### v2.0 — planning
| Milestone | Goal | Effort |
|---|---|---|
| **8 — Reliability & Releases** | Backup/restore, opt-in auto-update, release infra | Medium |
| **9 — Content & Imports** | More song formats, image/PDF decks, Bible ref-jump | Large |
| **10 — Presentation Richness** | Transition library, overlays, dual scripture, clean output, video transport controls | Large |
| **11 — Internationalization** | Multi-language UI (.resx infra, language setting, Spanish translation) | Medium |
| **12 — VideoPsalm Migration** | Full-agenda import (songs/scripture/media/schedule/themes), references-only scripture (verse text omitted as licensed), centralized enrichable Bible, batch + dedup | Large |
| **13 — Plugins** | GitHub-distributed add-ons (`IPlugin` + `.oaplugin` loader, Settings UX); api.bible bring-your-own-key Bible connector as the first plugin | Medium |
| **14 — Content-level theming** | Theme on content (Song + per-type defaults) resolved by one cascade everywhere; standalone projection themed; folds in M12.4's per-item themes | Medium |
