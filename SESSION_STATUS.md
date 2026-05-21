# Session Status — OpenAdoration WPF

_Last updated: 2026-05-21_

---

## Current build state

```
Build:  SUCCEEDED  (dotnet build OpenAdoration.sln --configuration Debug)
Errors:   0
Warnings: 0
Tests:    8/8 pass  (dotnet test OpenAdoration.Tests.Infrastructure)
Last commit: (pending — Bible UI redesign)
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
| Bible book name localization (OsisXmlParser priority fix + BSS JSON book_name field) | DONE |
| M3 — Service Schedule (service list + builder + live mode) | DONE |
| Bible UI Redesign — Live-service UX overhaul | DONE |

---

## What was done in the Bible UI redesign

### BibleViewModel.cs — full rewrite
- Replaced `DisplayedVerses` / `SelectedVerse` with `ObservableCollection<BibleVerseCheckItem> CheckableVerses`
- Added reference bar: `ReferenceInput`, `BookSuggestions`, `HasBookSuggestions`, `ReferenceBarPlaceholder`
- Added slide preview: `SlidePreviewText`, `SlidePreviewLabel`, `HasSlidePreview`
- Added mode flags: `IsFrozen` (property-changed handler projects on unfreeze), `IsKeywordMode`
- New commands: `ParseReferenceCommand`, `SelectVerseCommand`, `SelectBookSuggestionCommand`, `ClearReferenceCommand`, `ProjectSelectedCommand`, `ExpandSelectionDown/UpCommand`, `AddSelectedToScheduleCommand`
- `PreviousVerse` / `NextVerse` now do exclusive-select navigation (first/last checked verse -/+1)
- `OnSelectedVersionChanged` saves restore point; reloads books and reapplies selection
- `LoadVersesAsync` applies `_restoreVerseNums` after loading if a restore target was set
- `RunSearchAsync` (private, triggered by `ParseReferenceCommand` in keyword mode) uses `ReferenceInput` as query
- Verse item subscription/unsubscription enforced on every collection rebuild (no leaks)
- `_updatingSelection` flag prevents recursive projection during bulk check/uncheck

### BibleView.xaml — full redesign
- **Reference bar** at top: TextBox (binds `ReferenceInput`, Enter=ParseReference, Esc=Clear), autocomplete Popup (binds `BookSuggestions`), Version ComboBox (moved here from old toolbar), Keyword ToggleButton, Clear Button
- **Import progress / summary / error** bars unchanged (moved below reference bar)
- **Slide preview panel** (DockPanel.Dock=Bottom above action bar): shows `SlidePreviewText` + `SlidePreviewLabel`
- **Action bar** (DockPanel.Dock=Bottom): Prev/Next, Freeze ToggleButton, + Schedule, ▶▶ Project; Import/Delete moved to right side
- **Verse list**: `ItemsControl` with `BibleVerseCheckItem` rows — each row has checkbox + verse number + text; row click = `SelectVerseCommand` (exclusive select); checkbox `IsHitTestVisible=False`
- `VerseCheckBox` dark-theme style added to local resources
- `UserControl.InputBindings`: Ctrl+↓ = ExpandDown, Ctrl+↑ = ExpandUp

---

## Next — Milestone 4: Media

Key work:
1. `MediaViewModel` — LoadCommand, ImportFileCommand (copy-on-import to `%LocalAppData%\OpenAdoration\Media\`), DeleteFileCommand, ProjectFileCommand
2. `MediaView.xaml` — WrapPanel of image/video cards (thumbnail 160×90, filename, Project/Delete), Import toolbar button
3. No new migrations — MediaFiles table already exists

---

## Remaining planned milestones

| # | Feature | Notes |
|---|---|---|
| M4 | Media | MediaViewModel + MediaView; no migration needed |
| M5 | Keyboard Shortcuts | KeyDown in MainWindow.xaml.cs |
| M6 | Stage view | Operator monitor: current + next slide preview |
| M7 | Announcement slide + Clock/Countdown | New SlideTypes |
| M8 | Font drop shadow/outline + slide transitions | Theme property + ProjectionWindow Storyboard |
| M9 | Song lyrics FTS + CCLI field | Migration + UI |
| M10 | Loop mode + configurable footer | ProjectionService flag + Theme property |
| M11 | Bilingual display | Two translations on one slide |
| M12 | Polish & Release | |
