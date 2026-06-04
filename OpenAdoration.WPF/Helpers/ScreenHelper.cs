using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;

namespace OpenAdoration.WPF.Helpers;

public static class ScreenHelper
{
    /// <summary>
    /// Returns the first non-primary screen, or null if only one monitor is connected
    /// (or the displays are mirrored / "Duplicate", which Windows reports as one screen).
    /// </summary>
    public static WinForms.Screen? GetSecondaryScreen() =>
        WinForms.Screen.AllScreens.FirstOrDefault(s => !s.Primary);

    /// <summary>
    /// Positions and sizes an already-shown window to exactly fill the given screen.
    /// Uses Win32 <c>SetWindowPos</c> with the screen's physical device-pixel bounds, which
    /// is correct on mixed-DPI / scaled setups — WPF's own <see cref="Window.Left"/> etc. are
    /// device-independent units, so feeding them raw device pixels lands the window on the
    /// wrong monitor or at the wrong size. The window must already have a handle (be shown).
    /// </summary>
    public static void PositionOnScreen(Window window, WinForms.Screen screen)
    {
        var bounds = screen.Bounds; // physical device pixels
        var hwnd   = new WindowInteropHelper(window).EnsureHandle();

        SetWindowPos(hwnd, IntPtr.Zero,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Returns all available screen names for display in the settings UI.
    /// </summary>
    public static IReadOnlyList<string> GetScreenNames() =>
        WinForms.Screen.AllScreens
            .Select((s, i) => $"Screen {i + 1}{(s.Primary ? " (Primary)" : string.Empty)} — {s.Bounds.Width}×{s.Bounds.Height}")
            .ToList();

    // -- Win32 -----------------------------------------------------------------

    private const uint SWP_NOZORDER     = 0x0004;
    private const uint SWP_NOACTIVATE   = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
