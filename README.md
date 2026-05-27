# OpenAdoration

Free, open-source worship presentation software for Windows. A self-hostable alternative to EasyWorship — fully offline, no accounts, no subscriptions.

---

## What it does

A single operator runs OpenAdoration during a church service to control what appears on the projector screen. Content is managed in the app, and slides are sent to a secondary monitor in real time.

| Module | What you can do |
|---|---|
| **Songs** | Build a song library with structured sections (Verse, Chorus, Bridge, etc.); search and project any song; navigate between sections with ◀/▶ |
| **Bible** | Browse any imported translation by book and chapter; click a verse to project it; search by keyword (FTS); select multi-verse ranges; import from 8 file formats |
| **Themes** | Control fonts, colors, and backgrounds (solid color, image, or looping video) for all projected content |
| **Media** | Import images and videos; project them full-screen with a click; videos play with audio |
| **Service Schedule** | Build a setlist before the service; add songs, Bible passages, and media; reorder items; go live and navigate through the schedule; add items to the queue on the fly during the service |
| **Projection** | Full-screen output on a secondary monitor; preview window on single-screen setups; blank screen with one key; theme applies per slide |

---

## Requirements

- Windows 10 or later
- .NET 10 Runtime
- A secondary monitor or projector (optional — a floating preview window is shown on single-screen setups)

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `dotnet-ef` CLI tool (for migrations, first time only):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### Run

```bash
git clone https://github.com/g0elles/OpenAdoration.git
cd OpenAdoration
dotnet run --project OpenAdoration.WPF
```

The database is created and migrated automatically on first launch.

| Path | Contents |
|---|---|
| `%LocalAppData%\OpenAdoration\openadoration.db` | SQLite database |
| `%LocalAppData%\OpenAdoration\Media\` | Imported media files (managed copy) |
| `%LocalAppData%\OpenAdoration\logs\` | Daily rolling log files |

### Build for release

```bash
dotnet build OpenAdoration.sln --configuration Release
# Output: OpenAdoration.WPF\bin\Release\net10.0-windows\OpenAdoration.exe
```

### Run tests

```bash
dotnet test OpenAdoration.Tests.Infrastructure
# 8/8 Bible parser tests (Zefania, OSIS, USFX, thiagobodruk JSON,
#   OpenAdoration JSON, BibleSuperSearch JSON / ZIP / SQLite)
```

---

## Bible import

OpenAdoration supports 8 Bible file formats, auto-detected by file extension and content:

| Format | Extension | Notes |
|---|---|---|
| Zefania XML | `.xml` | Root element `XMLBIBLE` or `ZEFANIA` |
| OSIS XML | `.xml` | Root element `osis`; streaming parser |
| USFX XML | `.xml` | Root element `usfx`; streaming parser |
| thiagobodruk JSON | `.json` | Array of books with chapters as arrays of arrays |
| OpenAdoration JSON | `.json` | Native export format |
| BibleSuperSearch JSON | `.json` | Object with `metadata` + `verses` array |
| BibleSuperSearch ZIP | `.zip` | `info.json` + pipe-delimited `verses.txt` |
| BibleSuperSearch SQLite | `.sqlite` | `meta` + `verses` tables |

Book names are preserved from the source file when available (localized).

---

## Tech stack

| Layer | Technology |
|---|---|
| UI | WPF (.NET 10) |
| Architecture | Clean Architecture — Domain / Application / Infrastructure / WPF |
| Database | SQLite via Entity Framework Core 9 |
| MVVM | CommunityToolkit.Mvvm 8.4 (source-generated) |
| Full-text search | SQLite FTS5 (Bible verse search) |
| Logging | Serilog — rolling daily file + debug sink |
| Multi-monitor | `System.Windows.Forms.Screen.AllScreens` |

---

## Project structure

```
OpenAdoration.Domain/           Entities, enums, BaseEntity
OpenAdoration.Application/      Service + repository interfaces, ProjectionService, Slide DTO
OpenAdoration.Infrastructure/   EF Core DbContext, migrations, repositories, Serilog setup
OpenAdoration.WPF/              WPF app — windows, views, view models, converters, styles
OpenAdoration.Tests.Infrastructure/  Bible parser integration tests
```

---

## Status

Pre-beta — all core features implemented.

| Feature | Status |
|---|---|
| Songs — CRUD, search, projection | Done |
| Themes — colors, fonts, image/video backgrounds | Done |
| Bible — browser, multi-verse selection, 8-format import, FTS | Done |
| Service Schedule — builder + live mode + on-the-fly queue editing | Done |
| Media — import, project images and videos (with audio) | Done |
| Projection engine — multi-monitor, theme per slide, blank | Done |
| Keyboard shortcuts | Planned |
| Installer / packaging | Planned |

---

## Contributing

Contributions are welcome. Please open an issue before submitting a pull request to discuss the change.

## License

MIT
