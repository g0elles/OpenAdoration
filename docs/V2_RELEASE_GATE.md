# OpenAdoration — v2.0 Release Gate

> **✅ v2.0 shipped 2026-06-19** (tag `v2.0.0`, GitHub release + MSI published). This document is
> retained as the **historical record** of the gate that governed the cut — it is not a live checklist.
> The Gate 2 "Big Test" was run as the manual QA pass (see `V2_QA_CHECKLIST.md`); a couple of items
> below stayed unchecked because the feature was **dropped** (dual-version scripture) or **deferred to
> backlog** (clean output, VC++ check) rather than blocking the release.

A pre-release checklist for cutting v2.0 from `dev`. Derived from the engineering-lead
review (out-of-order execution risk) plus a code-verification pass on 2026-06-17.

The point: features have been delivered **out of order** (M12/M13/M10.5 pulled forward,
M8.2/M9/M10/M11.3 backfilled), so many were built in isolation and have **never been
exercised together**. These gates exist to catch what that hides. Work the gates top to
bottom; don't start a new milestone until they pass.

---

## Gate 1 — Reconcile before RC
- [x] `ROADMAP.md` top progress table realigned to `dev`'s actual state (the v2.0 snapshot table is
      current as of 2026-06-19).
- [x] `SESSION_STATUS.md` reconciled with reality (kept current each session).
- [x] **Feature freeze** — M14 (incl. G27 theming) is now complete; remaining work is QA + ship-safety
      + release mechanics, not new milestones.

## Gate 2 — The "Big Test" (integration QA, features frozen)
One full, real service that deliberately combines features that landed separately:
- [-] Dual-version scripture **+** persistent lower-third **+** a slide transition, in one service.
      *(Dual-version scripture was **dropped** in QA; lower-third + transition verified — see `V2_QA_CHECKLIST.md` §A.)*
- [x] VideoPsalm agenda import → project its songs, scripture (ref-only) and media (HEVC) live.
- [x] Auto-advance crossing song → scripture → media item boundaries.
- [x] **Full Spanish pass** (flag is now ON): every view, dialog and VM message in `es`.
- [x] Backup create + restore round-trip on a second machine/profile.
- [-] Fix the dual-version minor bugs that were deferred to this pass. *(Moot — feature dropped.)*

## Gate 3 — Ship-safety (data + supply chain)
- [x] **Migration rollback snapshot** — `{db}.oabak.auto` taken before `MigrateAsync`, restored
      in place on failure (`InfrastructureServiceExtensions.InitialiseDatabaseAsync`). _Done 2026-06-17._
      Matters because M8.2 auto-update runs migrations unattended on user machines.
- [x] **Pin the FFmpeg binary hash** in `installer/fetch-ffmpeg.ps1` — _Done 2026-06-19._ All 9
      packages pinned to exact build filenames + SHA256, verified after download (refuses on mismatch).
      Re-ran clean (19 DLLs). No more "latest matching".
- [x] Auto-updater handles UAC cancel / non-admin denial gracefully — _Done 2026-06-19._
      `DownloadAndApplyAsync` returns bool; `Win32Exception 1223` (ERROR_CANCELLED) → false, caller
      stays running (Settings shows "update cancelled", startup path just continues). No crash.
- [x] (Optional) VC++ 2015–2022 runtime presence check at startup → friendly dialog if missing.
      *(Done — `App.WarnIfVcRuntimeMissing()`, non-blocking info dialog + log.)*

## Enforcement going forward (cheap, high value)
- [x] **Architecture test** (NetArchTest) locking layer boundaries — _Done._ `LayerDependencyTests`
      enforces G28 (Domain→nothing, Application→Domain, Infrastructure→never WPF).
- [ ] i18n discipline: new user-facing strings go through `.resx` only. With the flag ON, a
      hardcoded English string now ships *visibly* in a Spanish service.

---

## Deferred / decided — NOT gates
- **Plugin settings DPAPI encryption** — v1 plaintext is an approved decision; revisit when the
  key-bearing api.bible plugin actually ships (separate repo).
- **DynamicResource / Light theme** — ✅ **DONE 2026-06-19 (G27, M14.x):** all brushes are
  `{DynamicResource}`, `Colors.{Dark,Light}.xaml` + `IAppThemeService` runtime swap, Settings toggle.
  Was an M14 item, not a v2.0 gate; now closed. Re-test via §M of the QA checklist.
- **SQLite `VACUUM`/`ANALYZE` after big imports** — nice-to-have, not blocking.
- **Feature ideas** from the reviews (OBS/WebSocket output, LAN web stage monitor, sanctuary
  alerts, CCLI usage auditor, smart verse-split, multi-version compare, songbook hierarchy,
  text stroke, context-menu "add to service", background dim) — roadmap fodder, no defects.
  Note: stage clock/countdown was already reviewed and **dropped**; text-stroke + background-dim
  overlap planned M14.

## Release mechanics (every version)
- [x] GitHub release with tag `vX.Y.Z` + MSI asset — the in-app auto-updater parses releases.
      *(v2.0.0 published 2026-06-19.)*
- [ ] Consider GitVersion to retire the hand-edited `<Version>` in the WPF `.csproj`. *(Still open — nice-to-have.)*
