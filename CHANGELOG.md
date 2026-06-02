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
