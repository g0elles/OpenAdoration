# Release process

How to cut a new OpenAdoration release. The output is a single MSI that the in-app
auto-update (Milestone 8.2) will discover from the GitHub **Releases** page.

## Prerequisites (one-time)

- .NET 10 SDK
- WiX **v5** CLI: `dotnet tool install --global wix --version 5.0.2`
  *(Do not use WiX v6/v7 — they require accepting the paid OSMF EULA.)*
- `gh` CLI authenticated (for uploading the release), or upload the MSI manually.

## Version is single-sourced

The version lives in `OpenAdoration.WPF/OpenAdoration.WPF.csproj`
(`<Version>`, `<FileVersion>`, `<AssemblyVersion>`). Update all three to the new
version before tagging. The MSI version is passed on the build command line and
must match.

## Steps (automated — preferred)

Releases are built and published by the **`release.yml`** GitHub Actions workflow
when a `vX.Y.Z` tag is pushed. The runner stages FFmpeg, runs `installer/build.ps1`,
and creates the GitHub release with the MSI attached — no local build needed.

1. **Bump the version** in `OpenAdoration.WPF.csproj` (`<Version>`/`<FileVersion>`/
   `<AssemblyVersion>`). The workflow **fails fast if `<Version>` ≠ the tag**, so this
   must match before tagging.
2. **Update `CHANGELOG.md`** — add a dated `[x.y.z]` section (Added/Changed/Fixed) and a compare link at the bottom.
3. **Merge to `master`** (via PR — `master` is branch-protected; CI must pass).
4. **Tag and push:**

   ```powershell
   git tag v2.0.0
   git push origin v2.0.0
   ```

   `release.yml` builds `OpenAdoration-2.0.0-win-x64.msi` and publishes the release
   with auto-generated notes. The MSI **asset name** keeps the
   `OpenAdoration-<version>-win-x64.msi` pattern that auto-update (8.2) will parse.
5. **Smoke-test** the published MSI on a clean Windows machine: install, launch,
   project a song, uninstall.

## Building locally (fallback / testing)

To produce the MSI without tagging (e.g. to test the installer):

```powershell
pwsh installer/build.ps1 -Version 2.0.0
```

Produces `installer/out/OpenAdoration-2.0.0-win-x64.msi` (self-contained, no .NET
prerequisite). This is exactly what `release.yml` runs. `fetch-ffmpeg.ps1` (run by the
build if FFmpeg is missing) verifies each binary against a pinned SHA256.

## What auto-update expects (Milestone 8.2)

- A GitHub release whose **tag** is `vX.Y.Z` (SemVer).
- Exactly one `.msi` asset matching `OpenAdoration-*-win-x64.msi`.
- `IUpdateService` compares the release tag against the running assembly version,
  downloads the MSI, then launches `msiexec /i`. The app exits only if the installer
  actually starts; if the operator cancels the UAC prompt it keeps running.

## Conventions

- **SemVer:** MAJOR for breaking data/format changes (e.g. a migration that older
  builds can't read), MINOR for features, PATCH for fixes.
- Keep the `UpgradeCode` in `installer/OpenAdoration.wxs` **stable forever**
  (`94340D83-8ACA-413F-A3C8-3B71C73D8D5C`) — changing it breaks in-place upgrades.
- Never commit `installer/out/` artifacts (gitignored).
