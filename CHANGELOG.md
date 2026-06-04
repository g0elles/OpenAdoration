# Changelog

All notable changes to OpenAdoration are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] — v2.0 (planning)

Target themes (see `ROADMAP.md`, Milestones 8–10):

### Planned
- **Reliability & Releases (M8):** local backup/restore to a portable `.oabak` file;
  opt-in auto-update from GitHub releases; release infrastructure.
- **Content & Imports (M9):** more song importers (ChordPro/SongPro, EasyWorship,
  best-effort ProPresenter); image-folder and PDF deck import; Bible quick-reference jump.
- **Presentation Richness (M10):** transition library (cut/fade/slide/zoom); persistent
  lower-third overlays; dual-version scripture slides; clean output for livestream;
  media transport controls (play/pause/seek/restart) for projected video (M10.5).
- **Internationalization (M11):** multi-language UI. *Foundation done* — resx (en + es),
  `{loc:Loc}` markup extension + live `TranslationSource`, `ILocalizationService`,
  `UiCulture` setting, and a language dropdown in Settings; app chrome, About window and
  Settings localized to Spanish. *Remaining* — externalize the rest of the views, dialogs
  and ViewModel messages.

## [1.1.0] — 2026-06-03

Projection reliability and video controls.

### Added
- **Video transport controls.** When a video media slide is projected, the control bar
  shows restart, back 10 s, play/pause, forward 10 s, a progress bar, and a time readout.

### Fixed
- **Multi-monitor projection placement.** The projection window now opens reliably
  full-screen on the secondary monitor (positioned by physical pixels), instead of
  landing on the operator's screen under display scaling or a maximize-before-show race.
  When no second screen is detected it logs a clear hint to set Windows to "Extend".
- **Silent video failures are now logged.** An unsupported video codec is recorded with
  its underlying error and shows blank, instead of a silent black screen.

### Changed
- The UI is temporarily locked to English until the interface is fully translated; the
  language selector offers only English for now.

> Note: HEVC/H.265 video (e.g. iPhone `.MOV`) still requires a Windows HEVC decoder to
> play; broad built-in codec support is planned for a later release.

## [1.0.1] — 2026-06-03

Bug-fix release. Bible import is now resilient to real-world data files.

### Fixed
- **Bible import no longer crashes on split verses.** Exchange formats (Zefania, OSIS,
  USFX, …) often emit one logical verse as several elements sharing a verse number;
  these previously tripped a `UNIQUE` constraint and aborted the whole import (e.g. the
  Reina Valera RVA). Repeated `(Book, Chapter, Verse)` keys are now merged across all
  eight importers.
- **Imported verses are no longer unreadable.** OSIS/USFX files without a book `<title>`
  stored the book under its canonical name ("Judges") but the verses under the raw book
  id ("Judg"), so lookups found nothing. The book row and its verses now resolve the same
  name.
- Added a post-import check that warns when any book has no matching verses, surfacing
  this class of mismatch at import time.

> Existing Bibles imported with a prior version should be deleted and re-imported to
> pick up these fixes.

## [1.0.0] — 2026-06-01

First public release. Free, fully offline, SQLite-only worship presentation for Windows 10+.

### Added
- **Songs:** CRUD, two-step search (title/author + lyrics FTS with prefix matching),
  projection, play-order (VerseOrder) editor, copyright + CCLI fields.
- **Song import:** OpenLyrics XML, OpenSong XML, and plain text via `SongFormatDispatcher`
  (auto-detected by namespace / extension / content).
- **Bible:** 3-column browser, FTS keyword + exact-phrase search, configurable
  verses-per-slide, 8-format import (Zefania, OSIS, USFX, thiagobodruk, OpenAdoration,
  BibleSuperSearch JSON/ZIP/SQLite) with localized book names.
- **Themes:** CRUD, 3-zone Header/Body/Footer layout, token chips, text alignment,
  background colour / image / video, live preview.
- **Service Schedule:** builder (songs/Bible/media, reorder), live mode (per-item
  projection, Prev/Next item, click-to-jump), per-item auto-advance, per-item
  verse-order override.
- **Media:** import, project, delete — images and video (with audio).
- **Projection:** 3-zone token rendering, per-slide theme resolution, announcement
  banner overlay, configurable slide-change fade.
- **Stage View:** themed 1920×1080 previews of current + UP NEXT (cross-item),
  Prev/Next item, real video preview.
- **Token system:** 12 tokens (2 church + 5 song + 5 Bible) with auto-hiding zones
  and clickable chip insertion.
- **Settings:** `settings.json` (church name + CCLI, default auto-advance,
  verses-per-slide, announcement duration, transition ms).
- **Keyboard shortcuts:** Space/arrows/B/Esc/1–9 for live control; Ctrl+1–5 for navigation.
- **Packaging:** self-contained single-file `OpenAdoration.exe` (no .NET prerequisite)
  and a per-machine WiX v5 MSI with Start Menu + Desktop shortcuts.

[Unreleased]: https://github.com/g0elles/openadoration/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/g0elles/openadoration/releases/tag/v1.0.0
