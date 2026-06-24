# OpenAdoration Security and Performance Audit

Audited: 2026-06-23  
Scope: local source review of Domain, Application, Infrastructure, WPF, plugins, importers, backup/restore, update, and media paths.  
Verification: `dotnet list package --vulnerable --include-transitive`, `dotnet build`, `dotnet test OpenAdoration.Tests.Infrastructure`.

## Executive Summary

The application has several good hardening controls already in place: XML import uses `XmlReaderSettings` with DTDs prohibited, backup/media zip extraction validates path boundaries, Bible ZIP imports have size and compression-ratio limits, FTS queries use parameters/escaping, and architecture tests keep layer boundaries clean.

I did find one high-impact local-file vulnerability in plugin installation, one high-severity vulnerable transitive dependency, and several medium-risk hardening gaps around updates, background media import, and plaintext plugin secrets. The main performance risks are synchronous large-file media work on the UI thread and over-fetching song sections for list/search views.

## Security Findings

### S1 - High - Plugin manifest ID can escape the plugin root and delete/write arbitrary directories — ✅ RESOLVED 2026-06-23

Resolved: `PluginManager` routes every id→path through `PluginDir(id)`, which validates the id against `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$` (no separators, `..`, or drive roots) and bounds the resolved directory to `Root`. Reused by `Install`/`Remove`/`GetSettings`/`UpdateSettings`. Covered by `PluginManagerTests.Install_RejectsTraversingPluginId_WithoutTouchingFilesystem` (`../evil`, `..\evil`, `a/../../evil`, `C:\temp\evil`, UNC, empty).


Location:
- `OpenAdoration.WPF/Plugins/PluginManager.cs:116-118`
- `OpenAdoration.WPF/Plugins/PluginManager.cs:120-126`
- `OpenAdoration.WPF/Plugins/PluginManager.cs:135-150`

What:
`Install` reads `manifest.Id` from an untrusted `.oaplugin` archive, then uses it directly in `Path.Combine(Root, manifest.Id)`. If the ID is rooted or contains traversal segments, the resulting `dir` can point outside `%LOCALAPPDATA%\OpenAdoration\plugins`. The code then deletes that directory recursively if it exists and extracts plugin files into it. `Remove`, `GetSettings`, and `UpdateSettings` repeat the same unbounded `Path.Combine(Root, id)` pattern.

Why it matters:
A malicious plugin archive can cause arbitrary directory deletion or arbitrary file writes under the current user's permissions before the plugin assembly is even loaded. The zip entry `SafeCombine` guard is good, but it protects only paths relative to the already-chosen plugin directory; it does not validate the directory derived from `manifest.Id`.

Suggested fix:
Validate plugin IDs before any filesystem operation. Allow only a narrow identifier grammar such as `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`, reject path separators/drive roots, and resolve the final plugin directory through a root-bounded helper. Reuse that helper for `Install`, `Remove`, `GetSettings`, and `UpdateSettings`. Add tests with `../evil`, `..\evil`, `C:\temp\evil`, UNC paths, and valid IDs.

### S2 - High - Vulnerable transitive SQLite native package — ✅ RESOLVED 2026-06-23 (pending publish smoke-test)

Resolved: added a direct `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 reference in Infrastructure, overriding EF Core 10's transitive 2.1.11 (advisory range `<= 2.1.11`). `dotnet list package --vulnerable --include-transitive` now reports **no vulnerable packages**; build is 0 warnings / 0 errors; 82/82 infra tests pass (real SQLite open + migrations + FTS). **Manual verify still owed** (major 2.x→3.x bump touches native loading): run `win-x64` single-file publish + MSI and confirm GUI startup, Bible SQLite import, migrations, and backup/restore.


Location:
- `OpenAdoration.Infrastructure/OpenAdoration.Infrastructure.csproj` references `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9
- `OpenAdoration.WPF/OpenAdoration.WPF.csproj` and `OpenAdoration.Tests.Infrastructure/OpenAdoration.Tests.Infrastructure.csproj` inherit the vulnerable transitive dependency

What:
`dotnet list package --vulnerable --include-transitive` reports `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 with high severity: GHSA-2m69-gcr7-jv3q. It appears in Infrastructure, WPF, and Tests.Infrastructure.

Why it matters:
This app imports and opens user-supplied SQLite Bible files (`BibleSuperSearchSqliteParser`) and uses SQLite as the main application database. A known vulnerable SQLite native package increases risk when parsing untrusted or malformed database files.

Suggested fix:
Plan a dependency update that moves the resolved SQLitePCLRaw native package to a non-vulnerable version. Because native SQLite loading affects single-file publish and installer behavior, verify GUI startup, Bible SQLite import, migrations, backup/restore, and the win-x64 publish/MSI path after the bump.

### S3 - Medium - Auto-update can run an MSI without a required digest or Authenticode trust check — 🔶 PARTIAL 2026-06-23

Resolved (digest half): `GitHubUpdateService.VerifyIntegrityAsync` now **requires** a SHA256 digest — a release asset without one is deleted and rejected (throws) instead of proceeding with a warning. Still open: Authenticode signer-identity verification before launching `msiexec` (needs code signing to exist first); tracked as the S3 follow-up.


Location:
- `OpenAdoration.Infrastructure/Update/GitHubUpdateService.cs:95-106`
- `OpenAdoration.Infrastructure/Update/GitHubUpdateService.cs:118-129`

What:
The updater downloads the release MSI, verifies SHA256 only when GitHub's asset digest is present, and otherwise logs a warning and proceeds. It then launches `msiexec`. The code also notes that a compromised release can replace the digest and that Authenticode signing is the real defense.

Why it matters:
If a release asset lacks a digest, the update path becomes unauthenticated beyond TLS/GitHub. If the release account or API response is compromised, the digest does not provide independent trust. Since `msiexec` may elevate through UAC, this path deserves a stricter policy.

Suggested fix:
Require a valid SHA256 digest for all updates and refuse unsigned/unknown-publisher installers once code signing is available. Longer term, verify Authenticode signer identity before launching and document the expected certificate subject.

### S4 - Medium - Theme background import skips media size and signature validation — ✅ RESOLVED 2026-06-23

Resolved: `MediaSignatureValidator` moved to `Application/Common` (reachable by the service layer), the 1 GB cap centralized as `MediaFormats.MaxFileSizeBytes`, and `MediaService.ImportBackgroundAsync` now enforces both the size cap and the content-signature check (throws `InvalidDataException`) before hashing/copying — the same policy the general importer applies. `MediaViewModel` consumes the shared const. Covered by `MediaRepositoryTests.ImportBackgroundAsync_RejectsSpoofedContent`.


Location:
- `OpenAdoration.Application/Services/MediaService.cs:66-86`
- Caller: `OpenAdoration.WPF/ViewModels/AddEditThemeViewModel.cs:198-209`
- Contrast: `OpenAdoration.WPF/ViewModels/MediaViewModel.cs:149-160`

What:
General media import enforces a 1 GB size limit and validates file signatures before copying. Theme background import only checks that the source file exists and has a supported extension, then hashes and copies it. It does not enforce the same size cap or call `MediaSignatureValidator`.

Why it matters:
An operator can select a very large or spoofed background file. That can cause disk/memory pressure during hashing/copying and may pass malformed media into image/video rendering libraries. Extension checks alone are not enough for files that later reach WPF/FFmpeg.

Suggested fix:
Move media validation into `MediaService` or a shared application-level helper so both general media and background imports enforce the same size, signature, and supported-format policy. Pass cancellation tokens through hashing/copying where possible.

### S5 - Medium - Plugin settings store API keys in plaintext — ✅ RESOLVED 2026-06-23

Resolved: DPAPI-protected the per-plugin settings blob (`CurrentUser` scope + a static entropy tag) using `System.Security.Cryptography.ProtectedData` — which ships in the `net10.0-windows` framework, so **no new package dependency** (an explicit reference drew NU1510 and was removed). `PluginManager` now writes encrypted bytes to `settings.dat` (renamed from `settings.json`, which previously held plaintext) and decrypts on load — secrets are no longer readable as plaintext on disk/backups. Whole-blob encryption (no per-field manifest marking) was chosen as the lazy-correct scope; per-field marking can come if a plugin ever needs mixed plaintext/secret fields. Covered by the existing `UpdateSettings_PersistsAndIsReadBack` round-trip test (now exercises encrypt→decrypt).


Location:
- `OpenAdoration.WPF/Plugins/PluginManager.cs:96-102`
- `OpenAdoration.WPF/Plugins/PluginManager.cs:146-150`

What:
Per-plugin settings, including future API keys, are serialized directly to `settings.json` under the plugin directory. The code comment explicitly notes plaintext storage.

Why it matters:
Any local process or user with access to the Windows profile can read plugin API keys. This is not remote compromise by itself, but it is a real secret-handling weakness for bring-your-own-key plugins.

Suggested fix:
Use DPAPI (`ProtectedData`) for secret-valued fields, keyed by the current Windows user. Keep non-secret settings plaintext. At minimum, mark secret settings in the plugin manifest and redact them in logs/UI.

## Performance Findings

### P1 - High - Large media imports hash and copy files synchronously on the UI thread — ✅ RESOLVED 2026-06-23

Resolved: per-file `FileInfo`/signature/SHA256/`File.Copy` now run via `Task.Run` off the UI thread in `MediaViewModel.ImportPathsAsync` (new `ValidateForImport` helper), and `MediaService.ImportBackgroundAsync` offloads hash + copy the same way. Not done: per-file progress reporting and threading a `CancellationToken` through the loop — deferred.


Location:
- `OpenAdoration.WPF/ViewModels/MediaViewModel.cs:130-180`
- `OpenAdoration.Application/Services/MediaService.cs:77-86`

What:
`ImportPathsAsync` is async, but the expensive work inside each iteration is synchronous: `new FileInfo`, signature reads, SHA256 hashing, and `File.Copy`. Background imports do the same in `MediaService.ImportBackgroundAsync`.

Why it matters:
Importing multiple videos or near-1 GB files can freeze the WPF UI even though `IsBusy` is set. It also makes cancellation ineffective during the most expensive work.

Suggested fix:
Move hashing/copying behind async file streams or `Task.Run` with a cancellation token. Report progress per file. Centralize import in `IMediaService` so UI code orchestrates and the service owns validation, hashing, copy, and dedup.

### P2 - Medium - Song list/search queries eagerly load all sections — ⏸ DEFERRED (needs care)

Traced 2026-06-23: the list/search results are not display-only — `SongsViewModel.EditSong` passes the list item straight into `InitialiseEdit` (reads `song.Sections`) and `SongsViewModel.ProjectSong` calls `GenerateSlides(song, …)` off the same item. So simply dropping the `Include(Sections)` would (a) blank lyrics on save via the RemoveRange+re-add path and (b) project empty slides. The correct fix is to drop the includes **and** re-fetch the full song by id at both the edit and project call sites (the schedule picker already uses only `.Id`, so it's safe). That touches the projection path for a scale-dependent win — deferred to its own focused change with a test rather than bundled here.


Location:
- `OpenAdoration.Infrastructure/Repositories/SongRepository.cs:40-49`
- `OpenAdoration.Infrastructure/Repositories/SongRepository.cs:51-69`
- `OpenAdoration.Infrastructure/Repositories/SongRepository.cs:71-85`

What:
`GetAllAsync`, `SearchByTitleAsync`, and `SearchByLyricsAsync` all include `Sections` ordered by section order. List and title/author search screens usually need only summary fields until the operator opens or projects a song.

Why it matters:
Large song libraries will fetch many unnecessary section rows on initial load and on search. This increases SQLite I/O, allocations, and UI collection update cost.

Suggested fix:
Add a song summary query/model for list/search views, or split list retrieval from full song retrieval. Keep sections in `GetByIdAsync` and in projection/edit flows that actually need lyrics.

### P3 - Medium - Bible upsert materializes all existing verse keys in memory — ⏸ DEFERRED (acceptable at scale)

Deferred 2026-06-23: the fix (unique index + SQLite `INSERT OR IGNORE`, or temp-table anti-join) needs a schema migration and a raw-SQL rewrite of the EF batch path. The audit itself rates the current HashSet approach "acceptable at current scale"; not worth a speculative migration until library size actually makes it bite (YAGNI). The in-memory guard is correct, just not optimal.


Location:
- `OpenAdoration.Infrastructure/Repositories/BibleRepository.cs:247-260`

What:
`InsertMissingVersesAsync` loads every existing `(Book, Chapter, Verse)` key for a Bible version into memory, converts it to a `HashSet`, then filters incoming verses against it.

Why it matters:
For full Bibles, this means tens of thousands of keys are loaded on every re-import or plugin enrichment, even when most verses already exist. It is acceptable at current scale, but it will age poorly as plugin sync/retry paths become more common.

Suggested fix:
Prefer a database-native idempotent insert strategy, such as a unique index plus SQLite `INSERT OR IGNORE`, or stage incoming rows into a temp table and insert only anti-join misses.

### P4 - Low - Plugin loading reads the entire entry assembly into memory — ✅ RESOLVED 2026-06-23

Resolved (the practical half): `PluginManager.Install` now rejects an `.oaplugin` whose total uncompressed payload exceeds `MaxPluginTotalBytes` (100 MB) before extracting, capping the disk + load-into-memory cost. The `File.ReadAllBytes`→`MemoryStream` load is kept deliberately (delete-without-restart); live unload is left as a future option.


Location:
- `OpenAdoration.WPF/Plugins/PluginManager.cs:78-84`

What:
`LoadPlugin` uses `File.ReadAllBytes` and wraps the bytes in `MemoryStream` before loading the assembly, to avoid locking the DLL on disk.

Why it matters:
This is fine for small plugins, but large plugin assemblies/dependency bundles add startup allocations. It is a lower-priority concern than the plugin path issue because plugins are optional and trusted by design.

Suggested fix:
Keep this if delete-without-restart is important, but enforce reasonable `.oaplugin` total size limits during install and document the tradeoff. Consider live unload/restart semantics if plugin packages grow.

## Positive Controls Observed

- XML import root sniffing and USFX parsing prohibit DTD processing and set `XmlResolver = null`.
- Bible ZIP import enforces entry count, uncompressed size, total size, line count, line length, and compression-ratio limits.
- Backup restore applies compression-ratio checks before extracting entries and bounds media extraction paths with `SafeCombine`.
- Plugin zip entry extraction uses `SafeCombine`; the missing guard is on `manifest.Id`, not zip entry names.
- SQLite FTS search uses parameters and escapes FTS terms before `MATCH`.
- `dotnet build` succeeded and `dotnet test OpenAdoration.Tests.Infrastructure` passed 75/75.

## Verification Notes

- `dotnet list package --vulnerable --include-transitive` succeeded and reported only `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 as vulnerable.
- `dotnet build` succeeded with NU1903 warnings for that same package.
- `dotnet test OpenAdoration.Tests.Infrastructure` passed 75 tests. The test build retried once because `OpenAdoration.Infrastructure.dll` was temporarily locked by a .NET host process, then completed successfully.

