using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using Unosquare.FFME;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Initializes the FFmpeg-backed media engine (FFME) so the projector can decode any codec
/// — including HEVC/H.265 (iPhone .MOV) — without depending on Windows-installed codecs.
///
/// The FFmpeg 4.4 shared binaries are deployed next to the executable in an <c>ffmpeg</c>
/// folder (fetched by <c>installer/fetch-ffmpeg.ps1</c> for dev, bundled by the MSI for users).
/// </summary>
public static class MediaEngine
{
    /// <summary>Folder, next to the executable, expected to contain the FFmpeg shared DLLs.</summary>
    public static string FFmpegDirectory => Path.Combine(AppContext.BaseDirectory, "ffmpeg");

    /// <summary>True once FFmpeg has been loaded successfully; video playback requires this.</summary>
    public static bool IsLoaded { get; private set; }

    public static void Initialize(ILogger logger)
    {
        var dir = FFmpegDirectory;
        if (!Directory.Exists(dir))
        {
            logger.LogError(
                "FFmpeg not found at '{Dir}' — video playback is unavailable. " +
                "Dev: run installer/fetch-ffmpeg.ps1. Users: reinstall (the MSI bundles FFmpeg).", dir);
            return;
        }

        // Pre-load every native DLL from the folder so the FFmpeg libraries find their many
        // sibling dependencies (avcodec -> openh264/svt-av1/aom/zlib; avformat -> libxml2/…).
        // LOAD_WITH_ALTERED_SEARCH_PATH makes Windows resolve each DLL's dependencies from the
        // FFmpeg folder regardless of the process DLL-search mode, so once they are all in
        // memory FFME's own loader resolves everything by module name. This is what makes
        // playback work on a machine that does NOT have these libraries on its PATH.
        SetDllDirectory(dir);
        PreloadNativeDependencies(dir, logger);

        Library.FFmpegDirectory = dir;

        // Load only the libraries needed for playback (avutil, avcodec, avformat, swresample,
        // swscale). This excludes avfilter/avdevice/postproc, which otherwise drag in extra
        // native dependencies (libpostproc is GPL-only; avfilter needs freetype/fontconfig).
        Library.FFmpegLoadModeFlags = FFmpegLoadMode.MinimumFeatures;

        try
        {
            IsLoaded = Library.LoadFFmpeg();
            if (IsLoaded)
                logger.LogInformation("FFmpeg media engine loaded from '{Dir}'", dir);
            else
                logger.LogError("FFmpeg failed to load from '{Dir}' — video playback is unavailable.", dir);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FFmpeg failed to load from '{Dir}' — video playback is unavailable.", dir);
        }
    }

    // Loads each FFmpeg DLL with LOAD_WITH_ALTERED_SEARCH_PATH (deps resolved from the DLL's
    // own folder). Two passes cover any load order — a DLL whose dependency loads later
    // succeeds on the second pass.
    private static void PreloadNativeDependencies(string dir, ILogger logger)
    {
        var dlls = Directory.GetFiles(dir, "*.dll");
        for (var pass = 0; pass < 2; pass++)
            foreach (var dll in dlls)
                LoadLibraryEx(dll, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);

        var failed = dlls.Where(d => LoadLibraryEx(d, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH) == IntPtr.Zero)
                         .Select(Path.GetFileName)
                         .ToList();
        if (failed.Count > 0)
            logger.LogWarning("Some FFmpeg DLLs could not be pre-loaded: {Failed}", string.Join(", ", failed));
    }

    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);
}
