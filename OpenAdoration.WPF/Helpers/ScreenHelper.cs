using System.Windows;
using WinForms = System.Windows.Forms;

namespace OpenAdoration.WPF.Helpers;

public static class ScreenHelper
{
    /// <summary>
    /// Returns the first non-primary screen, or null if only one monitor is connected.
    /// </summary>
    public static WinForms.Screen? GetSecondaryScreen() =>
        WinForms.Screen.AllScreens.FirstOrDefault(s => !s.Primary);

    /// <summary>
    /// Positions and sizes a WPF window to exactly fill the given screen.
    /// Uses device pixels — correct even on mixed-DPI setups.
    /// </summary>
    public static void PositionOnScreen(Window window, WinForms.Screen screen)
    {
        var bounds = screen.Bounds;
        window.Left   = bounds.Left;
        window.Top    = bounds.Top;
        window.Width  = bounds.Width;
        window.Height = bounds.Height;
    }

    /// <summary>
    /// Returns all available screen names for display in the settings UI.
    /// </summary>
    public static IReadOnlyList<string> GetScreenNames() =>
        WinForms.Screen.AllScreens
            .Select((s, i) => $"Screen {i + 1}{(s.Primary ? " (Primary)" : string.Empty)} — {s.Bounds.Width}×{s.Bounds.Height}")
            .ToList();
}
