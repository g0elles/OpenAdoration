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

## Steps

1. **Bump the version** in `OpenAdoration.WPF.csproj` (e.g. `1.0.0` → `1.1.0`).
2. **Update `CHANGELOG.md`** — move items from `[Unreleased]` into a new dated
   `[x.y.z]` section; refresh the compare links at the bottom.
3. **Run the tests:** `dotnet test OpenAdoration.Tests.Infrastructure` (must be green).
4. **Build the installer:**

   ```powershell
   pwsh installer/build.ps1 -Version 1.1.0
   ```

   Produces `installer/out/OpenAdoration-1.1.0-win-x64.msi` (self-contained,
   no .NET prerequisite). This script publishes the single-file exe first, then
   builds the MSI.
5. **Smoke-test** the MSI on a clean Windows VM/machine: install, launch, project a
   song, uninstall.
6. **Tag and push:**

   ```powershell
   git tag v1.1.0
   git push origin v1.1.0
   ```
7. **Create the GitHub release** and attach the MSI:

   ```powershell
   gh release create v1.1.0 installer/out/OpenAdoration-1.1.0-win-x64.msi `
       --title "OpenAdoration 1.1.0" --notes-file CHANGELOG-1.1.0.md
   ```

   The MSI **asset name** must keep the `OpenAdoration-<version>-win-x64.msi`
   pattern — the auto-update check parses the version and downloads this asset.

## What auto-update expects (Milestone 8.2)

- A GitHub release whose **tag** is `vX.Y.Z` (SemVer).
- Exactly one `.msi` asset matching `OpenAdoration-*-win-x64.msi`.
- `IUpdateService` compares the release tag against the running assembly version,
  downloads the MSI, then launches `msiexec /i` and exits.

## Conventions

- **SemVer:** MAJOR for breaking data/format changes (e.g. a migration that older
  builds can't read), MINOR for features, PATCH for fixes.
- Keep the `UpgradeCode` in `installer/OpenAdoration.wxs` **stable forever**
  (`94340D83-8ACA-413F-A3C8-3B71C73D8D5C`) — changing it breaks in-place upgrades.
- Never commit `installer/out/` artifacts (gitignored).
