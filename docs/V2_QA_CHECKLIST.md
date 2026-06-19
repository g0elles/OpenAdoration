# OpenAdoration — v2.0 "Big Test" QA Checklist

The integration pass from [V2_RELEASE_GATE.md](V2_RELEASE_GATE.md) Gate 2. Walk it top to bottom.
Mark each item and jot findings in the **Notes** block under each area — that's the organized feedback
loop. We triage the notes afterward (fix now vs defer vs won't-fix).

**Legend:** `[x]` pass · `[!]` works-but-issue · `[ ]` not yet / broken (say which in Notes) · `[-]` n/a
**For every issue, note:** what you did → what you expected → what happened.

> Run the cross-feature **§A scenarios first** — they're where out-of-order delivery hides bugs.
> Everything below §A is feature-by-feature coverage.

---

## §A — Cross-feature integration scenarios (highest priority)

- [x] **A1. Stacked live service:** start a service that contains a song → a dual-version scripture →
      a media (HEVC video) item, with auto-advance on. Navigate slide-by-slide and let auto-advance
      cross item boundaries. Expect: no stalls, correct order, no stale/blank slides.
- [x] **A2. Overlays together:** while a song slide with a **video background theme** is live, show an
      **announcement** AND a **lower-third** at the same time. Expect: both visible, lyrics still readable,
      announcement auto-dismisses, lower-third persists until cleared.
- [x] **A3. Transition × content:** set transition to Fade, then Slide, then Zoom; project songs and
      scripture with each. Expect: smooth, no flicker, background doesn't jump.
- [-] **A4. Dual-version scrutiny:** project dual-version scripture (primary + secondary). **Re-check the
      minor bugs flagged earlier** here. Note anything off (alignment, pairing, missing secondary verse).
- [x] **A5. VideoPsalm end-to-end:** import a `.vpagd`, then run that service live — songs, scripture
      (ref-only), media all project; summary said Bible text omitted. Expect: order matches VideoPsalm.
- [!] **A6. Language mid-session:** with a service live, switch language en↔es in Settings. Expect:
      UI re-renders in the new language, projection unaffected, nothing half-translated.
- [x] **A7. Live parking:** start a live service, navigate to Songs/Bible/Themes and back. Expect:
      live state preserved, projection never interrupted, returns to the same live item.

**Notes (§A):**
> A dual version scripture cannot be added to the service, mmm you know what, the dual version scripture can go; the blank field in spanish it says Negro... that doesn't make sense it should be limpiar, the theme swap doesn't work, if the default time is changed it should be reflected on the slides or not?... evaluate that



---

## §B — Songs
- [x] New / edit / delete; validation (title required, ≥1 section, all sections have lyrics).
- [x] Search by title/author; lyrics keyword (FTS) search.
- [x] Section editor: add all types, reorder ▲▼, delete; play-order field + "shows N of M" hint.
- [x] Import each: OpenLyrics XML, OpenSong, plain text, ChordPro `.cho`, VideoPsalm `.vpagd`.
- [x] Project a song; Space/arrows move through sections; verse-order respected.

**Notes (§B):**
> 

---

## §C — Bible
- [x] Import a Bible (Zefania / OSIS / USFX / JSON): progress bar, summary, books resolve.
- [x] Browse books → chapters → verses; multi-verse select with Ctrl+↓/↑.
- [ ] Reference bar parse ("John 3:16-18"); keyword search; `" "` phrase toggle.
- [x] Project verses; verses-per-slide setting honored.
- [-] Secondary version picker (dual) + ✕ clear; degrades cleanly if book names mismatch.
- [x] `.vpc` import → clear "encrypted, can't import" refusal (no crash).

**Notes (§C):**
> Project verses don't work properly, secondary version picker don't convence me to keep this feature, 

---

## §D — Themes
- [x] CRUD; DEFAULT badge; set default (★); default theme can't be deleted (clear message).
- [x] Editor: font family/size ±, font color, alignment L/C/R.
- [x] Background color / image (browse, clear) / video; live preview reflects each.
- [x] Header/Footer templates: type text, insert token chips; zones auto-hide when token is empty.

**Notes (§D):**
> 

---

## §E — Media
- [x] Import file(s); import folder; unsupported/oversized skipped with a count message.
- [x] Project an image.
- [x] Project a video **incl. HEVC/.MOV** (FFME): plays with audio; transport restart / −10 / play-pause / +10 / progress all work.

**Notes (§E):**
> 

---

## §F — Service Schedule (builder)
- [x] Create service (name + date); add song / bible / media; reorder ▲▼; delete.
- [x] Auto-advance −/+ per item persists; verse-order override per song item.
- [x] VideoPsalm single import + folder import; summary dialog states Bible text omitted.
- [x] Unresolved scripture shows ⚠ + "Select from Bible…" → replaces in place, keeps position.

**Notes (§F):**
> 

---

## §G — Live mode & projection bar
- [x] Start service; item list highlights current; click to jump; Prev/Next item; "Item X of Y" counter.
- [x] Add song/bible/media to the queue on the fly.
- [x] Open/Close screen; correct monitor (multi-monitor); Blank; Stop.
- [!] Keyboard: Space/→/PgDn next, ←/PgUp prev, B blank, Esc stop, 1–9 go-to, Ctrl+1–5 nav.
- [x] Announcement (auto-dismiss) and Lower-third (persistent + Clear).

**Notes (§G):**
> Ctrl+1–5 nav, space and  1–9 go-to don't work, →/PgDn next, ←/PgUp prev, B blank, Esc stop yes but only if the user click on the bottom bat, if the user clears the announcement it should clear the text, it applies to lower third

---

## §H — Stage View
- [x] Current slide + UP NEXT previews render with theme layers; LIVE/STOPPED badge.
- [!] Prev/Next item visible only when a service is live; video preview shows.

**Notes (§H):**
> when I'm on the stage view if I pause a video on the stage view the video is still playing

---

## §I — Settings
- [x] Church name / CCLI tokens resolve in projected header/footer.
- [x] Defaults: auto-advance, verses/slide, announcement duration, transition ms — all apply.
- [!] Language dropdown switches en↔es live.
- [x] Backup create → restore (app restarts, library intact).
- [x] Check for updates (no update → "latest version"; behaves offline).

**Notes (§I):**
> If the user change the languaje it changes nice, but if the user doesn't save it the ui will be in the idiom, if the user plans to leave the settings section with pending changes it shouldn't come out of the menu without saving

---

## §J — Plugins
- [x] Add `.oaplugin` → loads live; edit settings; Fetch versions; Import a version; Remove (restart).

**Notes (§J):**
> 

---

## §K — Full Spanish pass (flag is ON)
- [!] Switch to Español and revisit **every** view, dialog, error/confirmation, and import summary.
      Flag anything still in English or awkwardly translated.

**Notes (§K):**
> I leaved a comment already

---

## §L — Safety / edge
- [x] Existing DB still opens after this build (migration ran; `.oabak.auto` snapshot appears on a schema change).
- [x] Corrupt / unsupported import files fail gracefully (no crash, clear message).
- [x] Backup restore on a second Windows profile/machine.

**Notes (§L):**
> 

---

## Sign-off
- Tester: Gabri Elles  Date: 2026-06-18  Build/commit: `dev` (post-QA fixes)
- Verdict: [x] ship  ·  [ ] ship after fixes (list below)  ·  [ ] not ready  — **PASSED** after the fixes below were applied and re-verified.
- Must-fix before v2.0 — ALL FIXED this pass:
  1. Keyboard shortcuts only worked when the projection bar had focus → tunneling `OnPreviewKeyDown`.
  2. Clear announcement/lower-third left text in the box → also clears the field.
  3. Stage preview ignored pause → mirrors `MediaTransport.IsPlaying`.
  4. Spanish "Negro" → "Limpiar".
  5. Bible search confusing (reference/keyword/phrase) + reference bar buggy → one smart box.
  6. Default-theme change didn't refresh projection → fires `NotifyThemeChanged`.
  7. Dual-version scripture confusing → removed (ROADMAP M10.3 dropped).
  8. Settings unsaved language change → prompt-on-leave.
  9. Spanish text overflow (field placeholders, theme rows, sidebar) → trimming + layout/width fixes.
- Defer (post-v2.0):
  1. ChordPro missing from the song-import tooltip/error format string (both languages) — cosmetic.
  2. Two-zone theme layout (only if a church requests dual scripture).
