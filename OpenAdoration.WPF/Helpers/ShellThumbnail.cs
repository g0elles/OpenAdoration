using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Extracts a thumbnail for any file the Windows shell can render — images AND videos
/// (and PDFs, etc.) — via <c>IShellItemImageFactory</c>, using the codecs already on the
/// machine. Returns null on any failure. No FFmpeg frame-grabbing, no extra dependency.
/// </summary>
internal static class ShellThumbnail
{
    public static BitmapSource? TryGet(string path, int size)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        var iid = typeof(IShellItemImageFactory).GUID;
        if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null)
            return null;

        var hbitmap = IntPtr.Zero;
        try
        {
            if (factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF.ResizeToFit, out hbitmap) != 0
                || hbitmap == IntPtr.Zero)
                return null;

            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap);
            Marshal.ReleaseComObject(factory);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [Flags]
    private enum SIIGBF { ResizeToFit = 0x00, BiggerSizeOk = 0x01, ThumbnailOnly = 0x08 }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }
}
