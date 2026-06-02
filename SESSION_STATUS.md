# Session Status — OpenAdoration WPF

_Last updated: 2026-06-01_

---

## Current build state

```
Build:    SUCCEEDED  (dotnet build OpenAdoration.sln --configuration Debug)
Errors:   0
Warnings: 0
Tests:    16/16 pass  (dotnet test OpenAdoration.Tests.Infrastructure)
Release:  v1.0.0 SHIPPED 2026-06-01 — self-contained single-file exe + WiX v5 MSI
Current migration: 20260529210146_AddSongScheduleItemVerseOrderOverride (no new migration since)
Branch: master
```

**Since 2026-05-29 (v1.0 finishing + ship):**
- Song import expanded: OpenSong XML + plain text via `SongFormatDispatcher` (+ `SongSectionTokens`); 6 new parser tests (16/16 total).
- M7.1 polish: 200-char max on song title input.
- M7.5 packaging: `win-x64.pubxml` single-file publish + WiX v5 MSI (`installer/`); release infra (`CHANGELOG.md`, `docs/RELEASE.md`).
- **All roadmap milestones M0–M7 complete.** `ROADMAP.md` is canonical (its old top table was corrected).

**Next: v2.0 planning** — `ROADMAP.md` Milestones 8–10: Reliability & Releases (backup/restore, auto-update), Content & Imports (more song/media formats, Bible ref-jump), Presentation Richness (transitions, overlays, dual scripture, **video transport controls**).

---

## Completed milestones

| Milestone | Status |
|---|---|
| M0 — Songs (CRUD + projection + section validation) | DONE |
| M0b — Projection UX (hidden window, corner label, Open/Close toggle) | DONE |
| M1 — Themes (full CRUD + BackgroundType + ColorPicker + projection applies theme) | DONE |
| M2 — Bible Browser (3-column, click-to-project, FTS search, 8-format import, 8/8 tests) | DONE |
| M3 — Service Schedule (service list + builder + live mode) | DONE |
| M4 — Media (import, project, delete; image + video with audio) | DONE |
| M5 — Keyboard Shortcuts (Space/→/PageDown=Next, ←/PageUp=Prev, B=Blank, Esc=Stop, 1–9=GoTo, Ctrl+1–5=nav tabs) | DONE |
| M6a — App icon + About screen (Help menu) | DONE |
| M6b — Unhandled exception handler | DONE |
| QA/Security rounds 1+2 — XML hardening, ZIP bomb guards, path traversal, ID validation | DONE |
| **VP-parity batch (2026-05-29):** | |
| Token system — ITokenResolver; 10 tokens; 3-zone projection layout (Header/Body/Footer); auto-hide zones | DONE |
| Song VerseOrder — section reordering token string; FTS lyrics search with prefix matching | DONE |
| Song Copyright + CcliNumber — fields on Song entity; [SongCopyright][SongCCLI] tokens; OpenLyrics parse | DONE |
| Bible tokens — [BibleReference] ("John 3:16"), [BibleChapterID], [BibleVerseID], [BibleBookName], [BibleDescription] | DONE |
| Theme editor — HeaderTemplate/FooterTemplate text fields; token chip row; auto-hide info banner | DONE |
| Stage View — embedded nav section (not window); 1920×1080 Viewbox themed preview; UP NEXT cross-item; Prev/Next Item buttons (live only); real video via MediaElement | DONE |
| Auto-advance per schedule item — DispatcherTimer, +/- UI in builder, DB persist, cross-item advance at end | DONE |
| Verse order override per agenda item — `VerseOrderOverride` on `SongScheduleItem`; builder TextBox (LostFocus persist); passed to `GenerateSlides` | DONE |
| Settings page + church tokens — `settings.json` (ChurchName/ChurchCcliNumber/DefaultAutoAdvanceSeconds); `[ChurchName]`/`[SiteLicense]` tokens; `IAppSettingsService`; ⚙ Settings nav | DONE |
| **P2 batch (2026-05-29):** | |
| Live announcement banner — `ShowAnnouncement`/`ClearAnnouncement` + `CurrentAnnouncement`/`AnnouncementChanged`; blue lower-third overlay (white text) over the untouched slide; auto-dismiss after `AnnouncementDurationSeconds` (default 25); projection-bar input + manual Clear | DONE |
| Configurable verses-per-slide (Bible) — `DefaultBibleVersesPerSlide` setting; `BibleService.GenerateSlides` chunks; applies to schedule + multi-verse selection | DONE |
| Song Play Order editor + token badges — VerseOrder field in song editor (fixed edit data-loss bug); color-coded V1/C/B token badges per section | DONE |
| **Review fixes (2026-05-29):** | |
| #1 ARCHITECTURE.md stale Blank diagram corrected (black-screen behavior) | DONE |
| #2 DispatcherUnhandledException — explicit recoverable allowlist (IO/UnauthorizedAccess/OperationCanceled); unknown → terminate | DONE |
| #3 Media import — magic-byte signature validation before copy (MediaSignatureValidator) + skipped-count message | DONE |
| #4 ZIP ratio guard — overflow-safe IsCompressionRatioSuspicious (division, MaxCompressionRatio=50) + 2 guard tests | DONE |

---

## What exists now (full feature map)

### Songs
- Full CRUD with section editor (Verse / Chorus / Bridge / Pre-Chorus / Intro / Outro / Tag)
- Two-step search: title/author LIKE first; lyrics FTS5 fallback with prefix matching per word
- VerseOrder — token string (e.g. "V1 C V2 C"); controls slide order on projection
- Copyright and CcliNumber fields; [SongCopyright] and [SongCCLI] tokens
- Project: all sections as slides; Space/arrows navigate between sections
- OpenLyrics XML import (title, author, copyright, ccliNo, verseOrder, sections)

### Themes
- Full CRUD with live preview
- Background: solid color | image | video (looping, muted)
- Font family, size, text alignment (L/C/R)
- **3-zone layout**: Header (Auto) / Body (*Viewbox) / Footer (Auto)
- **HeaderTemplate / FooterTemplate**: free text + token chips; token list: [SongTitle][SongAuthor][SongVerseTag][SongCopyright][SongCCLI][BibleBookName][BibleChapterID][BibleVerseID][BibleReference][BibleDescription]
- Zone auto-hide: zone collapses if resolved text has no letter or digit (G20)

### Bible Browser
- 3-column layout: book tree (OT/NT grouped) → chapter tiles → verse list
- Multi-verse selection; Ctrl+↓/↑ extends/shrinks selection
- Reference bar + FTS keyword search
- [BibleReference] token formats as "John 3:16" or "John 3:16-18"
- 8-format import; localized book names preserved

### Media
- Import with extension + size validation (≤1 GB)
- Image and video projection; theme background video always muted

### Service Schedule
- Service list + builder (song/bible/media, reorder ▲▼)
- **Auto-advance per item**: [−] [⏱ Manual/Ns] [+] controls in builder; persists to DB; DispatcherTimer resets on every slide change (one-shot pattern)
- Live mode: clickable item list, Prev/Next Item, add to queue on the fly

### Projection Engine
- Singleton ProjectionService; full event bus
- Events: SlideChanged, ProjectionStateChanged, ThemeChanged, NextScheduleItemRequested, PreviousScheduleItemRequested, ServiceScheduleActiveChanged, NextScheduleItemPreviewChanged
- Properties: IsServiceScheduleActive, NextScheduleItemPreviewSlide
- Methods: SetServiceScheduleActive(bool), SetNextScheduleItemPreview(Slide?), RequestNext/PreviousScheduleItem()
- ITokenResolver (singleton) resolves [Token] patterns in header/footer templates against SlideContext

### Stage View
- Embedded nav section (sidebar "📺 Stage View" button → NavigateTo<StageViewModel>())
- Left panel (2/3): current slide — full 1920×1080 Viewbox with all theme layers (BgColor, BgImage, BgVideo placeholder, 3-zone text, MediaImage, MediaElement video, BlankOverlay)
- Right panel (1/3): UP NEXT — same Viewbox stack; shows first slide of next schedule item when on last slide of current item
- Status bar: LIVE/STOPPED badge, ContextLabel, SlidePosition
- Prev/Next Item buttons: visible only when IsServiceScheduleActive (not during standalone song/bible projection)
- Real video via MediaElement (muted, loops in code-behind SyncVideo())

---

## Remaining work (P1 priority)

| # | Feature | Notes |
|---|---|---|
| P2 | Additional song import formats | OpenSong, plain text |
| M6c | Packaging | MSIX or Setup; users shouldn't run via `dotnet run` — **deferred to last, after church testing** |

**All P0 + P1 done. P2 mostly done** (announcement, verses-per-slide, slide transitions, Bible phrase search). Clock/countdown overlay was reviewed and **dropped (not needed)**. Remaining: additional song importers (P2), then M6c packaging (last). See `VIDEOPSALM_REFERENCE.md` §2 for the full reconciled gap matrix.

### P2 batch 2 (2026-05-29) — DONE this session
- **Slide transitions** — configurable opacity Fade on projection output. `ProjectionWindow` wraps foreground layers in `ContentLayers`; `PlayTransition()` animates opacity 0→1 each render (theme bg stays static, no flicker). `SlideTransitionMilliseconds` setting (0=off, default 300). Stage preview is not faded.
- **Bible phrase search** — `BibleSearchMode { Keyword, Phrase }`. Keyword = all words, per-word prefix, implicit AND (new default); Phrase = exact FTS5 quoted phrase (the prior behavior). Threaded through `IBibleService`/`IBibleRepository.SearchAsync`; `BuildFtsTerm` builds the MATCH expr. Bible browser has a `" " Phrase` toggle (keyword mode only); toggling re-runs the search.

### P1-2 (settings page + church tokens) — DONE this session
- `AppSettings` (Application/Common): ChurchName, ChurchCcliNumber, DefaultAutoAdvanceSeconds.
- `IAppSettingsService` → `AppSettingsService` (Infrastructure/Settings): singleton, loads `settings.json` once at construction (defaults on missing/corrupt), `SaveAsync` rewrites + updates `Current`.
- `AddInfrastructure(services, dbPath, settingsPath)` — **settingsPath is now a required 2nd arg**; `App.xaml.cs` passes `appDataDir\settings.json`.
- `TokenResolver` ctor takes `IAppSettingsService`; `[ChurchName]` → ChurchName, `[SiteLicense]` → ChurchCcliNumber (app-wide, not per-slide).
- `SettingsViewModel` (transient) + `SettingsView` + "⚙ Settings" nav button + `NavigateToSettingsCommand` + DataTemplate. Save calls `IProjectionService.NotifyThemeChanged()` so active header/footer re-resolve.
- `DefaultAutoAdvanceSeconds`: `Add{Song,Bible,Media}ItemAsync` gained `int? autoAdvanceSeconds`; `ServiceScheduleViewModel` passes the default (null when 0) on each ConfirmAdd. No migration (JSON-only).

### P1-1 (verse order override) — DONE this session
- `SongScheduleItem.VerseOrderOverride (string?)` — per-service section order, same token syntax as `Song.VerseOrder`; null/empty falls back to the song's own order.
- `Song.GetOrderedSections(string? verseOrderOverride = null)` — override wins when set, else `VerseOrder`, else definition order.
- `ISongService.GenerateSlides(song, themeId, verseOrderOverride)` — third optional param threaded through.
- `IWorshipServiceService/Repository.SetItemVerseOrderOverrideAsync(itemId, verseOrder)` — repo casts to `SongScheduleItem`, trims/null-normalizes, saves.
- `ScheduleItemViewModel`: `VerseOrderOverride` observable + `IsSongItem` gate + `VerseOrderOverrideChangeRequested` event fired from `OnVerseOrderOverrideChanged` partial (persists on LostFocus, mirroring auto-advance).
- `ServiceScheduleViewModel`: subscribes/unsubscribes the event in Subscribe/UnsubscribeItemEvents; `OnVerseOrderOverrideChangeRequested` persists + syncs entity; both `GenerateSlides` call sites (live + UP NEXT preview) pass the override.
- View: builder row title cell is now a StackPanel with a verse-order `TextBox` (`UpdateSourceTrigger=LostFocus`), visible only when `IsSongItem`.
- Migration: `20260529210146_AddSongScheduleItemVerseOrderOverride` (adds nullable TEXT `VerseOrderOverride` to `ScheduleItems`).

---

## Key technical gotchas (active)

- **G1** `UseWindowsForms=true` — always fully-qualify: `System.Windows.Controls.UserControl`, `System.Windows.Controls.MediaElement`, `System.Windows.Input.KeyEventArgs`, `Microsoft.Win32.OpenFileDialog`, `System.Windows.Media.Color`, `System.Windows.MessageBox.Show()`
- **G4** IsBusy guard — `LoadAsync` opens with `if (IsBusy) return;`. Never set IsBusy before calling it.
- **G8** DataTemplate required in App.xaml for every navigated ViewModel — missing entry = blank content, no error.
- **G9** Unsubscribe all IProjectionService events in OnClosed()/Dispose() — ProjectionWindow, StageViewModel, ServiceScheduleViewModel all do this.
- **G10** FieldComboBox needs full ControlTemplate on ItemContainerStyle — do not simplify.
- **G11** Scope-per-navigation — always resolve page VMs via `NavigateTo<T>()`, never from root IServiceProvider.
- **G14** Testament enum — values are `Old` / `New`, NOT `OldTestament` / `NewTestament`.
- **G16** BibleViewModel.SelectedChapter — int, 0 = sentinel for "none selected".
- **G17** AlignToggleButton IsChecked must be `Mode=OneWay`.
- **G18** `Run.Text` on read-only properties must be `Mode=OneWay`.
- **G19** DispatcherTimer must be stopped in StopLive(), OnProjectionStateChanged(false), AND Dispose(). One-shot pattern: stop before advancing, SlideChanged restarts it.
- **G20** Token zone auto-hide: use `resolved.Any(char.IsLetterOrDigit)`, NOT whitespace trim. "  :" passes trim but has no useful content.
- **Live service nav** — `MainViewModel` stores `_liveServiceScope` + `_liveServiceVm`; restores by setting `CurrentView = _liveServiceVm` directly.
- **IntEqualityConverter** — MultiBinding on two ints for chapter tile highlight and media card selected state.
- **ThemeChanged pipeline** — `AddEditThemeViewModel.SaveAsync` → `IProjectionService.NotifyThemeChanged` → `ProjectionWindow.OnThemeChanged` (clears cache, calls `RefreshCurrentSlide`).
- **ContentVideo vs ThemeBackgroundVideo** — `ContentVideo` MediaElement is for media slide videos (plays with audio); `ThemeBackgroundVideo` is for theme background (always muted, loops via `MediaEnded`).
