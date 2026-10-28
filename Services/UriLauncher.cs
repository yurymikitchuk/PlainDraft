using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PlainDraft.Services;

internal static class UriLauncher
{
    public static void Open(Uri uri)
    {
        try
        {
            // file:// → LocalPath so Explorer/apps get a normal Windows path.
            var target = uri.IsFile ? uri.LocalPath : uri.AbsoluteUri;
            var process = Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true
            });

            if (process is null) return;
            _ = Task.Run(() =>
            {
                try
                {
                    process.WaitForInputIdle(1500);
                    var hwnd = process.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                        SetForegroundWindow(hwnd);
                }
                catch
                {
                    // Existing browser tabs often keep focus; ignore.
                }
            });
        }
        catch
        {
            // Best-effort launch.
        }
    }

    public static void Open(string absoluteUri)
    {
        if (Uri.TryCreate(absoluteUri, UriKind.Absolute, out var uri))
            Open(uri);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
