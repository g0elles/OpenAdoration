# Third-Party Notices

OpenAdoration is distributed under the [MIT License](LICENSE). It bundles or
depends on the third-party components listed below; each remains under its own
license. This file is provided to satisfy those licenses' notice requirements.

## Bundled at runtime (shipped in the installer)

| Component | License | Notes |
|---|---|---|
| **FFmpeg** (shared libraries, used via FFME) | **LGPL v2.1+** | Dynamically linked. The FFmpeg shared libraries can be replaced by the user with compatible builds. Source: <https://ffmpeg.org/>. License: <https://www.ffmpeg.org/legal.html>. The build is configured to avoid GPL-only components. |
| **FFME.Windows** | Ms-PL / project terms | WPF media element wrapping FFmpeg. <https://github.com/unosquare/ffmediaelement> |
| **SQLite** (native `e_sqlite3` via SQLitePCLRaw) | Public Domain | <https://www.sqlite.org/copyright.html> |
| **Microsoft.EntityFrameworkCore** (+ `.Sqlite`) | MIT | <https://github.com/dotnet/efcore> |
| **Microsoft.Extensions.*** (Hosting, Logging) | MIT | <https://github.com/dotnet/runtime> |
| **CommunityToolkit.Mvvm** | MIT | <https://github.com/CommunityToolkit/dotnet> |
| **Extended.Wpf.Toolkit** | Ms-PL | <https://github.com/xceedsoftware/wpftoolkit> |
| **Serilog** (+ Sinks.File, Sinks.Debug, Extensions.Logging) | Apache-2.0 | <https://github.com/serilog/serilog> |
| **.NET runtime / WPF** | MIT | <https://github.com/dotnet/wpf> |

## LGPL compliance (FFmpeg)

The FFmpeg libraries are licensed under the GNU Lesser General Public License
(LGPL) v2.1 or later and are used via dynamic linking. In accordance with the
LGPL: the libraries are shipped as separate, replaceable shared library files;
their license text and source location are referenced above; and users may
substitute their own compatible FFmpeg builds.

## Build / development only (not shipped)

xUnit (Apache-2.0), Microsoft.NET.Test.Sdk (MIT), NetArchTest.Rules (MIT).

---

If you believe an attribution is missing or incorrect, please open an issue.
