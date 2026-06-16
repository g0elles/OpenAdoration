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
$api       = "https://api.anaconda.org/package/conda-forge"

# FFmpeg is pinned to a specific LGPL build. Its runtime dependencies are pinned to the
# version ranges that build was compiled against (conda 'depends'), so the DLL ABIs match.
# We only load the playback libraries (MinimumFeatures), so avfilter's font deps are omitted.
$pinnedFfmpeg = "ffmpeg-4.4.2-lgpl_h907f4eb_4.tar.bz2"
$depPackages  = @(
    @{ pkg = "libzlib";        ver = "1.2.13" },  # zlib.dll
    @{ pkg = "bzip2";          ver = "1.0.8"  },  # libbz2.dll
    @{ pkg = "libiconv";       ver = "1.17"   },  # iconv.dll
    @{ pkg = "libxml2";        ver = "2.9.14" },  # libxml2.dll (must be 2.9.x for FFmpeg 4.4.2)
    @{ pkg = "xz";             ver = "5.2"    },  # liblzma.dll (libxml2 dep)
    @{ pkg = "openh264";       ver = "2.2.0"  },  # openh264-6.dll (avcodec)
    @{ pkg = "svt-av1";        ver = "1.1.0"  },  # svtav1enc.dll (avcodec)
    @{ pkg = "aom";            ver = "3.4.0"  }    # aom.dll (avcodec, AV1)
)
# Note: FFmpeg also needs the VC++ 2015-2022 runtime (vcruntime140.dll / msvcp140.dll),
# which ships with Windows 10/11 and the .NET runtime — treated as a system prerequisite.

# FFmpeg shared libraries FFME loads (4.4 sonames). libpostproc is GPL-only and
# intentionally absent from the LGPL build — the app loads everything except it.
$requiredFfmpeg = @(
    "avcodec-58.dll", "avformat-58.dll", "avutil-56.dll",
    "swresample-3.dll", "swscale-5.dll", "avfilter-7.dll", "avdevice-58.dll"
)

function Resolve-Win64([string]$pkg, [string]$ver) {
    $files = Invoke-RestMethod "$api/$pkg/files"
    $cand  = $files |
        Where-Object { $_.attrs.subdir -eq 'win-64' -and $_.basename -like '*.tar.bz2' -and ($ver -eq '' -or $_.version -like "$ver*") } |
        Sort-Object { [datetime]$_.upload_time } -Descending |
        Select-Object -First 1
    if (-not $cand) { throw "No win-64 .tar.bz2 found for conda-forge/$pkg $ver." }
    return Split-Path $cand.basename -Leaf
}

function Expand-CondaDlls([string]$fileName, [string]$work, [string]$dest) {
    $archive = Join-Path $work $fileName
    Write-Host "    - $fileName"
    Invoke-WebRequest -Uri "$cdn/$fileName" -OutFile $archive -UseBasicParsing
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

    Write-Host "==> Downloading FFmpeg 4.4.2 (LGPL) + dependencies from conda-forge..."
    Expand-CondaDlls $pinnedFfmpeg $work $targetDir
    foreach ($d in $depPackages) {
        Expand-CondaDlls (Resolve-Win64 $d.pkg $d.ver) $work $targetDir
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
