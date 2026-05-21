# OpenAdoration — Architecture & Developer Reference

> Last updated: 2026-05-19  
> Status: Active development — Songs, Bible, and Themes complete; Service Schedule and Media planned

---

## 1. Executive Summary

OpenAdoration is a **Windows desktop worship-presentation application** — a free, open-source alternative to EasyWorship. A single operator runs it during a church service to control what is shown on a projector screen.

Core loop:
1. Operator builds a **song library** and a **service schedule** before the meeting.
2. On the day, the operator opens the app, navigates through the schedule, and **sends slides to the projector** (a secondary monitor) in real time.
3. Slides are generated on the fly from the stored content — nothing is pre-rendered.

The app is **fully offline**. There are no network calls, no accounts, no cloud sync. Everything lives in a single SQLite file on the operator's machine.

---

## 2. Architecture

### 2.1 Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     OpenAdoration.WPF                       │
│                                                             │
│  App.xaml.cs  ──  DI composition root, startup/shutdown    │
│  MainWindow   ──  Shell: sidebar nav + projection bar       │
│  ProjectionWindow ─ Full-screen secondary-monitor output    │
│                                                             │
│  Views/           UserControls, one per feature            │
│  ViewModels/      ObservableObject + RelayCommand (MVVM)   │
│  Converters/      IValueConverter implementations          │
│  Helpers/         ScreenHelper (multi-monitor)             │
│  Styles/          Colors.xaml, Base.xaml                   │
└─────────────────────┬───────────────────────────────────────┘
                      │  interfaces only (no concrete refs)
┌─────────────────────▼───────────────────────────────────────┐
│                  OpenAdoration.Application                   │
│                                                             │
│  Services/     ISongService, IBibleService, IThemeService,  │
│                IMediaService, IWorshipServiceService,        │
│                IProjectionService  (+ implementations)      │
│                                                             │
│  Repositories/ ISongRepository, IBibleRepository, ...       │
│                (interfaces only — Infrastructure implements) │
│                                                             │
│  Common/       Slide, SlideType  (runtime DTO, never stored)│
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                 OpenAdoration.Infrastructure                  │
│                                                             │
│  Persistence/  AppDbContext, IDbContextFactory<AppDbContext> │
│  Configurations/ Fluent API entity configs (one per entity) │
│  Repositories/ Concrete EF Core implementations            │
│  Logging/      LoggingConfiguration (Serilog setup)         │
│  Extensions/   AddInfrastructure(), InitialiseDatabaseAsync()│
│  Migrations/   EF Core migration files                      │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                   OpenAdoration.Domain                       │
│                                                             │
│  Entities/     Song, SongSection, BibleVerse, Theme, ...    │
│  Enums/        SectionType, MediaType, Testament            │
│  Common/       BaseEntity (Id, CreatedAt, UpdatedAt)        │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Dependency Rule

```
WPF  →  Application  →  Domain
         ↑
Infrastructure  →  Application  →  Domain
```

- **WPF is the composition root** — it references both Application (for service interfaces) and Infrastructure (solely to call `AddInfrastructure()` in `App.xaml.cs`). This is the only permitted Infrastructure reference from WPF; all other WPF code must go through Application interfaces.
- Infrastructure references Application but never WPF.
- Domain references nothing — it is the innermost ring.
- All cross-layer business logic goes through the interfaces defined in Application.

### 2.3 Key Design Decisions

| Decision | Choice | Reason |
|---|---|---|
| UI framework | WPF (.NET 10) | Rich desktop controls, proven multi-monitor support |
| DB | SQLite via EF Core 9 | Zero-install, single file, offline-first |
| MVVM | CommunityToolkit.Mvvm 8 | Source-generated commands/properties, no boilerplate |
| Navigation | ContentControl + DataTemplates | No navigation framework needed; ViewModel-first |
| DbContext lifetime | `IDbContextFactory` (one ctx per operation) | Avoids long-lived tracking bugs, safe in async code |
| Projection | Singleton `ProjectionService` | Owns state machine for the app's lifetime |
| Logging | Serilog (rolling daily file + debug sink) | Structured, queryable, non-blocking |
| Multi-monitor | `System.Windows.Forms.Screen` | Only API that reliably enumerates physical screens in WPF |

---

## 3. Data Flows

### 3.1 Startup

```
App.OnStartup()
  │
  ├─ LoggingConfiguration.Configure(logDir)   ← Serilog configured first
  │
  ├─ Host.CreateDefaultBuilder()
  │    ├─ ConfigureLogging → UseOpenAdorationSerilog()
  │    └─ ConfigureServices → AddInfrastructure(dbPath)
  │                           RegisterViewModels()
  │                           RegisterWindows()
  │
  ├─ services.InitialiseDatabaseAsync()        ← MigrateAsync() — no-op if up to date
  │    └─ on failure → MessageBox + Shutdown(1)
  │
  └─ Show MainWindow
```

### 3.2 Navigation Flow

```
Operator clicks "🎵 Songs" sidebar button
  │
  ▼
MainViewModel.NavigateToSongsCommand
  │  GetRequiredService<SongsViewModel>()   ← Transient: new VM each time
  │
  ▼
MainViewModel.CurrentView = SongsViewModel
  │
  ▼
ContentControl in MainWindow.xaml
  │  DataTemplate lookup: SongsViewModel → SongsView (UserControl)
  │
  ▼
SongsView.OnLoaded()
  │  vm.LoadCommand.Execute(null)
  │
  ▼
SongsViewModel.LoadAsync()
  │  ISongService.GetAllAsync()
  │  ISongRepository.GetAllAsync()
  │  AppDbContext  (created, queried, disposed)
  │
  ▼
Songs ObservableCollection populated → UI renders list
```

### 3.3 Projection Flow

```
Operator clicks "▶ Project" on a song row
  │
  ▼
SongsViewModel.ProjectSongCommand(song)
  │  ISongService.GenerateSlides(song)
  │    → one Slide per ordered SongSection
  │
  ▼
IProjectionService.LoadSlides(slides, contextLabel)   ← Singleton
  │  _isProjecting = true
  │  _currentIndex = 0
  │  RaiseSlideChanged(slides[0])   ← catches subscriber exceptions
  │  RaiseProjectionStateChanged(true)
  │
  ├──▶ MainViewModel.OnSlideChanged()
  │      CurrentSlideLabel = slide.Label
  │      (updates "PROJECTING" bar in MainWindow)
  │
  └──▶ ProjectionWindow.OnSlideChanged()
         Dispatcher.Invoke(() => RenderSlide(slide))
           │
           ├─ SlideType.Song/Bible → ShowText(slide.Content)
           ├─ SlideType.Media      → ShowMedia(slide.MediaPath)
           └─ SlideType.Blank      → ShowBlankOverlay()
```

### 3.4 Song Save Flow

```
Operator fills in title + sections, clicks "Save Song"
  │
  ▼
AddEditSongViewModel.SaveCommand
  │  BuildEntity() → Song domain object (Id=0 for new, Id>0 for edit)
  │  Saved event raised
  │
  ▼
SongsViewModel.OnSongSaved(song)
  │  IsEditing = false / EditViewModel = null
  │
  ├─ song.Id == 0 → ISongService.CreateAsync(song)
  │                   ISongRepository.AddAsync(song)
  │                   AppDbContext.SaveChangesAsync()
  │                     StampTimestamps() → CreatedAt + UpdatedAt set
  │
  └─ song.Id > 0 → ISongService.UpdateAsync(song)
                    ISongRepository.UpdateAsync(song)
                      RemoveRange(existing.Sections)   ← replace all sections
                      Add new sections with Id=0
                      AppDbContext.SaveChangesAsync()
  │
  ▼
SongsViewModel.LoadAsync()   ← full reload to reflect DB truth
```

---

## 4. API Routes

This is a **desktop application** — there are no HTTP endpoints, REST APIs, or network services. All communication is internal between layers via C# interfaces.

The equivalent "API surface" is the Application service interfaces:

### `ISongService`
| Method | Description |
|---|---|
| `GetByIdAsync(id)` | Fetch one song with sections |
| `GetAllAsync()` | All songs ordered by title |
| `SearchByTitleAsync(term)` | Case-insensitive title search |
| `CreateAsync(song)` | Persist new song + sections |
| `UpdateAsync(song)` | Replace song metadata + all sections |
| `DeleteAsync(id)` | Hard delete song (cascades to sections) |
| `GenerateSlides(song)` | Map ordered sections → `Slide[]` (pure, no I/O) |

### `IBibleService`
| Method | Description |
|---|---|
| `GetVersionsAsync()` | All imported Bible translations |
| `GetBooksAsync(versionId)` | Books in a version, ordered by BookNumber |
| `GetVersesAsync(versionId, book, chapter)` | All verses for a chapter |
| `GetVerseAsync(versionId, book, chapter, verse)` | Single verse lookup |
| `SearchAsync(versionId, term, maxResults)` | Full-text LIKE search in verse text |
| `ImportVersionAsync(version, books, verses)` | Bulk import (batched, transactional) |
| `DeleteVersionAsync(versionId)` | Remove a translation + all its data |
| `GenerateSlide(verses)` | Compose verse(s) into a `Slide` (pure) |

### `IProjectionService` (Singleton)
| Method | Description |
|---|---|
| `LoadSlides(slides, contextLabel)` | Start projection with a slide list |
| `Next()` | Advance one slide |
| `Previous()` | Go back one slide |
| `GoTo(index)` | Jump to specific slide by index |
| `ShowBlank()` | Show black screen without stopping |
| `Stop()` | End projection, clear all state |
| Event: `SlideChanged` | Fires on every slide transition |
| Event: `ProjectionStateChanged` | Fires when projection starts/stops |

### `IThemeService`
| Method | Description |
|---|---|
| `GetAllAsync()` | All themes |
| `GetDefaultAsync()` | The active default theme (throws if none — DB corruption) |
| `CreateAsync(theme)` | Add theme |
| `UpdateAsync(theme)` | Update; handles default-flag exclusivity |
| `DeleteAsync(id)` | Rejects if theme is the default |

---

## 5. Database

**Engine**: SQLite (file at `%LocalAppData%\OpenAdoration\openadoration.db`)  
**ORM**: Entity Framework Core 9, Fluent API configuration  
**Migrations**: auto-applied at startup via `MigrateAsync()`

### 5.1 Schema Overview

```
┌──────────────────────────────────────────────────────────────┐
│ Songs                       │ SongSections                   │
│─────────────────────────────│────────────────────────────────│
│ Id             INT  PK      │ Id           INT  PK           │
│ Title          TEXT NOT NULL│ SongId       INT  FK → Songs   │
│ Author         TEXT NULL    │ Type         INT  (enum)       │
│ Classification TEXT NULL    │ SectionNumber INT              │
│ CreatedAt      DATETIME     │ Lyrics       TEXT NOT NULL     │
│ UpdatedAt      DATETIME     │ Order        INT  NOT NULL     │
│                             │ CreatedAt    DATETIME          │
│                             │ UpdatedAt    DATETIME          │
│  1 Song ──< many Sections (Cascade delete)                   │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ Themes                                                       │
│──────────────────────────────────────────────────────────────│
│ Id                   INT   PK                                │
│ Name                 TEXT  NOT NULL  (max 100)               │
│ FontFamily           TEXT  NOT NULL  (max 100)               │
│ FontSize             INT   NOT NULL                          │
│ FontColor            TEXT  NOT NULL  (max 9, e.g. #FFFFFF)   │
│ BackgroundColor      TEXT  NOT NULL  (max 9)                 │
│ BackgroundImagePath  TEXT  NULL      (max 1024)              │
│ BackgroundVideoPath  TEXT  NULL      (max 1024)              │
│ TextAlignment        TEXT  NOT NULL  default "Center"        │
│ IsDefault            BOOL  NOT NULL                          │
│ CreatedAt            DATETIME                                │
│ UpdatedAt            DATETIME                                │
│                                                              │
│  Seeded: Id=1, Name="Default", black bg, white 48pt Arial,  │
│          TextAlignment="Center", IsDefault=true              │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ BibleVersions        │ BibleBooks           │ BibleVerses    │
│──────────────────────│──────────────────────│────────────────│
│ Id          INT  PK  │ Id           INT  PK │ Id    INT  PK  │
│ Name        TEXT     │ BibleVersionId INT FK│ BibleVersionId │
│ Abbreviation TEXT    │ Name         TEXT    │ Book  TEXT     │
│ Language    TEXT     │ Abbreviation TEXT    │ Chapter INT    │
│ CreatedAt   DATETIME │ Testament    INT     │ Verse  INT     │
│ UpdatedAt   DATETIME │ BookNumber   INT     │ Text  TEXT     │
│                      │ ChapterCount INT     │ CreatedAt      │
│                      │ CreatedAt    DATETIME│ UpdatedAt      │
│                      │ UpdatedAt    DATETIME│                │
│  1 Version ──< Books ──< Verses (all cascade on version delete)
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ WorshipServices      │ ScheduleItems  (TPH — all in one table)│
│──────────────────────│──────────────────────────────────────│
│ Id    INT  PK        │ Id         INT  PK                   │
│ Name  TEXT           │ ServiceId  INT  FK → WorshipServices │
│ Date  DATETIME       │ ItemType   TEXT  ← discriminator     │
│ CreatedAt DATETIME   │ Order      INT  NOT NULL             │
│ UpdatedAt DATETIME   │ ThemeId    INT? FK → Themes (SetNull)│
│                      │ CreatedAt  DATETIME                  │
│                      │ UpdatedAt  DATETIME                  │
│                      │                                      │
│                      │ -- Song rows also have:              │
│                      │   SongId   INT FK → Songs            │
│                      │                                      │
│                      │ -- Bible rows also have:             │
│                      │   Book, Chapter, VerseStart, VerseEnd│
│                      │   BibleVersionId INT? FK             │
│                      │                                      │
│                      │ -- Media rows also have:             │
│                      │   MediaFileId INT FK → MediaFiles    │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ MediaFiles                                                   │
│──────────────────────────────────────────────────────────────│
│ Id        INT   PK                                           │
│ FileName  TEXT  NOT NULL                                     │
│ FilePath  TEXT  NOT NULL                                     │
│ Type      INT   (enum: Image=0, Video=1)                     │
│ CreatedAt DATETIME                                           │
│ UpdatedAt DATETIME                                           │
└──────────────────────────────────────────────────────────────┘
```

### 5.2 Table Per Hierarchy (TPH) — ScheduleItems

All three schedule item types (`SongScheduleItem`, `BibleScheduleItem`, `MediaScheduleItem`) share a single `ScheduleItems` table. The `ItemType` TEXT column (`"Song"` / `"Bible"` / `"Media"`) is the EF Core discriminator. Columns for a given type are `NULL` in rows of other types.

### 5.3 Timestamp Handling

`AppDbContext.StampTimestamps()` intercepts every `SaveChanges` call:
- `Added` → sets both `CreatedAt` and `UpdatedAt` to `DateTime.UtcNow`
- `Modified` → sets only `UpdatedAt`; marks `CreatedAt` as not modified so it can't be overwritten

The seed data in `ThemeConfiguration` uses a **static** date (`new DateTime(2025,1,1, 0,0,0, DateTimeKind.Utc)`) — never `DateTime.UtcNow`, which would change on every migration run and cause spurious migrations.

### 5.4 Indexes

| Table | Index |
|---|---|
| Songs | `Title` (speed up search + ordered list) |
| ScheduleItems | `(ServiceId, Order)` composite |

---

## 6. External Dependencies

OpenAdoration has **zero runtime network dependencies**. All data is local.

### NuGet packages (runtime)

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Extensions.Hosting` | 9.0.4 | DI container, generic host |
| `Microsoft.EntityFrameworkCore` | 9.0.4 | ORM |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.4 | SQLite provider |
| `CommunityToolkit.Mvvm` | 8.4.0 | Source-generated MVVM (ObservableProperty, RelayCommand) |
| `Serilog` | 4.2.0 | Structured logging core |
| `Serilog.Extensions.Logging` | 9.0.0 | Bridge from MEL to Serilog |
| `Serilog.Sinks.File` | 6.0.0 | Rolling daily file output |
| `Serilog.Sinks.Debug` | 3.0.0 | IDE debug output sink |

### Build-time only

| Package | Purpose |
|---|---|
| `Microsoft.EntityFrameworkCore.Design` | EF Core CLI tooling (`dotnet ef migrations`) — present in both Infrastructure and WPF projects |

### Platform dependencies

| Dependency | Why |
|---|---|
| Windows 10+ | WPF requirement |
| .NET 10 Runtime | Target framework |
| `UseWindowsForms=true` in WPF `.csproj` | `System.Windows.Forms.Screen.AllScreens` — only reliable multi-monitor enumeration API |

---

## 7. Key Files

```
OpenAdoration.sln

OpenAdoration.Domain/
  Common/
    BaseEntity.cs                    ← Id, CreatedAt, UpdatedAt for all entities
  Entities/
    Song.cs                          ← Title, Author, Sections list
    SongSection.cs                   ← Type, SectionNumber, Lyrics, Order, Label
    WorshipService.cs                ← Name, Date, ordered Items list
    ScheduleItem.cs          ★       ← Abstract base; Theme override per item
    SongScheduleItem.cs              ← TPH subtype: points at Song
    BibleScheduleItem.cs             ← TPH subtype: Book/Chapter/VerseStart/VerseEnd
    MediaScheduleItem.cs             ← TPH subtype: points at MediaFile
    BibleVersion.cs                  ← Name, Abbreviation, Language
    BibleBook.cs                     ← BookNumber, Testament, ChapterCount
    BibleVerse.cs                    ← Book, Chapter, Verse, Text, Reference
    Theme.cs                         ← FontFamily/Size/Color, BackgroundColor/Image, IsDefault
    MediaFile.cs                     ← FileName, FilePath, Type (Image/Video)
  Enums/
    SectionType.cs                   ← Verse,Chorus,PreChorus,Bridge,Intro,Outro,Tag
    MediaType.cs                     ← Image, Video
    Testament.cs                     ← Old, New

OpenAdoration.Application/
  Common/
    Slide.cs                 ★       ← Runtime projection unit — never stored
    SlideType.cs                     ← Song, Bible, Media, Blank
  Repositories/                      ← Interface definitions only
    ISongRepository.cs
    IBibleRepository.cs
    IThemeRepository.cs
    IMediaRepository.cs
    IWorshipServiceRepository.cs
  Services/
    IProjectionService.cs    ★       ← Projection state machine contract
    ProjectionService.cs     ★       ← Singleton — the projection engine
    ISongService.cs / SongService.cs
    IBibleService.cs / BibleService.cs
    IThemeService.cs / ThemeService.cs
    IMediaService.cs / MediaService.cs
    IWorshipServiceService.cs / WorshipServiceService.cs

OpenAdoration.Infrastructure/
  Persistence/
    AppDbContext.cs           ★       ← StampTimestamps(), ApplyConfigurationsFromAssembly
    AppDbContextFactory.cs            ← Design-time factory for dotnet-ef tooling
  Configurations/
    SongConfiguration.cs              ← HasIndex(Title), Cascade delete sections
    SongSectionConfiguration.cs
    ScheduleItemConfiguration.cs ★   ← TPH discriminator setup
    ThemeConfiguration.cs            ← Seeds default theme with fixed timestamps
    BibleVersionConfiguration.cs / BibleBookConfiguration.cs / BibleVerseConfiguration.cs
    MediaFileConfiguration.cs
    WorshipServiceConfiguration.cs
  Repositories/
    SongRepository.cs         ★       ← UpdateAsync: replace-all-sections pattern
    BibleRepository.cs        ★       ← ImportVersionAsync: 1000-row batches + transaction
    ThemeRepository.cs                ← DeleteAsync: rejects default; ClearDefaultFlagAsync
    MediaRepository.cs
    WorshipServiceRepository.cs
  Logging/
    LoggingConfiguration.cs   ★       ← Configure(), UseOpenAdorationSerilog(), CloseAndFlush()
  Extensions/
    InfrastructureServiceExtensions.cs ★ ← AddInfrastructure(), InitialiseDatabaseAsync()
  Migrations/
    20260505012006_InitialCreate.cs   ← Full schema, seeds default theme

OpenAdoration.WPF/
  App.xaml / App.xaml.cs     ★       ← DI host, startup/shutdown, DataTemplate nav mapping
  MainWindow.xaml             ★       ← Shell: sidebar + ContentControl + projection bar
  ProjectionWindow.xaml.cs    ★       ← RenderSlide(), Dispatcher.Invoke, ShowOnSecondaryScreen()
  Helpers/
    ScreenHelper.cs                   ← GetSecondaryScreen(), PositionOnScreen()
  Converters/
    InverseBoolToVisibilityConverter.cs
  ViewModels/
    BaseViewModel.cs                  ← IsBusy, ErrorMessage, HasError
    MainViewModel.cs          ★       ← Navigation + projection controls + state sync
    SongsViewModel.cs         ★       ← Load, search filter, add/edit/delete, project
    AddEditSongViewModel.cs   ★       ← Section management, Saved/Cancelled events
    SongSectionViewModel.cs           ← Per-section: type, lyrics, move/delete events
    BibleViewModel.cs          ★       ← Bible browser — version/book/chapter cascade, multi-format import
    AddEditThemeViewModel.cs   ★       ← Theme editor — color pickers, alignment strip, live preview
    ThemeViewModel.cs                  ← Theme CRUD + set default
    ServiceScheduleViewModel.cs        ← (stub — Milestone 3)
    MediaViewModel.cs                  ← (stub — Milestone 4)
  Views/
    SongsView.xaml            ★       ← List panel ↔ edit panel toggle by IsEditing
    AddEditSongView.xaml      ★       ← Title/Author + sections list + type buttons
    BibleView.xaml            ★       ← 3-column Bible browser + import toolbar
    AddEditThemeView.xaml              ← Theme editor form with live preview rectangle
    ThemeView.xaml                     ← Theme list panel
    ServiceScheduleView.xaml           ← (stub — Milestone 3)
    MediaView.xaml                     ← (stub — Milestone 4)
  Styles/
    Colors.xaml                       ← All color/brush resources
    Base.xaml                 ★       ← All control styles (Button, TextBox, ComboBox, Cards)
```

OpenAdoration.Tests.Infrastructure/
  BibleImport/
    BibleParserTests.cs           ← 5 [Fact] tests, one per format, via BibleFormatDispatcher
    Fixtures/                     ← 5 minimal XML/JSON fixtures (1 book, 1 chapter, 3 verses each)

★ = highest-leverage files; start here when debugging

---

## 8. Common Gotchas

### 8.1 `UseWindowsForms=true` causes type ambiguity
**Problem**: Many WPF types (`UserControl`, `MessageBox`, `Application`, `Timer`) also exist in `System.Windows.Forms`. Adding `UseWindowsForms=true` (needed for `Screen.AllScreens`) pulls both namespaces in.  
**Rule**: Always fully-qualify ambiguous types in code-behind files:
```csharp
public partial class MyView : System.Windows.Controls.UserControl { }
System.Windows.MessageBox.Show(...);
using WpfApp = System.Windows.Application;
```

### 8.2 EF Core Design package must be on the startup project
**Problem**: `dotnet ef migrations` looks for design-time services on the `--startup-project`. `Microsoft.EntityFrameworkCore.Design` in Infrastructure has `PrivateAssets=all` so it doesn't flow transitively.  
**Rule**: `Microsoft.EntityFrameworkCore.Design` must be listed in **both** `OpenAdoration.Infrastructure.csproj` AND `OpenAdoration.WPF.csproj`.

### 8.3 Never use `DateTime.UtcNow` in entity seed data
**Problem**: `HasData()` seed records with runtime timestamps change value on every run, causing EF Core to detect a "change" and generate a spurious migration.  
**Rule**: All seed timestamps must be a `static readonly` fixed date (see `ThemeConfiguration.SeedDate`).

### 8.4 `IsBusy` guard in `LoadAsync`
**Problem**: `LoadAsync` opens with `if (IsBusy) return;`. If any caller sets `IsBusy = true` before calling `LoadAsync`, the load silently does nothing.  
**Rule**: Never set `IsBusy = true` from a caller that then calls `LoadAsync`. Let `LoadAsync` own its own busy state.

### 8.5 `ProjectionService` subscribers must not throw
**Problem**: `RaiseSlideChanged` and `RaiseProjectionStateChanged` catch all exceptions from subscribers and log them, because a UI bug must never crash the projection engine mid-service.  
**Implication**: If your subscriber silently fails, the projection window won't update — check the log.

### 8.6 `SongRepository.UpdateAsync` replaces all sections
**Problem**: EF Core's change tracking doesn't cleanly handle replacing a collection of owned entities when working with detached objects.  
**Pattern used**: Load `existing` from DB (tracked), call `RemoveRange(existing.Sections)`, add incoming sections with `Id = 0` (forces INSERT, not UPDATE). Any attempt to re-use old section IDs will cause duplicate key or concurrency errors.

### 8.7 Bible import memory pressure
**Problem**: A full Bible translation has ~31,000 verses. Loading all at once before insert exhausts memory.  
**Pattern used**: `BibleRepository.ImportVersionAsync` inserts in batches of 1,000 and calls `ChangeTracker.Clear()` after each batch. Do not remove this — it prevents the context from accumulating all 31,000 tracked entities.

### 8.8 `ContentPresenter` and DataTemplate resolution
**Problem**: The `ContentControl` in `MainWindow` resolves the view via `DataTemplate` lookup in `App.xaml`. If the ViewModel type doesn't match the `DataType` in the template, nothing is shown (blank content, no error).  
**Rule**: Every ViewModel navigated to via `MainViewModel.CurrentView` must have a corresponding `DataTemplate` in `App.xaml`.

### 8.9 `ProjectionWindow` events must be unsubscribed
**Problem**: If `ProjectionWindow` is closed without unsubscribing from `IProjectionService` events, the `ProjectionService` singleton holds a reference to the closed window, preventing GC and causing `Dispatcher.Invoke` on a dead window.  
**Handled by**: `ProjectionWindow.OnClosed()` unsubscribes both events.

### 8.10 WPF dark-theme `ComboBoxItem` visibility
**Problem**: WPF's default `ComboBoxItem` template uses `SystemColors.HighlightBrush`, ignoring `Background` and `Foreground` style setters entirely. On a dark theme, items render as dark text on a dark background (invisible).  
**Fix**: `FieldComboBox` in `Base.xaml` includes a full `ControlTemplate` on `ComboBoxItem` that uses `{TemplateBinding Background}`. Do not remove the inner template — without it, the dropdown appears empty.

---

## 9. Common Operations

### 9.1 First-time setup
```bash
git clone https://github.com/g0elles/OpenAdoration.git
cd OpenAdoration

# Install EF Core tools (once per machine)
dotnet tool install --global dotnet-ef

# Build
dotnet build

# Run — MigrateAsync() creates the DB automatically on first launch
dotnet run --project OpenAdoration.WPF
```

### 9.2 Adding an EF Core migration
```bash
dotnet ef migrations add <MigrationName> \
  --project OpenAdoration.Infrastructure \
  --startup-project OpenAdoration.WPF

# Migrations are applied automatically at startup via MigrateAsync()
# No need to run `dotnet ef database update` manually
```

### 9.3 Resetting the database
```powershell
# Stop the app first, then:
Remove-Item "$env:LOCALAPPDATA\OpenAdoration\openadoration.db" -Force
# Next launch re-creates from scratch via MigrateAsync()
```

### 9.4 Reading logs
```powershell
# Today's log:
Get-Content "$env:LOCALAPPDATA\OpenAdoration\logs\openadoration-$(Get-Date -Format yyyyMMdd).log" -Tail 100

# Stream live (like tail -f):
Get-Content "$env:LOCALAPPDATA\OpenAdoration\logs\openadoration-$(Get-Date -Format yyyyMMdd).log" -Wait -Tail 50
```

Log format:
```
2026-05-11 14:23:01.123 [INF] OpenAdoration.WPF.ViewModels.SongsViewModel: Failed to load songs
```

EF Core `Database.Command` and `Infrastructure` noise is suppressed to `Warning` level — only slow/failed queries appear.

### 9.5 Debugging projection issues
1. Check `[INF] ProjectionService: Loading N slide(s) for 'Song Title'` — confirms `LoadSlides` was called.
2. Check `[INF] ProjectionService: Projection started` — confirms state machine entered projecting state.
3. If the ProjectionWindow shows nothing: look for `[ERR]` lines in `ProjectionWindow` — subscriber exceptions are caught and logged but the slide still changes.
4. If `ProjectionWindow` opens on the wrong screen: look for `[WRN] No secondary screen detected`.

### 9.6 Adding a new feature (e.g. Bible browser)
1. **Domain** — entities already exist (`BibleVersion`, `BibleBook`, `BibleVerse`)
2. **Application** — `IBibleService` and `BibleService` are fully implemented
3. **WPF ViewModel** — replace the stub in `BibleViewModel.cs` with full implementation
4. **WPF View** — replace the stub `BibleView.xaml` with full UI
5. **App.xaml** — DataTemplate already registered (`BibleViewModel → BibleView`)
6. **No migration needed** — Bible tables already exist in `InitialCreate`

### 9.7 Build the solution
```bash
dotnet build OpenAdoration.sln --configuration Release
```

### 9.8 Run parser tests
```bash
dotnet test OpenAdoration.Tests.Infrastructure
# Expected: 5/5 pass (Zefania, OSIS, USFX, Thiagobodruk, OpenAdoration JSON)
```

Output: `OpenAdoration.WPF\bin\Release\net10.0-windows\OpenAdoration.exe`

### 9.9 Feature status at a glance

| Feature | Domain | Service | Repository | ViewModel | View |
|---|---|---|---|---|---|
| Songs | ✅ | ✅ | ✅ | ✅ | ✅ |
| Bible | ✅ | ✅ | ✅ | ✅ | ✅ |
| Themes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Service Schedule | ✅ | partial | partial | stub | stub |
| Media | ✅ | ✅ | ✅ | stub | stub |
| Projection engine | — | ✅ | — | ✅ | ✅ |
