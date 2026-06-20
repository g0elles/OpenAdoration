# Contributing to OpenAdoration — Working Standard

This is the core standard for how work lands in OpenAdoration. It exists because the
project was delivered out of order (normal for church-driven work) and the **source of
truth drifted**: the roadmap listed shipped features as "not started", and documented
invariants (migration snapshots, dynamic-resource theming) were written as rules but were
false in the code. Out-of-order is fine. **Unverified truth is not.**

## The principle

> A fact lives in exactly one place, and a claimed invariant is backed by a test —
> or it isn't claimed.

## The four rules

1. **Definition of Done (per milestone/feature).** Order doesn't matter; proof does. A change
   is *done* only when **all** of:
   - builds with **0 warnings / 0 errors**;
   - tests green (`dotnet test OpenAdoration.Tests.Infrastructure`);
   - user-facing strings go through `.resx` (never hardcoded) — the i18n flag is **on**, so a
     stray English literal ships visibly in a Spanish service;
   - GUI-verified for anything user-visible;
   - `ROADMAP.md` (status) and `SESSION_STATUS.md` (in-flight notes) updated **in the same commit**.

2. **No invariant in prose without a test.** If an architectural or safety rule matters
   ("layers don't cross", "migrations snapshot first"), it gets a test. If we won't write the
   test, it's documented as *aspiration*, not as a rule. New `CLAUDE.md` gotchas tagged as rules
   must be ✅ENFORCED (name the test/impl) or ⚠️ASPIRATION.

3. **One source of truth per fact.** `ROADMAP.md` = milestone status. Tests = code invariants.
   `SESSION_STATUS.md` = in-flight only. Don't restate a fact in a second doc; link it.

4. **Release ritual.** Before any `X.0`, run [`docs/V2_RELEASE_GATE.md`](docs/V2_RELEASE_GATE.md):
   reconcile the roadmap, run the integration "Big Test", clear ship-safety items.

## Memory & cross-references (so context stays cheap to load)

Knowledge is **typed** and lives in exactly one home; notes **point** to it instead of restating it.

- **Where each kind lives.** Policy/rules → `CLAUDE.md` (+ G-gotchas). Working preferences & durable
  facts → `.claude/memory/` (typed frontmatter) and `ROADMAP.md` (milestone status). Episodic "what we
  did" → `SESSION_STATUS.md`. The repo + git history is the raw trace.
- **`SESSION_STATUS.md` is a *recent* working log**, not an archive: keep ~6–8 newest entries; roll older
  ones into `SESSION_STATUS_ARCHIVE.md` (grep by date). When a fact in a session note becomes durable,
  **promote it** to the right typed home and let the raw entry age out. (Both files are local-only — never committed.)
- **Every session entry ends with a `↳ Refs` footer** pointing to where the work lives, graded by scope:
  - *pinpoint* (a fix): the exact file(s) `path:line` + the one milestone/gotcha ID it touches;
  - *feature*: a block — milestone ID, the key files, CHANGELOG, ARCH section **only if** it adds design
    rationale the code can't show (code is the source of truth for "what/how");
  - *cross-cutting*: link the whole file(s).
- **Reference by stable ID / heading / filename — never bare line numbers** (they rot). Use `M10.1`, `G27`,
  `§12`, `[2.0.1]`, `[[memory-file]]`, `path/to/File.cs::Symbol`. See [`docs/CONTEXT_MAP.md`](docs/CONTEXT_MAP.md)
  for the subsystem → (roadmap ID · files · arch §) lookup.

## Branch flow

- Active work → `dev` (public repo; commit/push here). Pair every commit with a `SESSION_STATUS.md`
  update.
- Promote `dev` → `master` via PR. `master` is branch-protected: `build-test` + `analyze` checks
  required. Dependabot targets `dev`, so `master` gets only curated bumps.
- Never skip hooks or bypass signing.

## Enforcement (what's automated, so it can't rot)

- **Layer boundaries:** `OpenAdoration.Tests.Infrastructure/Architecture/LayerDependencyTests.cs`
  (NetArchTest) — runs in CI on every PR.
- **i18n parity:** the en/es `.resx` parity test fails if keys diverge.
- **Pre-done checklist:** the `definition-of-done` skill runs the automatable DoD checks on demand
  (build, tests, hardcoded-string scan, docs-touched) and prints the human-judgment items.
