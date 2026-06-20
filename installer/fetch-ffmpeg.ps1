<#
.SYNOPSIS
  Downloads the FFmpeg shared libraries (and their runtime dependencies) that the FFME
  media engine loads, and stages them in OpenAdoration.WPF/ffmpeg/.

.DESCRIPTION
  FFME (FFME.Windows 4.4.x) binds FFmpeg 4.4. We use the conda-forge **LGPL** build of
  FFmpeg 4.4.2 (LGPL-2.1-or-later) so the binaries can be redistributed alongside our
  MIT-licensed app via dynamic linking.

  conda's FFmpeg links its dependencies dynamically (libxml2, iconv, zlib, bzip2, lzma),
  which live in separate conda packages — so we fetch those too and drop every DLL into
  the same folder. The binaries are NOT committed to git: run this once on a dev machine;
  the installer bundles the result for end users.

.NOTES
  Requires `tar` (bundled with Windows 10+). Run from anywhere:
    pwsh installer/fetch-ffmpeg.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot  = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $repoRoot "OpenAdoration.WPF\ffmpeg"
$cdn       = "https://conda.anaconda.org/conda-forge/win-64"

# Every package is pinned to an EXACT build filename + SHA256 of the archive bytes (supply-chain
# gate: we verify what we download instead of trusting "latest matching"). FFmpeg is the LGPL 4.4.2
# build; deps are the exact builds it was compiled against. We only load the playback libraries
# (MinimumFeatures), so avfilter's font deps are omitted.
# To bump: change the filename, re-run, take the printed actual hash, paste it here, re-run clean.
$packages = @(
    @{ file = "ffmpeg-4.4.2-lgpl_h907f4eb_4.tar.bz2"; sha256 = "d7f6d2999d638299af23a27169993161ca5c3c449e839f2d6e8093e82a3ea50c" },  # FFmpeg 4.4.2 LGPL
    @{ file = "libzlib-1.2.13-hcfcfb64_4.tar.bz2";    sha256 = "184da12b4296088a47086f4e69e65eb5f8537a824ee3131d8076775e1d1ea767" },  # zlib.dll
    @{ file = "bzip2-1.0.8-h8ffe710_4.tar.bz2";       sha256 = "5389dad4e73e4865bb485f460fc60b120bae74404003d457ecb1a62eb7abf0c1" },  # libbz2.dll
    @{ file = "libiconv-1.17-h8ffe710_0.tar.bz2";     sha256 = "657c2a992c896475021a25faebd9ccfaa149c5d70c7dc824d4069784b686cea1" },  # iconv.dll
    @{ file = "libxml2-2.9.14-hf5bbc77_4.tar.bz2";    sha256 = "cf8215e429ff6572f77ee7382b4c9e06a31126318f22d45fc281b6062d3be544" },  # libxml2.dll (2.9.x for FFmpeg 4.4.2)
    @{ file = "xz-5.2.6-h8d14728_0.tar.bz2";          sha256 = "54d9778f75a02723784dc63aff4126ff6e6749ba21d11a6d03c1f4775f269fe0" },  # liblzma.dll (libxml2 dep)
    @{ file = "openh264-2.2.0-h0e60522_2.tar.bz2";    sha256 = "f01fc82b13e7dd99a017ea1c107b4f3cb3d25619f804b722b520f24fdbfb4dad" },  # openh264 (avcodec)
    @{ file = "svt-av1-1.1.0-h0e60522_1.tar.bz2";     sha256 = "edf19ff4d5c7d6b78f7dbe3eabbe0314a099d5b5ba43713d8121bc9848b3122d" },  # svtav1enc (avcodec)
    @{ file = "aom-3.4.0-h0e60522_1.tar.bz2";         sha256 = "84f5264645fcc049168d4c1208daa87541313a126640f38842554a5276e3b4e0" }   # aom (avcodec, AV1)
)
# Note: FFmpeg also needs the VC++ 2015-2022 runtime (vcruntime140.dll / msvcp140.dll),
# which ships with Windows 10/11 and the .NET runtime — treated as a system prerequisite.

# FFmpeg shared libraries FFME loads (4.4 sonames). libpostproc is GPL-only and
# intentionally absent from the LGPL build — the app loads everything except it.
$requiredFfmpeg = @(
    "avcodec-58.dll", "avformat-58.dll", "avutil-56.dll",
    "swresample-3.dll", "swscale-5.dll", "avfilter-7.dll", "avdevice-58.dll"
)

function Expand-CondaDlls([string]$fileName, [string]$sha256, [string]$work, [string]$dest) {
    $archive = Join-Path $work $fileName
    Write-Host "    - $fileName"
    Invoke-WebRequest -Uri "$cdn/$fileName" -OutFile $archive -UseBasicParsing
    # Supply-chain gate: the bytes must match the pinned hash, or we refuse to use them.
    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne $sha256.ToLower()) {
        throw "SHA256 mismatch for $fileName`n  expected: $sha256`n  actual:   $actual"
    }
    $ex = Join-Path $work ([System.IO.Path]::GetFileNameWithoutExtension($fileName))
    New-Item -ItemType Directory -Force -Path $ex | Out-Null
    tar -xf $archive -C $ex
    if ($LASTEXITCODE -ne 0) { throw "tar extraction failed for $fileName (exit $LASTEXITCODE)." }
    $bin = Join-Path $ex "Library\bin"
    if (Test-Path $bin) { Copy-Item (Join-Path $bin "*.dll") $dest -Force }
    # Bundle the first license file we see, for LGPL compliance.
    $lic = Get-ChildItem (Join-Path $ex "info\licenses") -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($lic -and -not (Test-Path (Join-Path $dest "FFmpeg-LICENSE.txt"))) {
        Copy-Item $lic.FullName (Join-Path $dest "FFmpeg-LICENSE.txt") -Force
    }
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("ffmpeg-fetch-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    if (Test-Path $targetDir) { Remove-Item $targetDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    Write-Host "==> Downloading FFmpeg 4.4.2 (LGPL) + dependencies from conda-forge (SHA256-verified)..."
    foreach ($p in $packages) {
        Expand-CondaDlls $p.file $p.sha256 $work $targetDir
    }

    $missing = $requiredFfmpeg | Where-Object { -not (Test-Path (Join-Path $targetDir $_)) }
    if ($missing) { throw "Missing expected FFmpeg DLLs after copy: $($missing -join ', ')" }

    Write-Host "==> Done. FFmpeg staged at: $targetDir"
    Get-ChildItem $targetDir -Filter *.dll | Measure-Object Length -Sum |
        ForEach-Object { "    {0} DLL(s), {1:N1} MB total" -f $_.Count, ($_.Sum / 1MB) } | Write-Host
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
