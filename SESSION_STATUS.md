# Session Status — OpenAdoration WPF

_Last updated: 2026-05-20_

---

## Current build state

```
Build:  SUCCEEDED  (dotnet build OpenAdoration.sln --configuration Debug)
Errors:   0
Warnings: 0
Tests:    8/8 pass  (dotnet test OpenAdoration.Tests.Infrastructure)
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
| Bible book name localization | DONE — this session |
| M3 — Service Schedule (service list + builder + live mode) | DONE |

---

## Bible book name localization (done 2026-05-20)

**Problem**: Book names were always shown in English regardless of the imported Bible's language.

**Root cause per parser:**
- `OsisXmlParser.FinaliseBook()`: priority was backwards — preferred catalog English (`info.Name`) over the file's `<title>` element (`currentBookName`). One-line swap fixed it.
- `BibleSuperSearchJsonParser`: ignored the `book_name` field in verse data entirely. Now collects it into `Dictionary<int, string>` and uses it as primary for both `BibleVerse.Book` and `BibleBook.Name`.
- `ZefaniaXmlParser`: already correct — uses `bname` attribute directly (localized).
- `UsfxXmlParser`: already correct — prefers `<h>`/`<toc>` over catalog.
- `BibleSuperSearchZipParser`, `BibleSuperSearchSqliteParser`: integer book numbers only, no localized name in format — remain English from `OsisBookCatalog.GetByNumber`.

**Files changed:** `OsisXmlParser.cs` (1 line), `BibleSuperSearchJsonParser.cs` (+10 lines)

---

## M3 — Service Schedule (done 2026-05-20)

### Application layer
- `IWorshipServiceRepository` — added `GetWithItemsAsync`, `AddSongItemAsync`, `AddBibleItemAsync`, `AddMediaItemAsync`, `RemoveItemAsync`, `ReorderItemsAsync`
- `WorshipServiceRepository` — full implementations; `GetWithItemsAsync` fetches nav props per-subtype within same context
- `IWorshipServiceService` + `WorshipServiceService` — delegates with logging

### WPF layer
- `ScheduleItemViewModel` — per-item wrapper: `TypeIcon` (♪/✦/▣), `DisplayTitle`, `CanMoveUp/Down`, `IsCurrentLiveItem`; move/delete/select events
- `ServiceScheduleViewModel` — service list, builder, live mode; ScheduleItemViewModel event wiring; exits live if main bar Stop is pressed
- `ServiceScheduleView.xaml` — 3-panel layout (service list → builder → live); IsBusy overlay; Run.Text bindings use `Mode=OneWay` (G18)

### Add Bible panel
- Version ComboBox → async loads books; Book ComboBox → populated by BookNumber; Chapter ComboBox driven by `SelectedBook.ChapterCount`; VerseStart/VerseEnd TextBoxes

### Live mode flow
1. "▶ Start Service" → index=0, `LoadSlidesForCurrentItemAsync`
2. Song items → `ISongService.GenerateSlides` → `IProjectionService.LoadSlides`
3. Bible items → `IBibleService.GetVersesAsync` + range filter → `GenerateSlide` → `LoadSlides`
4. Media items → `IMediaService.GenerateSlide` → `LoadSlides`
5. Main bar Next/Prev navigates slides within item; "Next/Prev Item" buttons advance between items
6. Clicking a row in the left list jumps directly to that item

---

## Next: Milestone 4 — Media

Key work:
1. `MediaViewModel` — `LoadCommand`, `ImportFileCommand` (copy-on-import to `%LocalAppData%\OpenAdoration\Media\`), `DeleteFileCommand`, `ProjectFileCommand`
2. `MediaView.xaml` — WrapPanel of image cards (thumbnail 160×90, filename, Project/Delete buttons), Import button in toolbar
3. No new migrations needed — `MediaFiles` table already exists
4. `IMediaService.AddAsync` — copies file, stores copied path
