using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace PlainDraft.Services;

/// <summary>
/// Standard DWM shadow + Win11 rounded corners + border tint.
/// Requires AllowsTransparency=False.
/// </summary>
internal static class DwmHelper
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwcpRound = 2;

    /// <summary>Reset DWMWA_BORDER_COLOR to the OS default frame color.</summary>
    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);

    private static bool? _win11FrameChrome;

    /// <summary>
    /// True when DWMWA_BORDER_COLOR is supported (Windows 11).
    /// Used to gate Win10-only dialog outline fallback.
    /// </summary>
    public static bool HasWin11FrameChrome =>
        _win11FrameChrome ?? (Environment.OSVersion.Version.Build >= 22000);

    public static void ApplySystemChrome(Window window, Color paperColor, bool systemBorder = false)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();

        var preference = DwmwcpRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));

        // COLORREF: 0x00BBGGRR; DWMWA_COLOR_DEFAULT restores the system frame.
        var paperRef = ToColorRef(paperColor);
        var borderRef = systemBorder ? DwmwaColorDefault : paperRef;
        var borderHr = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderRef, sizeof(int));
        _win11FrameChrome = borderHr == 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref paperRef, sizeof(int));

        var margins = new Margins { Left = 0, Right = 0, Top = 0, Bottom = 1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private static int ToColorRef(Color color) =>
        color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }
}
