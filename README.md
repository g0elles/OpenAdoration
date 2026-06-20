# OpenAdoration

Free, open-source worship presentation software for Windows. A self-hostable alternative to EasyWorship — fully offline, no accounts, no subscriptions.

📖 **Guía del usuario (español):** [`docs/GUIA-USUARIO.md`](docs/GUIA-USUARIO.md) — cómo usar la aplicación durante el servicio.

---

## What it does

A single operator runs OpenAdoration during a church service to control what appears on the projector screen. Content is managed in the app, and slides are sent to a secondary monitor in real time.

| Module | What you can do |
|---|---|
| **Songs** | Build a song library with structured sections (Verse, Chorus, Bridge, etc.); search and project any song; navigate between sections with ◀/▶; set play order; import from OpenLyrics, OpenSong, ChordPro, and plain text |
| **Bible** | Browse any imported translation by book and chapter; click a verse to project it; one smart search box (reference jump / keyword FTS / exact phrase); select multi-verse ranges; import from 8 file formats |
| **Themes** | Control fonts, colors, and backgrounds (solid color, image, or looping video); assign a theme to content (per song + per-content-type defaults) resolved by one cascade; optional per-theme slide transition |
| **Media** | Import images and videos (single files or a whole folder); thumbnails for both — incl. HEVC/iPhone `.MOV`; project full-screen with a click; transport controls (restart / ±10s / play-pause / seek); any codec via FFmpeg |
| **Service Schedule** | Build a setlist; add songs, Bible passages, and media; reorder with drag-and-drop or ▲▼; per-item auto-advance and verse-order override; import a full VideoPsalm agenda; go live and navigate; add to the queue on the fly |
| **Projection** | Full-screen output on a secondary monitor; header/body/footer zones with template tokens; announcement banner; persistent lower-third; Cut/Fade/Slide/Zoom transitions; blank screen with one key; theme applies per slide |
| **Stage View** | Operator monitor: themed preview of the current slide + UP NEXT (including the next schedule item); Prev/Next item controls |
| **Settings** | Church name + CCLI tokens; UI **language** (English/Spanish) and **Light/Dark appearance**; default auto-advance; verses-per-slide; announcement duration; transition speed; backup/restore; opt-in update check |

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

pwsh installer/build.ps1 -Version 2.0.0
# → installer/out/OpenAdoration-2.0.0-win-x64.msi
#   (and a single self-contained OpenAdoration.exe that needs no .NET install)
```

See [`docs/RELEASE.md`](docs/RELEASE.md) for the full tag → build → GitHub-release flow.

### Run tests

```bash
dotnet test OpenAdoration.Tests.Infrastructure
# 70/70 — Bible parsers (Zefania, OSIS, USFX, thiagobodruk / OpenAdoration /
#   BibleSuperSearch JSON / ZIP / SQLite + ZIP guards + import sanity check),
#   song import (OpenLyrics, OpenSong, ChordPro, plain text), VideoPsalm agenda
#   + DRM detector, theme cascade, layer-boundary (NetArchTest), and en/es
#   localization parity
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
| UI | WPF (.NET 10); runtime Light/Dark theming via `DynamicResource` |
| Architecture | Clean Architecture — Domain / Application / Infrastructure / WPF (boundaries enforced by NetArchTest) |
| Database | SQLite via Entity Framework Core 10 |
| MVVM | CommunityToolkit.Mvvm 8.4 (source-generated) |
| Full-text search | SQLite FTS5 (Bible verse + song lyric search) |
| Video | FFmpeg via FFME (any codec, incl. HEVC) |
| i18n | resx + `{loc:Loc}` markup extension + live `TranslationSource` (en/es) |
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

**v2.0 — release-ready.** v1.0 (M0–M7) shipped 2026-06-01; the v2.0 line (M8–M14) is complete and
pending the release cut (see [`ROADMAP.md`](ROADMAP.md) and [`CHANGELOG.md`](CHANGELOG.md)).

| Feature | Status |
|---|---|
| Songs — CRUD, search, projection, play order, OpenLyrics/OpenSong/ChordPro/text import | Done |
| Themes — colors, fonts, image/video backgrounds, 3-zone tokens; per-content theming; per-theme transition | Done |
| Bible — browser, multi-verse selection, 8-format import, smart reference/keyword/phrase search | Done |
| Service Schedule — builder + live mode + auto-advance + verse-order + drag-reorder + VideoPsalm import | Done |
| Media — import (files/folder), thumbnails (incl. HEVC), project images + any-codec video, transport controls | Done |
| Projection — multi-monitor, theme per slide, tokens, announcements, lower-thirds, transitions, blank | Done |
| Stage View — operator preview + cross-item UP NEXT | Done |
| Settings — church tokens, defaults, language (en/es), Light/Dark, backup/restore, update check | Done |
| Internationalization — English + Spanish UI | Done |
| Reliability — backup/restore, opt-in auto-update, safe migrations | Done |
| Plugin foundation (`.oaplugin` loader; UI hidden until the first connector ships) | Done |
| Keyboard shortcuts · Installer / packaging (self-contained exe + MSI) | Done |

Deferred to the backlog (not blocking v2.0): EasyWorship/ProPresenter song import, PDF/pptx decks,
clean-output/NDI, and the api.bible connector (ships as a separate plugin repo). See [`ROADMAP.md`](ROADMAP.md).

---

## Documentation

| Doc | Purpose |
|---|---|
| [`docs/GUIA-USUARIO.md`](docs/GUIA-USUARIO.md) | **User guide (Spanish)** — operating the app during a service |
| [`ROADMAP.md`](ROADMAP.md) | Milestones — v1.0 shipped, v2.0 release-ready |
| [`CHANGELOG.md`](CHANGELOG.md) | What shipped in each version |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Developer reference — layers, data flows, DB schema |
| [`docs/RELEASE.md`](docs/RELEASE.md) | How to cut a release |

> The UI ships in **English and Spanish** (switch in Settings). Adding a language is just another resx.

## Contributing

Contributions are welcome. Please open an issue before submitting a pull request to discuss the change.

## License

MIT
