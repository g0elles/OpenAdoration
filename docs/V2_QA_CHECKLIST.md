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

- [ ] **A1. Stacked live service:** start a service that contains a song → a dual-version scripture →
      a media (HEVC video) item, with auto-advance on. Navigate slide-by-slide and let auto-advance
      cross item boundaries. Expect: no stalls, correct order, no stale/blank slides.
- [ ] **A2. Overlays together:** while a song slide with a **video background theme** is live, show an
      **announcement** AND a **lower-third** at the same time. Expect: both visible, lyrics still readable,
      announcement auto-dismisses, lower-third persists until cleared.
- [ ] **A3. Transition × content:** set transition to Fade, then Slide, then Zoom; project songs and
      scripture with each. Expect: smooth, no flicker, background doesn't jump.
- [ ] **A4. Dual-version scrutiny:** project dual-version scripture (primary + secondary). **Re-check the
      minor bugs flagged earlier** here. Note anything off (alignment, pairing, missing secondary verse).
- [ ] **A5. VideoPsalm end-to-end:** import a `.vpagd`, then run that service live — songs, scripture
      (ref-only), media all project; summary said Bible text omitted. Expect: order matches VideoPsalm.
- [ ] **A6. Language mid-session:** with a service live, switch language en↔es in Settings. Expect:
      UI re-renders in the new language, projection unaffected, nothing half-translated.
- [ ] **A7. Live parking:** start a live service, navigate to Songs/Bible/Themes and back. Expect:
      live state preserved, projection never interrupted, returns to the same live item.

**Notes (§A):**
> 

---

## §B — Songs
- [ ] New / edit / delete; validation (title required, ≥1 section, all sections have lyrics).
- [ ] Search by title/author; lyrics keyword (FTS) search.
- [ ] Section editor: add all types, reorder ▲▼, delete; play-order field + "shows N of M" hint.
- [ ] Import each: OpenLyrics XML, OpenSong, plain text, ChordPro `.cho`, VideoPsalm `.vpagd`.
- [ ] Project a song; Space/arrows move through sections; verse-order respected.

**Notes (§B):**
> 

---

## §C — Bible
- [ ] Import a Bible (Zefania / OSIS / USFX / JSON): progress bar, summary, books resolve.
- [ ] Browse books → chapters → verses; multi-verse select with Ctrl+↓/↑.
- [ ] Reference bar parse ("John 3:16-18"); keyword search; `" "` phrase toggle.
- [ ] Project verses; verses-per-slide setting honored.
- [ ] Secondary version picker (dual) + ✕ clear; degrades cleanly if book names mismatch.
- [ ] `.vpc` import → clear "encrypted, can't import" refusal (no crash).

**Notes (§C):**
> 

---

## §D — Themes
- [ ] CRUD; DEFAULT badge; set default (★); default theme can't be deleted (clear message).
- [ ] Editor: font family/size ±, font color, alignment L/C/R.
- [ ] Background color / image (browse, clear) / video; live preview reflects each.
- [ ] Header/Footer templates: type text, insert token chips; zones auto-hide when token is empty.

**Notes (§D):**
> 

---

## §E — Media
- [ ] Import file(s); import folder; unsupported/oversized skipped with a count message.
- [ ] Project an image.
- [ ] Project a video **incl. HEVC/.MOV** (FFME): plays with audio; transport restart / −10 / play-pause / +10 / progress all work.

**Notes (§E):**
> 

---

## §F — Service Schedule (builder)
- [ ] Create service (name + date); add song / bible / media; reorder ▲▼; delete.
- [ ] Auto-advance −/+ per item persists; verse-order override per song item.
- [ ] VideoPsalm single import + folder import; summary dialog states Bible text omitted.
- [ ] Unresolved scripture shows ⚠ + "Select from Bible…" → replaces in place, keeps position.

**Notes (§F):**
> 

---

## §G — Live mode & projection bar
- [ ] Start service; item list highlights current; click to jump; Prev/Next item; "Item X of Y" counter.
- [ ] Add song/bible/media to the queue on the fly.
- [ ] Open/Close screen; correct monitor (multi-monitor); Blank; Stop.
- [ ] Keyboard: Space/→/PgDn next, ←/PgUp prev, B blank, Esc stop, 1–9 go-to, Ctrl+1–5 nav.
- [ ] Announcement (auto-dismiss) and Lower-third (persistent + Clear).

**Notes (§G):**
> 

---

## §H — Stage View
- [ ] Current slide + UP NEXT previews render with theme layers; LIVE/STOPPED badge.
- [ ] Prev/Next item visible only when a service is live; video preview shows.

**Notes (§H):**
> 

---

## §I — Settings
- [ ] Church name / CCLI tokens resolve in projected header/footer.
- [ ] Defaults: auto-advance, verses/slide, announcement duration, transition ms — all apply.
- [ ] Language dropdown switches en↔es live.
- [ ] Backup create → restore (app restarts, library intact).
- [ ] Check for updates (no update → "latest version"; behaves offline).

**Notes (§I):**
> 

---

## §J — Plugins
- [ ] Add `.oaplugin` → loads live; edit settings; Fetch versions; Import a version; Remove (restart).

**Notes (§J):**
> 

---

## §K — Full Spanish pass (flag is ON)
- [ ] Switch to Español and revisit **every** view, dialog, error/confirmation, and import summary.
      Flag anything still in English or awkwardly translated.

**Notes (§K):**
> 

---

## §L — Safety / edge
- [ ] Existing DB still opens after this build (migration ran; `.oabak.auto` snapshot appears on a schema change).
- [ ] Corrupt / unsupported import files fail gracefully (no crash, clear message).
- [ ] Backup restore on a second Windows profile/machine.

**Notes (§L):**
> 

---

## Sign-off
- Tester: ____________  Date: ____________  Build/commit: ____________
- Verdict: [ ] ship  ·  [ ] ship after fixes (list below)  ·  [ ] not ready
- Must-fix before v2.0:
  1. 
  2. 
- Defer (post-v2.0):
  1. 
