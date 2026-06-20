# OpenAdoration — v2.0 Release Gate

A pre-release checklist for cutting v2.0 from `dev`. Derived from the engineering-lead
review (out-of-order execution risk) plus a code-verification pass on 2026-06-17.

The point: features have been delivered **out of order** (M12/M13/M10.5 pulled forward,
M8.2/M9/M10/M11.3 backfilled), so many were built in isolation and have **never been
exercised together**. These gates exist to catch what that hides. Work the gates top to
bottom; don't start a new milestone until they pass.

---

## Gate 1 — Reconcile before RC
- [ ] `ROADMAP.md` top progress table realigned to `dev`'s actual state (M8.2 auto-update,
      M9.1 ChordPro, M9.2 image-folder, M10.1 transitions, M10.2 lower-thirds,
      M10.3 dual-version, M11.3 Spanish are all **done** — table still says otherwise).
- [ ] `SESSION_STATUS.md` reconciled with reality.
- [ ] **Feature freeze** declared — no new milestones (incl. M14) until Gates 2–3 pass.

## Gate 2 — The "Big Test" (integration QA, features frozen)
One full, real service that deliberately combines features that landed separately:
- [ ] Dual-version scripture **+** persistent lower-third **+** a slide transition, in one service.
- [ ] VideoPsalm agenda import → project its songs, scripture (ref-only) and media (HEVC) live.
- [ ] Auto-advance crossing song → scripture → media item boundaries.
- [ ] **Full Spanish pass** (flag is now ON): every view, dialog and VM message in `es`.
- [ ] Backup create + restore round-trip on a second machine/profile.
- [ ] Fix the dual-version minor bugs that were deferred to this pass.

## Gate 3 — Ship-safety (data + supply chain)
- [x] **Migration rollback snapshot** — `{db}.oabak.auto` taken before `MigrateAsync`, restored
      in place on failure (`InfrastructureServiceExtensions.InitialiseDatabaseAsync`). _Done 2026-06-17._
      Matters because M8.2 auto-update runs migrations unattended on user machines.
- [ ] **Pin the FFmpeg binary hash** in `installer/fetch-ffmpeg.ps1` (currently resolves
      "latest matching" from conda-forge with no checksum — supply-chain hole).
- [ ] Auto-updater handles UAC cancel / non-admin denial gracefully (no crash loop).
- [ ] (Optional) VC++ 2015–2022 runtime presence check at startup → friendly dialog if missing.

## Enforcement going forward (cheap, high value)
- [ ] **Architecture test** (NetArchTest) locking layer boundaries — CLAUDE.md G28 calls for it;
      it was never built. One test file; protects the whole design from drift.
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
- [ ] GitHub release with tag `vX.Y.Z` + MSI asset — the in-app auto-updater parses releases.
- [ ] Consider GitVersion to retire the hand-edited `<Version>` in the WPF `.csproj`.
