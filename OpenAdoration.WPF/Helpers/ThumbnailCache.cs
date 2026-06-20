using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// On-disk PNG cache for generated thumbnails (the expensive FFmpeg ones), keyed by source
/// path + size + last-write so an edited file regenerates. Lets HEVC decode happen once, ever.
/// </summary>
internal static class ThumbnailCache
{
    // Mirrors App.xaml.cs data-dir resolution so OA_DATA_DIR isolation (e2e/portable) is honoured.
    private static readonly string CacheDir = Path.Combine(
        Environment.GetEnvironmentVariable("OA_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAdoration"),
        "thumbcache");

    public static BitmapSource? TryLoad(string source)
    {
        try
        {
            var file = PathFor(source);
            if (file is null || !File.Exists(file)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(file, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public static void Save(string source, BitmapSource bmp)
    {
        try
        {
            var file = PathFor(source);
            if (file is null) return;
            Directory.CreateDirectory(CacheDir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(file);
            encoder.Save(fs);
        }
        catch { /* cache write is best-effort */ }
    }

    private static string? PathFor(string source)
    {
        try
        {
            var fi = new FileInfo(source);
            if (!fi.Exists) return null;
            var key = $"{source}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
            var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)));
            return Path.Combine(CacheDir, hash + ".png");
        }
        catch { return null; }
    }
}
