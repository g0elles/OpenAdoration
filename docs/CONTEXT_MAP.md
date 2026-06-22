# Context Map — subsystem → where it lives

A lookup so a reference can name a **subsystem** and resolve to its roadmap slot, key files, and
architecture section without reading whole files. Used by the `↳ Refs` footer convention
(see [`CONTRIBUTING.md`](../CONTRIBUTING.md) → *Memory & cross-references*).

> Reference by **stable ID / heading / filename**, never bare line numbers. Roadmap = `M…` IDs in
> [`ROADMAP.md`](../ROADMAP.md); gotchas = `G…` in `CLAUDE.md`; arch = `§…` in [`ARCHITECTURE.md`](../ARCHITECTURE.md);
> `[[name]]` = a file in `.claude/memory/`. **Code is the source of truth for "what/how"; ARCH only for design rationale.**

| Subsystem | Roadmap | Key files | Arch | Gotchas / memory |
|---|---|---|---|---|
| **Projection engine** | M1, M6, M10 | `OpenAdoration.Application/Services/IProjectionService.cs` + `ProjectionService.cs`; `OpenAdoration.WPF/ProjectionWindow.xaml{.cs}` | §2, §12 | G5, G9 (unsubscribe), stale-render guard |
| **Theming + cascade** | M14 | `OpenAdoration.Application/Common/ThemeCascade.cs`; `OpenAdoration.WPF/Views/AddEditThemeView.xaml`; `…/ViewModels/AddEditThemeViewModel.cs` | §12 | G6 (song ThemeId), G20 |
| **App appearance (Light/Dark)** | M14.x | `OpenAdoration.WPF/Services/AppThemeService.cs`; `…/Styles/Colors.{Dark,Light}.xaml` | §3.5b | **G27 ✅ENFORCED** |
| **i18n (en/es)** | M11 | `OpenAdoration.WPF/Resources/Strings{.es}.resx`; `…/Localization/`; `…/Services/ILocalizationService.cs` | §12 | [[i18n_pattern]]; skill `localize` |
| **Stage View** | M6 | `OpenAdoration.WPF/Views/StageView.xaml{.cs}`; `…/ViewModels/StageViewModel.cs` | §12 | mirrors projection; bg-video via FFME |
| **Media / thumbnails / video** | M4, M9.2, M10.5 | `OpenAdoration.WPF/Views/MediaView.xaml`; `…/Helpers/{ShellThumbnail,FfmpegThumbnail,ThumbnailCache}.cs`; `…/Behaviors/ThumbnailImage.cs` | §12 | [[ffme_video]], G25 |
| **Service Schedule** | M3 | `OpenAdoration.WPF/Views/ServiceScheduleView.xaml{.cs}`; `…/ViewModels/ServiceScheduleViewModel.cs` | §12 | G19 (auto-advance timer) |
| **Bible (import + search)** | M2, M9.3 | `OpenAdoration.WPF/Services/BibleImportService.cs`; `…/Helpers/BibleImport/`; `BibleReferenceParser` | §2 | [[bible_import_dedup]]; G7, G14, G16, G21 |
| **Songs (CRUD + import)** | M0, M9.1 | `OpenAdoration.WPF/Helpers/SongImport/` (`SongFormatDispatcher`, `ChordProParser`, …); `Infrastructure/Repositories/SongRepository.cs` | — | G6 (UpdateAsync replaces sections) |
| **VideoPsalm migration** | M12 | `OpenAdoration.WPF/Helpers/VideoPsalmMigration/` | §12 | [[videopsalm_format]], [[m12_m13_plan]] |
| **Plugins** | M13 | `OpenAdoration.Plugins.Abstractions`; `OpenAdoration.WPF/Plugins/` | §12 | [[m12_m13_plan]]; in-proc full-trust |
| **Reliability: backup + auto-update** | M8 | `Infrastructure/Backup/ZipBackupService.cs`; `Infrastructure/Update/GitHubUpdateService.cs`; `Application/Services/{IBackupService,IUpdateService}.cs`; `WPF/App.xaml.cs` | §12 | G26 (safe migration); [[packaging_release]] |
| **Packaging / release** | M7.5 | `installer/` (`build.ps1`, `OpenAdoration.wxs`, `fetch-ffmpeg.ps1`); `.github/workflows/release.yml` | — | [[packaging_release]]; [`docs/RELEASE.md`](RELEASE.md) |

_Keep this current when a subsystem moves. One row = one subsystem's home; don't restate status here (that's `ROADMAP.md`)._
