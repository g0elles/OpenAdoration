# OpenAdoration

Free, open-source worship presentation software for Windows. A self-hostable alternative to EasyWorship — fully offline, no accounts, no subscriptions.

📖 **Guía del usuario (español):** [`docs/GUIA-USUARIO.md`](docs/GUIA-USUARIO.md) — cómo usar la aplicación durante el servicio.

---

## What it does

A single operator runs OpenAdoration during a church service to control what appears on the projector screen. Content is managed in the app, and slides are sent to a secondary monitor in real time.

| Module | What you can do |
|---|---|
| **Songs** | Build a song library with structured sections (Verse, Chorus, Bridge, etc.); search and project any song; navigate between sections with ◀/▶; set play order; import from OpenLyrics, OpenSong, and plain text |
| **Bible** | Browse any imported translation by book and chapter; click a verse to project it; search by keyword (FTS); select multi-verse ranges; import from 8 file formats |
| **Themes** | Control fonts, colors, and backgrounds (solid color, image, or looping video) for all projected content |
| **Media** | Import images and videos; project them full-screen with a click; videos play with audio |
| **Service Schedule** | Build a setlist before the service; add songs, Bible passages, and media; reorder items; per-item auto-advance and verse-order override; go live and navigate the schedule; add items to the queue on the fly |
| **Projection** | Full-screen output on a secondary monitor; header/body/footer zones with template tokens; announcement banner; configurable fade; blank screen with one key; theme applies per slide |
| **Stage View** | Operator monitor: themed preview of the current slide + UP NEXT (including the next schedule item); Prev/Next item controls |
| **Settings** | Church name + CCLI for `[ChurchName]`/`[SiteLicense]` tokens; default auto-advance; verses-per-slide; announcement duration; transition speed |

---

## Requirements

**To run the installed app:**
- Windows 10 or later
- *No .NET install needed* — the release is a self-contained build
- A secondary monitor or projector (optional — a floating preview window is shown on single-screen setups)

**To build from source:** see [Getting started](#getting-started) — needs the .NET 10 SDK.

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

### Build a release (self-contained exe + installer)

```powershell
# Requires the WiX v5 CLI once per machine:
dotnet tool install --global wix --version 5.0.2

pwsh installer/build.ps1 -Version 1.0.0
# → installer/out/OpenAdoration-1.0.0-win-x64.msi
#   (and a single self-contained OpenAdoration.exe that needs no .NET install)
```

See [`docs/RELEASE.md`](docs/RELEASE.md) for the full tag → build → GitHub-release flow.

### Run tests

```bash
dotnet test OpenAdoration.Tests.Infrastructure
# 16/16 — 10 Bible parser tests (Zefania, OSIS, USFX, thiagobodruk JSON,
#   OpenAdoration JSON, BibleSuperSearch JSON / ZIP / SQLite + ZIP guards)
#   and 6 song import tests (OpenLyrics, OpenSong, plain text)
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
OpenAdoration.Tests.Infrastructure/  Bible + song import parser tests
```

---

## Status

**v1.0 — released.** All milestones (M0–M7) complete; v2.0 is in planning (see [`ROADMAP.md`](ROADMAP.md)).

| Feature | Status |
|---|---|
| Songs — CRUD, search, projection, play order, OpenLyrics/OpenSong/text import | Done |
| Themes — colors, fonts, image/video backgrounds, 3-zone layout + tokens | Done |
| Bible — browser, multi-verse selection, 8-format import, keyword + phrase FTS | Done |
| Service Schedule — builder + live mode + auto-advance + verse-order override | Done |
| Media — import, project images and videos (with audio) | Done |
| Projection — multi-monitor, theme per slide, tokens, announcements, fade, blank | Done |
| Stage View — operator preview + cross-item UP NEXT | Done |
| Settings — church tokens, defaults | Done |
| Keyboard shortcuts | Done |
| Installer / packaging (self-contained exe + MSI) | Done |

### Coming in v2.0

Backup/restore, opt-in auto-update, more import formats (songs + image/PDF decks), Bible quick-reference jump, richer transitions, persistent overlays, and dual-version scripture. (Video transport controls shipped in v1.1.0.) See [`ROADMAP.md`](ROADMAP.md) Milestones 8–13.

---

## Documentation

| Doc | Purpose |
|---|---|
| [`docs/GUIA-USUARIO.md`](docs/GUIA-USUARIO.md) | **User guide (Spanish)** — operating the app during a service |
| [`ROADMAP.md`](ROADMAP.md) | Milestones — v1.0 shipped, v2.0 in planning |
| [`CHANGELOG.md`](CHANGELOG.md) | What shipped in each version |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Developer reference — layers, data flows, DB schema |
| [`docs/RELEASE.md`](docs/RELEASE.md) | How to cut a release |

> A multi-language UI (including English + Spanish) is planned for v2.0 — see ROADMAP.md, Milestone 11.

## Contributing

Contributions are welcome. Please open an issue before submitting a pull request to discuss the change.

## License

MIT
