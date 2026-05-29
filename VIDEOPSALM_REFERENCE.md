# VideoPsalm → OpenAdoration Reference
# Purpose: Feature gap analysis + implementation guidance based on reverse-engineering of VP 1.29.
# Read this when: designing or implementing new OA features; answering "how does VP do X?"; planning the roadmap.
# Companion files: CLAUDE.md (code standards), ARCHITECTURE.md (design), SESSION_STATUS.md (current build state).
# Last updated: 2026-05-29 (v3 — gap matrix reconciled after VP-parity batch + P1-1 verse-order-override + P1-2 settings/church-tokens; P0 now 100%, P1 only gap is slide transitions)

---

## 1. What VideoPsalm Is

VP 1.29 is a free Windows worship-presentation app (.NET WPF + DevExpress 18.1 + C#).
It is the primary competitor reference for OpenAdoration.
VP data lives in `C:\Users\Public\Documents\VideoPsalm\`.
VP is set to Spanish (es-MX) on the analysis machine.

---

## 2. Feature Gap Matrix

Legend: ✅ Done in OA | 🔶 Partial | ❌ Missing | — Not planned
Status reconciled 2026-05-29 against actual code (was last accurate before the VP-parity batch).

### P0 — Core (must-have) — ✅ 100% complete

| VP Feature | OA Status | Notes |
|---|---|---|
| Agenda / service planner | ✅ | `ServiceSchedule` — list + builder + live mode + auto-advance |
| Song library (CRUD, sections, search) | ✅ | Songs M0 + section types + two-step search (title + lyrics FTS) |
| Bible display (multi-version, book/ch/verse) | ✅ | Bible M2 + 8-format import |
| Multi-screen output (projection + presenter) | ✅ | `ProjectionWindow` + **Stage View** (embedded nav: themed 1920×1080 preview, UP NEXT cross-item, Prev/Next Item). Remote (browser) stage view still missing — see P2. |
| Text style system (theme per content type) | ✅ | OA Themes (font/color/bg) + **3-zone layout (Header/Body/Footer)** with auto-hide zones |
| Template tokens (`[SongTitle]`, `[BibleBookName]`, etc.) | ✅ | **12 tokens** (2 church + 5 song + 5 bible) via `ITokenResolver`; clickable chip insertion in theme editor |

### P1 — High value — only gap is slide transitions

| VP Feature | OA Status | Notes |
|---|---|---|
| Background system (image / video / solid color) | ✅ | OA Themes: BackgroundType = Color / Image / Video |
| Auto-advance (configurable interval per item) | ✅ | `ScheduleItem.AutoAdvanceSeconds`; DispatcherTimer (one-shot, resets on SlideChanged); cross-item advance at end. `DefaultAutoAdvanceSeconds` in settings applies to new items. |
| Verse order customization (reorder song sections per service) | ✅ | `Song.VerseOrder` (default) **and** `SongScheduleItem.VerseOrderOverride` (per-agenda override). `GetOrderedSections(override)` resolves token string. |
| CCLI license tracking (`[SiteLicense]` in footer) | ✅ | `Song.CcliNumber` + `[SongCCLI]` token; church-wide `ChurchCcliNumber` in settings + `[SiteLicense]` token; `[ChurchName]` too |
| Slide transitions (animated) | 🔶 | **Fade DONE** (configurable opacity DoubleAnimation on ContentLayers; `SlideTransitionMilliseconds`). VP's other 16 HLSL shader effects (FadeAndBlur, Blinds, Checkerboard, Melt, RotateWipe, Star, …) not implemented — not needed for now. |

### P2 — Nice to have

| VP Feature | OA Status | Notes |
|---|---|---|
| Live announcement / message slide | ✅ | `IProjectionService.ShowAnnouncement/ClearAnnouncement` + `CurrentAnnouncement`/`AnnouncementChanged`. Blue lower-third **banner overlay** (white text) over the untouched slide; auto-dismisses after `AnnouncementDurationSeconds` (default 25). Rendered by ProjectionWindow + StageView. |
| Bible search — phrase / word / Strong's modes | 🔶 | **Keyword (all-words, prefix) + exact-Phrase modes DONE** (FTS5; UI toggle in Bible browser). Strong's not planned (needs Strong's markup in source). |
| Song import formats (20+ formats) | 🔶 | **OpenLyrics XML done.** Planned next: OpenSong, plain text (P2-c). VP also imports CCLI `.usr`, EasyWorship, ZionWorx, ChordPro, SoftProjector, WorshipCenterPro, etc. |
| Configurable verses-per-slide (Bible) | ✅ | `DefaultBibleVersesPerSlide` setting; `BibleService.GenerateSlides` chunks verses. Applies to schedule Bible items + multi-verse selection. |
| Chord display (ChordPro / bracket) | ❌ | VP shows chords above lyrics or inline; `ViewBracketContentAsChord` flag on songbook |
| Audio-only agenda item | ❌ | VP: `AgendaAudio` item type — audio plays during service with no visual content (pre-service music). OA has no audio-only item. |
| Clock overlay (analog/digital, countdown) | — | DROPPED (decided with user — not needed). VP: 3 analog styles × 3 modes (clock/countdown/stopwatch). |
| Aspect-ratio presets / background brightness | ❌ | VP Fondo tab: Actual/4:3/16:9/16:10/16:8 + brightness slider on bg media |
| Bilingual / dual Bible mode | ❌ | VP shows two versions side-by-side on one slide |
| Selectable zone-layout presets | ❌ | VP "Apariencia" 3×3 grid of Header/Body/Footer arrangements |
| Camera input (OBS Virtual Camera supported) | ❌ | VP: live camera feed as content layer |
| Stage view — remote (browser, password-protected) | ❌ | VP HTTP server streams stage view to tablets/phones on LAN |
| Online catalog (downloadable Bibles + songbooks) | ❌ | VP: `Meta.json` catalogs available content; downloads to local store |
| Songbook grouping layer + style inheritance | ❌ | VP: songbooks → songs tree; 4-level style cascade (§4.1, §12). OA songs are a flat list. |

### P3 — Optional / low priority

| VP Feature | OA Status | Notes |
|---|---|---|
| PowerPoint / Word / Excel display (COM) | — | VP uses NetOffice COM interop |
| YouTube / Vimeo embedding (WebView) | — | VP: `AgendaWebSite` item type with URL |
| Auto-translation (Google Translate) | — | VP: translates UI string files in-app |
| CCLI SongSelect import | — | VP: `.usr` file format; `CCLISongImportFormat` |
| Backup / restore entire library | — | VP: `FormBackup` / `FormRestore` |

---

## 3. VP Data Model Patterns OA Should Know

### 3.1 Song Fields OA Is Missing

VP song has 25+ fields; OA currently stores: Title, Author, Classification, Copyright, CcliNumber, VerseOrder, Sections.

Still-missing VP fields worth adding to OA when relevant:

```
CCLINumber          — ✅ DONE (Song.CcliNumber + [SongCCLI] token)
Copyright           — ✅ DONE (Song.Copyright + [SongCopyright] token)
VerseOrder          — ✅ DONE (Song.VerseOrder + per-agenda override)
Key                 — musical key (e.g. "G", "Am")        ← would enable [SongKey] token
Capo                — capo fret number                     ← [SongCapo]
Tempo               — BPM                                  ← [SongTempo]
TimeSignature       — e.g. "4/4"                           ← [SongTimeSignature]
Reference           — scripture reference for the song     ← [SongReference]
Theme               — category/theme tag                   ← [SongTheme]
Memo1/Memo2/Memo3   — free custom fields
```

OA domain file: `OpenAdoration.Domain/Entities/Song.cs`

### 3.2 Section Types OA Is Missing

VP section tags (from live UI, Spanish labels → internal names):
```
Estrofa N   → Verse N     ✅ OA has SectionType.Verse
Coro N      → Chorus N    ✅ OA has SectionType.Chorus
Puente      → Bridge      ✅ OA has SectionType.Bridge
Pre-Coro    → Pre-Chorus  ✅ OA has SectionType.PreChorus
Introducción → Intro      ✅ OA has SectionType.Intro
Outro                     ✅ OA has SectionType.Outro
Tag                       ✅ OA has SectionType.Tag
```
OA section types already match VP's full set. No gap here.

### 3.3 Verse Order System (VP pattern for future OA)

VP per-agenda-item verse order:
- Songs store a default `VerseOrder` string: `"V1 C V2 C B C"`.
- Each `AgendaItem` can override this with a custom `VerseOrderIndex` pointing to a saved order preset.
- At projection time, VP resolves which section to show using the order string instead of the definition order in the file.

**✅ DONE.** OA implemented this exactly:
1. `Song.VerseOrder` (nullable string) — default order. ✅
2. `SongScheduleItem.VerseOrderOverride` — per-agenda override (VP's `VerseOrderIndex` equivalent). ✅
3. `Song.GetOrderedSections(string? verseOrderOverride = null)` parses the token string and maps to `SongSection` by `Type + SectionNumber`; falls back to definition order. ✅
4. Migrations `AddSongVerseOrder` + `20260529210146_AddSongScheduleItemVerseOrderOverride`. ✅

### 3.4 Header / Footer / Body Zone Layout (VP's projection layout system)

VP uses a **10,000 × 10,000 logical coordinate space** for all layout.
Three named zones per content type:
```
Header: x,y,width,height  (e.g. "200,200,9600,1000"  → top strip ~10% height)
Body:   x,y,width,height  (e.g. "200,1200,9600,7900" → main area ~79% height)
Footer: x,y,width,height  (e.g. "200,9100,9600,700"  → bottom strip ~7% height)
```

**✅ DONE.** `ProjectionWindow` is now a 3-row Grid (Header `Auto` / Body `*` Viewbox / Footer `Auto`).
`Theme.HeaderTemplate` + `Theme.FooterTemplate` hold token strings, resolved at render time via `ITokenResolver`.
Zones auto-hide when the resolved text has no letter/digit (G20). `CornerLabel` remains only as a fallback when no HeaderTemplate is set.
(VP's explicit zone *rects* / selectable layout presets are not replicated — WPF Grid layout is used instead. Zone-preset picker remains a P2 gap.)

### 3.5 VP Template Token System

VP renders tokens in header/footer text at display time. Full token list:

**Bible tokens:** `[BibleBookName]`, `[BibleChapterID]`, `[BibleVerseID]`, `[BibleDescription]`, `[SiteLicense]`

**Song tokens (selected):**
`[SongTitle]`, `[SongID]`, `[SongAuthor]`, `[SongCopyright]`, `[SongCCLI]`, `[SongKey]`,
`[SongCapo]`, `[SongTempo]`, `[SongTimeSignature]`, `[SongTheme]`, `[SongReference]`,
`[SongVerseTag]`, `[SongVerseID]`, `[SongSequence]`

**Songbook tokens:** `[SongBookTitle]`, `[SongBookAbbreviation]`, `[SongBookCopyright]`

**✅ DONE.** `ITokenResolver` → `TokenResolver` (Application) resolves `[Token]` against a `SlideContext` bag on the `Slide` DTO.
12 tokens implemented: church `[ChurchName]`/`[SiteLicense]` (from `IAppSettingsService`, app-wide), song `[SongTitle]`/`[SongAuthor]`/`[SongVerseTag]`/`[SongCopyright]`/`[SongCCLI]`, bible `[BibleBookName]`/`[BibleChapterID]`/`[BibleVerseID]`/`[BibleReference]`/`[BibleDescription]`.
`ProjectionWindow` and `StageViewModel` both call `Resolve()` before rendering.
Still missing vs VP's full list: `[SongID]`, `[SongKey]`, `[SongCapo]`, `[SongTempo]`, `[SongTimeSignature]`, `[SongTheme]`, `[SongReference]`, `[SongSequence]`, songbook tokens (those source fields don't exist on OA's `Song` yet).

---

## 4. VP UX Patterns Worth Replicating in OA

### 4.1 Style Inheritance Hierarchy (VP's "CSS cascade")

VP has 4 style levels (most-specific wins):
```
Base (root — all content) → Cancioneros (all songbooks) → Cancionero (one songbook) → Canto (one song)
```
OA currently: one Theme per slide (or default). No inheritance.

**Value:** Churches set a base style once, then override only what differs per song/section.
**OA path:** Add `ThemeId` FK to `SongBook` entity (when songbooks are added); resolver picks most-specific non-null theme.

### 4.2 Agenda Item Properties (VP's per-item flow control)

VP per agenda item:
```json
{
  "FlowType": 0,      // 0=manual, 1=auto-loop, 2=auto-advance
  "AutoAdvance": 0,   // bool
  "Interval": 5000,   // ms
  "VerseOrderIndex": -1
}
```
OA `ScheduleItem` has no flow/timing settings. Adding `FlowType` + `Interval` to OA's `ScheduleItems` table would enable timed auto-advance during live services.

### 4.3 Two-Step Song Search (VP's search UX)

VP searches:
1. Title/alias match first (fast index scan).
2. Lyrics full-text search only when title search returns zero results.

**✅ DONE.** OA implements the two-step pattern: `SearchByTitleAsync` (title/author LIKE) first, `SearchByLyricsAsync` (FTS5 over `SongSectionsFts`) as fallback when title returns zero results. Lyrics FTS uses per-word prefix matching (`cura*` matches `curará`).

### 4.4 Stage View / Presenter Monitor

VP `FormStageView` shows:
- Current displayed content
- Next slide preview
- Notes
- Clock / countdown timer

**✅ DONE (as embedded Stage View).** OA's `StageView` is an embedded nav page (not a separate window): left panel = current slide (themed 1920×1080 Viewbox), right panel = UP NEXT (incl. cross-item preview of the next schedule item's first slide), status bar with LIVE/STOPPED + Prev/Next Item buttons (shown only when a service schedule is live). Listens to `SlideChanged`/`ProjectionStateChanged`/`ThemeChanged`/`ServiceScheduleActiveChanged`/`NextScheduleItemPreviewChanged`.
Not done: notes panel, clock/countdown in the stage view, and the **remote (browser) stage view** below (§4.5).

### 4.5 Remote Stage View (VP's network feature)

VP has a password-protected HTTP/WebSocket server that streams the stage view to tablets/phones on the same LAN.

OA has no networking. This is a P2 feature but high-value for large churches with dedicated presenters.

**OA implementation path:** ASP.NET Core minimal API embedded in the host; SignalR hub pushes `SlideChanged` events as JSON to browser clients; a simple HTML/JS page renders the current + next slide content.

### 4.6 "Añadir" Add-to-Service UX Workflow

VP's library → agenda workflow is entirely right-click driven:

1. Operator finds a song or Bible passage in the library panel (Cancioneros or Biblias tab).
2. **Right-click** on the item → context menu → "Agregar al Orden De Culto" (Add to Service Order).
3. A submenu shows recent/open agendas so the item goes to the correct service.
4. The item appears at the bottom of the agenda panel immediately.

Alternatively, the library panel has a **"+ Añadir" button** (green plus) at the top right that adds the currently selected item.

VP does **not** use drag-and-drop from library to agenda for this primary workflow.

**OA current approach:** Per-section "Add Song" / "Add Bible" buttons inside the Service Schedule builder panel. This is functionally equivalent but less discoverable — the operator must navigate to the service first, then add.

**VP's advantage:** The operator can be browsing songs and add directly from the library without switching views.

---

## 5. VP Bible Browser UI Layout (confirmed from screenshots)

### 5.1 Layout Overview

VP's Bible browser is a horizontal layout inside the library panel:

```
┌─────────────────────────────────────────────────────────────────────┐
│  [📖 ASV] [📖 NIVUK2011] [📖 RVA2015] [📖 TLA]  ← version tabs      │
│  [Traducción en Lenguaje Actual (TLA)          ▼] ← version dropdown │
│  [Referencia a seleccionar, ej: Romanos 3:23-25] [+ Añadir ▼]        │
├────────────────────────┬──────┬──────────────────────────────────── │
│  OT Books (col 1)      │ Ch#  │  NT Books (col 2)          │ Ch#    │
│                        │      │                            │        │
│  Génesis  (orange)     │      │  ...                       │        │
│  Éxodo    (orange)     │      │  2 Tesalonicenses (blue)   │        │
│  ...                   │      │  1 Timoteo  (blue)         │        │
│  Josué    (green)      │      │  Tito       (blue)         │        │
│  ...                   │      │  Filemón    (blue)         │        │
│  Job      (gold)       │      │  Hebreos    (varied)       │        │
│  Salmos   (gold)       │      │  [Santiago] (green, bold)  │ 1      │
│  ...                   │      │  1 Pedro    (green)        │ 2      │
│  Isaías   (red/pink)   │      │  2 Pedro    (green)        │ 3      │
│  ...                   │      │  1-3 Juan   (green)        │ 4      │
│  Oseas    (varied)     │      │  Judas      (varied)       │ 5      │
│  ...                   │      │  Apocalipsis (gold/yellow) │        │
└────────────────────────┴──────┴────────────────────────────┴────────┘
```

**Two columns of books — OT on the left, NT on the right** — each independently scrollable.
The NT column in the screenshot is scrolled to show Santiago (James) selected.

**Center area** (main content, above library panel): "Versículo Bíblico" — read-only field showing the currently selected verse text.

**Bottom-right pane**: Live projection preview (see 5.5 below).

### 5.2 Bible Version Selection — Two Methods

VP provides two equivalent ways to switch the active Bible version:

1. **Tabs** (icon + abbreviation): One tab per installed Bible (e.g. ASV | NIVUK2011 | RVA2015 | TLA). Click a tab to switch.
2. **Dropdown** (below the tabs): Full Bible name shown (e.g. "Traducción en Lenguaje Actual (TLA)"). Useful when many versions are installed and tabs overflow.

Both controls are always visible and in sync.

Installed Bibles on analysis machine: ASV, NIVUK2011, RVA2015, TLA (4 versions).

### 5.3 Book List (two-pane: OT | NT)

- **Two independent scrollable columns**: OT books (39) on the left, NT books (27) on the right.
- Each book is a **color-coded row** by canonical group:

| Group | OT color | Group | NT color |
|---|---|---|---|
| Pentateuch | orange/amber | Gospels | (blue-ish) |
| Historical | green | Acts | (blue-ish) |
| Wisdom/Poetry | gold/yellow | Pauline Epistles | light blue/cyan |
| Major Prophets | red/pink | General Epistles | green shades |
| Minor Prophets | varied | Revelation | gold/yellow |

- **Selected book**: highlighted (bold + distinct color), e.g. "Santiago" shown in bold green.
- Clicking a book loads its chapter numbers in the adjacent chapter column.

### 5.4 Chapter Column (narrow, numbers only)

- A narrow column of plain numbers next to each book column.
- Only the selected book's chapter count is shown (e.g. Santiago/James = 1, 2, 3, 4, 5).
- Clicking a chapter number loads all verses of that chapter in the verse list pane (right panel).

### 5.5 Verse List (right pane — same as previously documented)

- Header: `[BookName] [Chapter]:[V1]-[Vn] ([BibleAbbr])` — e.g. "2 Peter 2:1-22 (NIVUK2011)"
- Numbered list of all verses with full text.
- **Single-click** a verse → highlights it + populates "Versículo Bíblico" field + projects immediately.

### 5.6 Bible Version Properties Panel (click on version → properties)

When clicking a Bible version (from version management, not the tab), a properties panel opens:

| Field | Value (TLA example) | Notes |
|---|---|---|
| Nombre | Traducción en Lenguaje Actual | Full name |
| Abreviatura | TLA | Short code shown on tab |
| Idioma | Español (México), es-MX | Language picker |
| Versificación | English | Versification scheme (dropdown) |
| ❤ Favorite Bible | ✅ checked | Favorite flag; affects display order |
| Mostrar nombres de Libros en Inglés | ☐ unchecked | Forces English book names even on non-English Bibles |
| Introducir Referencias por las Abreviaturas | ☐ unchecked | Allows entering refs via abbreviations |
| **Mostrar siempre en modo Dual (bilingüe)** | ☐ unchecked | **Bilingual mode** — shows two versions side by side simultaneously |
| Posición | 0 | Display order / tab position |
| Characters to filter | (empty) | Strip specific characters from verse text |
| Comprimido | ✅ (grayed) | Read-only; .vpc file is compressed |
| Documento | `C:\Users\Public\Documents\VideoPsalm\Bibles\Spanish Traducción en Lenguaje Actual (TLA).vpc` | Physical file path |
| GUID | opIDi7C3BUC9ML2QBM5Mgw | Unique identifier |
| Versión | 27/09/2015 12:00:00 a. m. | Content version date |

Tabs at bottom: Descripción | Editorial | Administrador | Copyright | Introducción (metadata fields).

**Notable feature — Bilingual/Dual mode:** VP can display two Bible versions side-by-side on the same slide. Not in OA.

### 5.7 Projected Bible Slide Layout (confirmed from preview pane)

```
┌─────────────────────────────────────────────────────────────────┐
│                                          2 Peter 2:11   🌀     │  ← Header (right-aligned)
│                                                                 │
│  yet even angels, although they are                            │
│  stronger and more powerful, do not heap                        │
│  abuse on such beings when bringing                            │
│  judgment on them from the Lord.                                │  ← Body (left-aligned, large)
│                                                                 │
│           New International Version Anglicised 2011             │  ← Footer (centered, small)
└─────────────────────────────────────────────────────────────────┘
```

- **Header** (top-right): `[BibleBookName] [BibleChapterID]:[BibleVerseID]` + decorative flourish icon
- **Body** (dominant area, left-aligned): verse text in large white font
- **Footer** (bottom strip, centered, smaller): full Bible version name (`[BibleDescription]` token)

### 5.8 Ribbon Tabs for Bible Context (4 tabs: Texto | Fondo | Mensaje | Edición)

When the Bible browser is active, the ribbon shows 4 context tabs. All confirmed from screenshots.

#### Tab 1 — Texto (Text)

| Group | Controls | Notes |
|---|---|---|
| Estilos | "Biblias" style-level dropdown + green sync button + A color | Style inheritance level selector |
| Fuente | Font family (Candara) + size (70) + A↑A↓ + B I U Ab + alignment + # chord | Same as song ribbon |
| **Slide layout** | **Max. verses: 1** + Max. characters: 0 | **Default = 1 verse per slide**; operator can increase |
| Colores | 5 preset "A" style buttons + paint brush dropdown | Quick text style presets |
| Animación | Transition effect buttons (4+ options) | Slide transition for Bible verses |

**Key:** "Max. verses: 1" means VP splits Bible content into one slide per verse by default. The operator can raise this to show multiple verses per slide.

#### Tab 2 — Fondo (Background)

| Group | Controls | Notes |
|---|---|---|
| Estilos | Biblias dropdown + sync + A | Style level selector |
| Color | Color swatches | Solid color background |
| Imagen | Thumbnail image presets (landscape/artistic) | Background image picker |
| Video | Dark video preset thumbnails | Background video picker |
| Brillo | Yellow dot + slider | Brightness control for background media |
| Animación | Transition buttons | Same as Texto tab |
| **Aspecto** | **Actual \| 4:3:2 \| 4:3 \| 16:10 \| 16:9 \| 16:8** | **Aspect ratio presets for the output screen** |

**Key:** Aspect ratio group lets operator match the projection to the physical screen (standard/widescreen/ultrawide). OA does not have explicit aspect ratio control.

#### Tab 3 — Mensaje (Message / Live Overlay)

| Group | Controls | Notes |
|---|---|---|
| Mensaje | Text dropdown + Mostrar + Ajustes | Push a live text message/announcement to screen |
| Pantalla de esc... | Display + Ajustes | Screen/display settings |
| **Reloj** | 3 clock face presets (cyan/red/dark) + Reloj ▼ (Cuenta regresiva / Cronómetro) + Mostrar + Ajustes | **Clock overlay — 3 styles, 3 modes (Clock/Countdown/Stopwatch)** |
| Transparencia | Yellow dot + slider | Overall overlay transparency |

**Key:** The clock overlay is accessed from the Mensaje tab (not a separate menu). Three visual styles (cyan analog, red analog, dark/black analog). Three operational modes: Reloj (clock), Cuenta regresiva (countdown), Cronómetro (stopwatch).

#### Tab 4 — Edición (Edit / Management)

| Group | Controls | Notes |
|---|---|---|
| Cancioneros | Guardar (Save) + Borrar Biblia (🗑 Delete Bible) | Manage the current Bible version |
| **Apariencia** | 3×3 grid of layout preset icons (↙ dialog launcher) | **Slide zone layout presets** — different arrangements of Header/Body/Footer zones |
| Herramientas | Importar + Guardar + Restaurar + Ajustes | Import new Bible; save/restore; settings |
| Ayuda | Ayuda (F1) + Acerca de... | Help and about |

**Key:** The Apariencia group (Appearance) shows preset layout buttons — approximately 9 icons in a 3×3 grid, each representing a different zone arrangement (header visible/hidden, footer visible/hidden, body position). This confirms that VP's Header/Body/Footer zone layout is not one fixed template but a set of selectable presets.

### 5.9 VP Bible Search Modes

VP exposes a `BibleSearchMode` enum with at least three modes, accessible from the search bar in the Biblias tab:

| Mode | Behavior |
|---|---|
| **Phrase** | Finds exact phrase match across all verses in the selected version |
| **Word** | Finds verses containing all specified words (order-independent) |
| **Strong's** | Searches by Strong's concordance number (H1234 / G5678) — Hebrew/Greek lexicon lookup |

**✅ Keyword + Phrase DONE.** `IBibleService.SearchAsync` takes a `BibleSearchMode { Keyword, Phrase }`. Keyword = all words, per-word prefix, implicit AND; Phrase = exact FTS5 quoted phrase. Bible browser has a `" " Phrase` toggle (keyword mode only).

**Strong's (not done):** requires the Bible version to include Strong's markup in the verse text — most common formats (Zefania, OSIS) support this optionally.

### 5.10 Key Differences from OA's Current Bible Browser

| Aspect | VP | OA (current) |
|---|---|---|
| Book display | **2 columns: OT left, NT right** (each scrollable) | Single list grouped by Testament (CollectionViewSource) — minor UX gap |
| Chapter display | Plain number list beside selected book | 38×38 tile grid (WrapPanel) — equivalent |
| Version selection | Tabs + dropdown (both always visible) | Tabs only (per installed version) — minor gap |
| Verse per slide | **Configurable (default: 1)** | ✅ configurable (`DefaultBibleVersesPerSlide`) |
| Slide footer | Bible version name (`[BibleDescription]`) | ✅ via FooterTemplate + `[BibleDescription]` token |
| Slide header | Right-aligned ref + decorative icon | ✅ via HeaderTemplate + `[BibleReference]` token (no decorative icon) |
| Aspect ratio | Selectable (Actual/4:3/16:9/16:10/16:8) | ❌ Not implemented |
| Clock overlay | Built-in (Mensaje tab, 3 styles, 3 modes) | ❌ Not implemented |
| Bilingual mode | Dual side-by-side display | ❌ Not implemented |
| Slide zone presets | 9 layout presets (Apariencia group) | ❌ Not implemented (fixed 3-zone Grid) |
| Brightness control | Slider on background media | ❌ Not implemented |

---

## 6. VP Song Browser UI Layout (confirmed from live screenshots)

### 6.1 Cancioneros Tab — Songbook Tree

```
┌──────────────────────────────────────────────────────┐
│ [🎵 Cancioneros]  [📖 Biblias]           [+ Añadir ▼] │
│ [🔍▼ Palabras (título y letra) o número a buscar...] │
├──────────────────────────────────────────────────────┤
│ ▶ 📚 A New Song (461 Cantos)                          │
│ ▶ 📚 Chorus (251 Cantos)                              │
│ ▼ 📗 Español - Himnario (764 Cantos)                  │
│   ♫  1  Nombre del canto...                          │
│   ♫  2  ...                                          │
│ ▶ 📚 Himnario Bautista CBP (530 Cantos)               │
└──────────────────────────────────────────────────────┘
```

- **Search box**: magnifier icon + dropdown arrow + text field. Searches titles, lyrics, and song numbers simultaneously.
- **Two-step search logic**: title/alias match first (fast); lyrics FTS only when title returns zero results.
- **Songbook node**: ▶ expand triangle + 📗 icon + "Name (N Cantos)". Click to expand/collapse.
- **Song node**: ♫ icon + number + title. Single-click selects; double-click adds to agenda / opens editor.

Confirmed installed songbooks: A New Song (461, en-AU), Chorus (251, en), Español - Himnario (764, es), Himnario Bautista CBP (530, es).

### 6.2 Song Item Right-Click Context Menu (13 items)

1. Agregar al Orden De Culto — Add to Service Order
2. Recent agendas ▶ (submenu)
3. ──────────────────
4. Nuevo Canto (Ctrl+N) — New Song
5. Nuevo Cancionero — New Songbook
6. Download a predefined songbook
7. ──────────────────
8. Borrar Canto — Delete Song
9. Mover Canto a ▶ — Move Song to another songbook
10. Copiar Canto a ▶ — Copy Song to another songbook
11. Conjunto Cancioneros ▶ — Songbook collection/grouping
12. ──────────────────
13. Verse order — Reorder song sections for this agenda item

### 6.3 Song Editor Panel (right panel when song selected)

Header: **♫ Canto**

**Tabs:**

| Tab | Contents |
|---|---|
| Letras | Lyrics editor — main editing area |
| Archivos | Attached media files (audio, video, PDF, PPT, Word) |
| Acordes | Chord notation settings |
| Varios | Miscellaneous metadata (key, tempo, CCLI, etc.) |

**Letras tab layout:**
- **Número**: hymnal number spinner (song position in book)
- **Título**: text field (song title)
- **Lyrics editor** (rich text): section tags + lyrics content

**Lyrics editor toolbar:**
`#b` (chord toggle) | `Ab` (all-caps) | Font family | A↑ A↓ | Color (A▼) | **B I U** | `abc` (small caps) | `¶` | additional formatting

### 6.4 Section Tag Format in the Lyrics Editor

VP's lyrics editor shows section tags as **colored label lines** directly in the text, not as separate UI elements:

```
[red bg]   Coro 1          ← DEFINITION — lyrics for this section follow
           Be to our God
           Forever and ever
           ...

[red bg]   Estrofa 1       ← DEFINITION
           Salvation belongs to our God
           ...

[gray bg]  Coro 1          ← REPEAT REFERENCE — no lyrics; VP plays the Coro 1 section again
[red bg]   Estrofa 2       ← DEFINITION
           And we the redeemed...
```

**Color coding rules:**
- **Red/pink background**: section DEFINITION — the canonical lyrics for that section live here.
- **Gray background**: section REPEAT/REFERENCE — the tag appears again in the flow but the lyrics above already defined it. VP renders the section's lyrics at every occurrence during playback.

**Section tag vocabulary** (Spanish labels used in VP, confirmed from live UI):

| Spanish label | Internal type | OA equivalent |
|---|---|---|
| Estrofa N | Verse N | `SectionType.Verse` |
| Coro N | Chorus N | `SectionType.Chorus` |
| Puente | Bridge | `SectionType.Bridge` |
| Pre-Coro | Pre-Chorus | `SectionType.PreChorus` |
| Introducción | Intro | `SectionType.Intro` |
| Outro | Outro | `SectionType.Outro` |
| Tag | Tag | `SectionType.Tag` |

OA's section types fully match VP's set — no gap.

**Key insight for OA:** OA currently has no visual distinction between a section definition and a repeat reference in the song editor. VP's red/gray color coding makes the verse-order system immediately visible to the operator. When OA implements `VerseOrder`, adding this visual distinction to `AddEditSongView.xaml` would be high value.

### 6.5 Song Contextual Ribbon (appears when song editor is active)

| Group | Controls |
|---|---|
| Cancioneros | **Ordenar por Canto** (sort by number) \| **Ordenar por Título** (sort by title) |
| Apariencia | Layout preset buttons (↙ dialog launcher) — same zone-arrangement presets as Bible |
| (actions) | **Importar** \| **Guardar ▼** \| **Restaurar ▼** \| **Herramientas** \| **Apuntes** (song notes) \| **Ayuda (F1)** |

### 6.6 Projected Song Slide Layout

VP applies the same 3-zone layout to songs as to Bible verses:

```
┌─────────────────────────────────────────────────────────┐
│  Amazing Grace                              Coro 1      │  ← Header ([SongTitle]  [SongVerseTag])
│                                                         │
│  Amazing grace! How sweet the sound        │
│  That saved a wretch like me               │  ← Body (lyrics, large text)
│  I once was lost, but now am found         │
│  Was blind, but now I see                  │
│                                                         │
│  HB-CBP  #45                    © 2001 Church Songs     │  ← Footer ([SongBookAbbreviation] [SongID] [SongCopyright])
└─────────────────────────────────────────────────────────┘
```

Header and footer content is fully configurable via template tokens (see Section 3.5 for full token list).

### 6.7 Key Differences from OA's Current Song Browser

| Aspect | VP | OA (current) |
|---|---|---|
| Song list | Tree (songbooks → songs, expandable) | Flat list, searchable — songbook layer not built |
| Search scope | Title + lyrics simultaneously (two-step) | ✅ two-step (title/author then lyrics FTS) |
| Section tag display | Red (definition) / gray (repeat) color coding | ✅ color-coded token badges (V1/C/B…) per section + editable Play Order field |
| Verse order | Configurable per song and per agenda item | ✅ both `Song.VerseOrder` + `SongScheduleItem.VerseOrderOverride` |
| Chord display | Dedicated tab + inline in lyrics editor | ❌ Not implemented |
| Attached media | Per-song audio/video/PDF/PPT files | ❌ Not implemented |
| Song notes (Apuntes) | Per-song notes field | ❌ Not implemented |
| Songbook organization | Hierarchical (songbooks → songs) | ❌ Flat (all songs in one list) |
| Add to service | Right-click → context menu from any view | Navigate to Schedule → add from picker (functional, less discoverable) |
| Import | OpenLyrics ✅ + 20 more | 🔶 OpenLyrics only; OpenSong/plain-text planned |

---

## 7. VP's Projection Window Layer Stack vs OA

VP's output screen layers (bottom to top):
```
[Background color fill]
[Background image / video]
[Body text zone]
[Header text zone]
[Footer text zone]
[Clock overlay (optional)]
[Camera feed (optional)]
```

OA's `ProjectionWindow` layers (bottom to top, from `ProjectionWindow.xaml`):
```
ThemeBackground (solid color Rectangle)
ThemeBackgroundImage (BitmapImage)
ThemeBackgroundVideo (MediaElement, muted, looping)
BackgroundImage (media slide image — BitmapImage)
ContentVideo (media slide video — MediaElement, with audio)
3-zone Grid: Header (Auto) / Body Viewbox (*) / Footer (Auto)  ← token-resolved, auto-hiding
BlankOverlay (opaque black Rectangle)
CornerLabel (fallback only — used when no HeaderTemplate set, ZIndex=100)
```

**✅ Header/Body/Footer zones DONE.** Remaining layer gaps vs VP: clock overlay and camera feed (both top-of-stack overlays, not yet built). A live-announcement overlay is being added (P2-a).

---

## 8. VP File Formats OA Does Not Use

| VP Format | Extension | Notes |
|---|---|---|
| Bible/SongBook content | `.vpc` | AES-256 encrypted ZIP + JSON inside. Cannot be read without password. |
| Agenda (service file) | `.vpagd` | Standard ZIP + JSON components (one file per agenda item + style files). Could be an export target for OA. |
| Settings | `.vpcsetting` | Encrypted ZIP. Internal use only. |
| Open Bible (import-only) | `.sqlite` | `meta` + `verses` tables. OA already handles this via `BibleSuperSearchSqliteParser`. |

**OA native Bible format (OpenAdoration JSON):**
```json
{
  "name": "Version Name",
  "abbreviation": "ABR",
  "language": "en",
  "books": [{
    "name": "Genesis",
    "chapters": [{
      "number": 1,
      "verses": [{"number": 1, "text": "In the beginning..."}]
    }]
  }]
}
```
Parser: `OpenADorationJsonParser.cs`

---

## 9. VP Song Import Formats OA Should Consider

OA imports **OpenLyrics XML** (`OpenLyricsParser.cs` — title/author/copyright/ccliNo/verseOrder/sections). VP imports from 20+ formats. Remaining priority order for OA:

| Priority | Format | Why |
|---|---|---|
| ✅ Done | OpenLyrics XML | Open standard, widely supported by OpenLP, OpenSong, WorshipAssistant |
| High | CCLI SongSelect `.usr` | Most churches already have CCLI accounts |
| Medium | OpenSong | Popular free alternative |
| Medium | EasyWorship | Common in US churches |
| Low | Plain text | Easy fallback for manual copy-paste |
| Low | ChordPro | For chord-focused congregations |

**OpenLyrics XML structure (for implementing a parser):**
```xml
<song xmlns="http://openlyrics.info/namespace/2009/song" version="0.8">
  <properties>
    <titles><title>Song Title</title></titles>
    <authors><author>Author Name</author></authors>
    <copyright>Copyright text</copyright>
    <ccliNo>12345</ccliNo>
    <verseOrder>v1 c v2 c</verseOrder>
  </properties>
  <lyrics>
    <verse name="v1"><lines>Line 1<br/>Line 2</lines></verse>
    <verse name="c"><lines>Chorus line</lines></verse>
  </lyrics>
</song>
```

OA `SectionType` mapping from OpenLyrics `verse.name`:
- `v1`, `v2`, ... → `SectionType.Verse` (SectionNumber = 1, 2, ...)
- `c`, `c1`, `c2` → `SectionType.Chorus`
- `b`, `b1` → `SectionType.Bridge`
- `p`, `p1` → `SectionType.PreChorus`
- `i` → `SectionType.Intro`
- `e` → `SectionType.Outro`
- `t` → `SectionType.Tag`

---

## 10. VP Bible Import Formats OA Already Supports

OA `BibleFormatDispatcher` handles all 8 formats tested against VP's catalog:

| Format | OA Parser | Test |
|---|---|---|
| Zefania XML | `ZefaniaXmlParser` | ✅ |
| OSIS XML | `OsisXmlParser` | ✅ |
| USFX XML | `UsfxXmlParser` | ✅ |
| thiagobodruk JSON | `ThiagobodrukJsonParser` | ✅ |
| OpenAdoration JSON | `OpenADorationJsonParser` | ✅ |
| BibleSuperSearch JSON | `BibleSuperSearchJsonParser` | ✅ |
| BibleSuperSearch ZIP | `BibleSuperSearchZipParser` | ✅ |
| BibleSuperSearch SQLite | `BibleSuperSearchSqliteParser` | ✅ |

VP also supports: e-Sword, The Unbound Bible, TheWord, EasySlides, OpenLP.
None of these are currently in OA.

---

## 11. VP Coordinate System vs OA Layout

VP renders in a **10,000 × 10,000 logical unit space** (resolution-independent).
All rects are `"x,y,width,height"` strings in that space.

OA uses WPF's native layout system (Grid, Viewbox, DockPanel), which is already resolution-independent via WPF's device-independent pixel system. No need to replicate VP's coordinate system — WPF's layout is equivalent and more idiomatic.

---

## 12. VP Songbook Structure and Flags

VP's songbook layer sits above individual songs. OA currently has no songbook entity — all songs are in a flat list. This section is forward-looking for when OA adds a songbook grouping layer.

### 12.1 Songbook Entity Fields

```
SongBook {
  Id (GUID — globally unique)
  Title
  Abbreviation            — short code shown in UI (e.g. "HB-CBP")
  Author
  Administrator           — admin contact
  Copyright
  Publisher
  Language                — e.g. "es-MX"
  Color                   — display color in the UI tree
  SongCount               — derived (not stored)
  FilePath                — path to the .vpc file
}
```

Metadata tabs on the songbook properties panel: Descripción | Editorial | Autor | Administrador | Copyright

### 12.2 Songbook Flags (behavior flags, not metadata)

| Flag | Type | Effect |
|---|---|---|
| `IsBibleVerseCollection` | bool | Songbook contains Bible verses formatted as "songs" — allows mixing scripture and hymns in one flow |
| `IsCCLISongBook` | bool | Imported from CCLI SongSelect; affects CCLI reporting |
| `IsCompressed` | bool | Content is ZIP-compressed inside the .vpc file |
| `IsProtected` | bool | Content is AES-256 encrypted (requires password to read) |
| `IsSearchable` | bool | Whether this songbook is included in global song search |
| `ViewBracketContentAsChord` | bool | Treats `[text]` in lyrics as chord names (ChordPro bracket style) |

**For OA:** When adding a `SongBook` entity, `IsSearchable` and `ViewBracketContentAsChord` are the two most immediately useful flags. `IsBibleVerseCollection` is a niche but interesting feature for churches that want to interleave scripture as song items.

### 12.3 SongBook Storage Path (confirmed)

`C:\Users\Public\Documents\VideoPsalm\SongBooks\`

Each songbook is stored as a `.vpc` file (AES-256 encrypted ZIP containing a JSON file named `SongBook_{GUID}.json`).

### 12.4 SongWords.db — Full-Text Search Index

VP builds a separate SQLite database at runtime:

- **Purpose**: FTS index over all song lyrics across all searchable songbooks.
- **Used for**: The lyrics fallback in the two-step song search (step 2 — see Section 4.3).
- **Not the song data itself** — the actual song content lives in the encrypted `.vpc` files.

**For OA:** When OA adds lyrics search, use an FTS5 virtual table on `SongSections.Lyrics` (same pattern as `BibleVersesFts` in migration `20260520041713_AddBibleVersesFts`). No separate database needed.

---

## 13. Key VP Observations for OA Design Decisions

1. **VP is fully offline (like OA)** — no accounts, no mandatory cloud sync. This confirms OA's design is correct.

2. **VP's "relaxed JSON" (JSON5-like)** — VP uses unquoted keys in settings files. OA uses `System.Text.Json` with standard JSON — no change needed; this is an OA advantage (strict parsing).

3. **VP's 32-bit build** — VP is x86 because of legacy dependencies (Ionic.Zip, DevExpress, COM interop). OA targets `net10.0-windows` x64 by default — this is fine and an advantage.

4. **VP uses DevExpress 18.1 (licensed, 2018-era)** — OA uses open-source WPF toolkit + CommunityToolkit.Mvvm. OA's stack is more modern and fully free/open-source.

5. **VP's agenda files are portable** (`.vpagd` ZIP embeds background images). OA's service schedule references media by absolute path — this means services are not portable between machines. If portability is needed, OA should copy media into the agenda file on export (same pattern as VP).

6. **VP's song section model uses plain-text tags as section identifiers** in the song file. OA's model uses `SectionType` enum + `SectionNumber` int — this is more structured and type-safe than VP's approach.
