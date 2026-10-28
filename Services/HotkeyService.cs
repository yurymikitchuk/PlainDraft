using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace PlainDraft.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int IdGoToNote = 1;
    private const int IdToggleVisible = 2;

    private HwndSource? _source;
    private IntPtr _hwnd;

    public event Action? GoToNotePressed;
    public event Action? ToggleVisiblePressed;

    public void Attach(IntPtr hwnd)
    {
        Detach();
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    public void Register(string goToNote, string toggleVisible)
    {
        if (_hwnd == IntPtr.Zero) return;
        Unregister();
        TryRegister(IdGoToNote, goToNote);
        TryRegister(IdToggleVisible, toggleVisible);
    }

    public void Unregister()
    {
        if (_hwnd == IntPtr.Zero) return;
        UnregisterHotKey(_hwnd, IdGoToNote);
        UnregisterHotKey(_hwnd, IdToggleVisible);
    }

    public static bool TryParse(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Key? key = null;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= 0x0002;
                    break;
                case "alt":
                    modifiers |= 0x0001;
                    break;
                case "shift":
                    modifiers |= 0x0004;
                    break;
                case "win":
                case "windows":
                    modifiers |= 0x0008;
                    break;
                default:
                    if (!Enum.TryParse(part, true, out Key parsed)) return false;
                    key = parsed;
                    break;
            }
        }

        if (key is null) return false;
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
        return virtualKey != 0;
    }

    public static string Normalize(ModifierKeys mods, Key key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void TryRegister(int id, string gesture)
    {
        if (!TryParse(gesture, out var mods, out var vk)) return;
        RegisterHotKey(_hwnd, id, mods, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            switch (wParam.ToInt32())
            {
                case IdGoToNote:
                    GoToNotePressed?.Invoke();
                    handled = true;
                    break;
                case IdToggleVisible:
                    ToggleVisiblePressed?.Invoke();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private void Detach()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source = null;
        _hwnd = IntPtr.Zero;
    }

    public void Dispose()
    {
        Detach();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
