# Changelog

All notable changes to OpenAdoration are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Notes/Sermon content type (F9).** A new plain-text content type, alongside Songs/Bible/Media:
  type or paste text (blank lines separate slides), add it to a service schedule or project it
  standalone from the new Notes page. Supports the same `**bold**` markers as song lyrics (F8),
  and Stage View's live style editor (F7) works for it from the start — both for a service-driven
  Notes item ("This Occurrence" scope) and standalone Notes (the app-wide default style).
- **Bold text in lyrics (F8).** Wrap any part of a lyric line in `**...**` (select text in the
  song editor and press Ctrl+B to toggle it) and that span renders bold on the projector and in
  Stage View's live preview. No new storage — `SongSections.Lyrics` stays plain text; the markers
  live inline and are parsed at render time.
- **Stage View live style editor for the live song or Bible passage (F7 rebuild).** Superseded
  the swatch-based quick fix, which never persisted and couldn't touch the background at all. The
  operator can now pick a scope (the song itself, or just this occurrence in the current service),
  adjust font size, pick any text colour, and set a background colour/image/video — changes write
  into a real theme (cloning one first if needed, so a shared/default theme is never mutated for
  other songs) and show up immediately on the projector, surviving a live-item change or app
  restart. A live Bible passage gets the same editor too — scoped to just that occurrence when
  it's part of a service, or to the app's default Bible style when browsed and projected directly
  (scripture has no song-like library entry of its own to point a narrower scope at).
- **Word (`.docx`) song import.** Pick one or many `.docx` files at once — each file's name
  becomes the song title and its blank-paragraph-separated paragraphs become verses (manual line
  breaks inside a paragraph stay as lyric lines), the same rule plain-text import already uses.
- **VideoPsalm songbook import (`.vpc`).** Import every song from a full VideoPsalm backup's
  `SongBooks/Songs.vpc` in one go, alongside the existing single-service `.vpagd` agenda import.
  Re-importing skips songs already in the library instead of duplicating them. Picking a `.vpc`
  that's actually a protected VideoPsalm Bible export is detected and redirected to Bible import.

### Fixed
- **Stage View now mirrors a scrolling lower-third.** The operator's Stage View showed the
  lower-third as static text even when "Desplazar el texto continuamente (marquesina)" was
  enabled in Settings; it now runs the same right-to-left ticker animation as the projector.

## [2.1.0] — 2026-08-30

### Added
- **Crossfade slide transitions (M15.1).** Slide changes now truly crossfade: the outgoing
  slide stays on screen as a still and fades/pushes out while the new one enters, so the
  projection never blanks between slides. Applies to Fade (real crossfade), Slide (old exits
  left in step with the new entering) and Zoom (old fades over the incoming zoom).
- **Transition preview in the theme editor (M15.2).** Picking a transition replays it on the
  Live Preview's sample lyrics so the operator sees the motion before Sunday.
- **Ticker lower-third with configurable band (M15.3).** The persistent lower-third can now
  scroll its text continuously right-to-left (marquee) until cleared — for welcome messages,
  announcements longer than the screen, or a news-style band. Settings → General gains a
  "Lower third" group: scroll on/off, speed, band colour (incl. opacity), text colour and size.
- **Stage View mirrors the lower-third (M15.4)** so the operator/stage sees what overlay is live.
- **Managed background-media library.** Theme backgrounds are now a first-class, reusable
  media category. Picking a background in the theme editor copies it into the managed media
  store (deduped by content, so a video shared across a service's themes is one file) instead
  of referencing a foreign folder. A new **Media → Backgrounds** subsection lists/imports them,
  and the theme editor gains a "choose an existing background" picker so a previously-used
  background can be re-applied without hunting for the file. Backgrounds are an *exclusive*
  category — a file is either a background or general slide media, never both.
- **Background delete-guard.** A background still referenced by a theme can't be deleted; the
  app blocks it with a "used by N theme(s)" message so projection can't lose its background.

### Fixed
- **Theme editor preview plays the background video.** The Live Preview in the theme editor
  showed a static "video background active" placeholder; it now plays the chosen background
  video (muted, looping, any codec via FFME), matching the projector and the Stage View fix.
- **Media import no longer freezes the window.** Hashing/copying files during import now runs
  off the UI thread, so large or multiple imports stay responsive.

### Security
- Plugin install now validates the plugin id before using it in a file path, closing a path-
  traversal risk from a malicious `.oaplugin`.
- The bundled SQLite native library was updated past a known vulnerability
  (GHSA-2m69-gcr7-jv3q).
- Auto-update now requires and verifies a SHA-256 digest on the downloaded release asset before
  installing it.
- Background image/video imports are now validated for size and content signature, matching the
  general media-import policy.
- Plugin packages larger than 100 MB (uncompressed) are rejected before extraction.
- Plugin settings, including bring-your-own-key API keys, are now encrypted at rest (DPAPI,
  current-user scope) instead of stored as plaintext JSON.

### Notes
- Upgrade-safe: the schema change is additive and a startup reconcile only *adds* library rows
  pointing at files already on disk — it never modifies or deletes themes/media, so an updated
  user keeps everything they had set up. Pre-existing VideoPsalm-imported backgrounds are
  adopted into the new library automatically.

## [2.0.1] — 2026-06-20

Patch release: Stage View and localization fixes.

### Fixed
- **Stage View now mirrors the projector for video-background themes.** The operator
  preview played a placeholder ("Video background active") whose text overlapped the
  centred lyrics; it now plays the theme's background video (muted, looping, any codec
  via FFME) behind the lyrics in both the current and "Up Next" panels.
- **Media library "Project" button is now localized** (showed English "Project" even in a
  Spanish session; now uses the shared `Common_Project` string → "Proyectar").

## [2.0.0] — 2026-06-19

Major release: reliability, full internationalization, richer presentation, VideoPsalm
migration, content-level theming with a runtime Light/Dark UI, and media thumbnails.

### Added
- **Backup & restore (M8):** export/import the whole library to a portable `.oabak` file;
  staged database swap on restart.
- **Opt-in auto-update (M8):** checks GitHub releases for a newer MSI and hands off to the
  installer; cancelling the UAC prompt leaves the app running.
- **Internationalization (M11):** full English + Spanish UI (resx + `{loc:Loc}` + live
  `TranslationSource`); language picker in Settings.
- **Runtime Light/Dark app theme (G27):** Light and Dark chrome palettes, live swap from
  Settings without restart, persisted. Projection output is unaffected (theme-entity driven).
- **Content-level theming (M14):** a theme attaches to content (song + per-content-type
  defaults) and is resolved by one cascade everywhere; optional per-theme slide transition.
- **Transitions (M10):** Cut / Fade / Slide / Zoom.
- **Persistent lower-thirds (M10):** show/clear an overlay independent of the current slide.
- **Media transport controls (M10.5):** restart / −10s / play-pause / +10s / progress for
  projected video, plus an any-codec engine (FFmpeg/FFME) that plays HEVC/H.265 and more.
- **Media thumbnails:** image + video previews in the library and the schedule add-media
  picker, with an FFmpeg frame-grab fallback for codecs Windows can't thumbnail (e.g. HEVC
  iPhone `.MOV`), cached to disk.
- **More imports (M9/M12):** ChordPro songs (`.cho/.crd/.chopro/.chordpro`); VideoPsalm
  full-agenda `.vpagd` migration (songs/scripture/media/schedule/themes, references-only
  scripture, encrypted `.vpc` Bibles detected and refused); image-folder media import.
- **Bible quick-reference jump (M9):** one smart search box (reference / keyword / phrase).
- **Drag-to-reorder** schedule items (alongside the ▲▼ arrows).
- **Plugin foundation (M13):** `.oaplugin` contract + isolated loader (UI hidden until the
  first connector module ships).
- **Architecture tests:** NetArchTest enforces the Clean Architecture layer boundaries (G28).

### Changed
- App-chrome brushes are now `{DynamicResource}` so the theme can swap at runtime.
- Chrome icons moved from color emoji to Segoe Fluent Icons for consistent rendering.
- Upgraded to .NET 10 and EF Core 10.
- FFmpeg binaries are pinned by exact build filename + SHA256 in `fetch-ffmpeg.ps1`
  (supply-chain hardening).
- The language selector now offers Spanish (the v1.1 English-only lock is lifted).

### Fixed
- Safe migrations: an automatic DB snapshot is taken before a schema migration and restored
  on failure.
- QA pass fixes: keyboard shortcuts work without the projection bar focused; clearing an
  announcement/lower-third clears its text; the stage preview honours pause; Spanish label
  overflow/clipping; default-theme changes refresh the projection; Settings prompts on leave
  with unsaved changes.

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

[2.1.0]: https://github.com/g0elles/openadoration/releases/tag/v2.1.0
[2.0.1]: https://github.com/g0elles/openadoration/releases/tag/v2.0.1
[2.0.0]: https://github.com/g0elles/openadoration/releases/tag/v2.0.0
[1.1.0]: https://github.com/g0elles/openadoration/releases/tag/v1.1.0
[1.0.1]: https://github.com/g0elles/openadoration/releases/tag/v1.0.1
[1.0.0]: https://github.com/g0elles/openadoration/releases/tag/v1.0.0
