# OpenAdoration — Agent Context

> **This file intentionally defers to the canonical docs.** It previously duplicated
> the full project context, which drifted out of date. To keep a single source of
> truth, the detailed context now lives in the files below — read those first.

## Read these (in order)

| File | What it gives you |
|---|---|
| **`CLAUDE.md`** | **Canonical** agent context — code standards, architecture, DI lifetimes, projection/token/settings systems, all gotchas (G1–G20), packaging, and the v1.0/v2.0 status. Read before touching any code. |
| **`ROADMAP.md`** | Canonical roadmap. v1.0 (M0–M7) shipped; v2.0 (M8–M10) in planning. Its top "current feature state" table is authoritative as of the last edit. |
| **`ARCHITECTURE.md`** | Developer reference — layer diagram, data-flow diagrams, service interfaces, DB schema, projection layer model. |
| **`CHANGELOG.md`** | What shipped in each version (Keep a Changelog format). |
| **`docs/RELEASE.md`** | How to cut a release (publish profile + WiX v5 MSI + GitHub release). |
| **`VIDEOPSALM_REFERENCE.md`** | Feature gap analysis vs VideoPsalm — the source of most roadmap ideas. |

## Non-negotiables (summary — full detail in CLAUDE.md)

- **Clean Architecture**: Domain ← Application ← Infrastructure; WPF → Application only. Never inject concrete types across layers.
- **SOLID + clean code**: VMs = state+commands; services = logic; repos = data. Methods < 20 lines, no dead code, no magic values.
- **Reliability**: every `IDisposable` unsubscribes events; non-UI-thread UI updates via `Dispatcher.Invoke`; `CancellationToken` through async chains; one `SaveChangesAsync` per logical op.
- **Patterns**: scope-per-navigation (G11); `IsBusy` guard owned by `LoadAsync` (G4); `DataTemplate` for every navigated VM (G8); fully-qualify WPF types under `UseWindowsForms` (G1).

## Project at a glance

- Free, offline, SQLite-only **WPF (.NET 10)** worship-presentation app for Windows 10+.
- Solution: `OpenAdoration.sln` (Domain / Application / Infrastructure / WPF / Tests.Infrastructure).
- Build: `dotnet build`; Run: `dotnet run --project OpenAdoration.WPF`; Test: `dotnet test OpenAdoration.Tests.Infrastructure`.
- DB at `%LOCALAPPDATA%\OpenAdoration\openadoration.db` (migrations auto-applied at startup).
