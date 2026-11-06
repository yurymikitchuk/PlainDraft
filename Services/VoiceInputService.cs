using System.Runtime.InteropServices;

namespace PlainDraft.Services;

/// <summary>
/// Starts/stops Windows Voice Typing (Win+H / Esc). The app focuses a TextBox
/// first — RichTextBox TextStore often drops dictation at line ends.
/// </summary>
public sealed class VoiceInputService : IDisposable
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VkLWin = 0x5B;
    private const ushort VkH = 0x48;
    private const ushort VkEscape = 0x1B;

    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    private bool _disposed;

    public event Action<string>? Failed;

    public void Start()
    {
        if (_disposed) return;
        try
        {
            if (!SendWinH())
                Failed?.Invoke("Не удалось открыть голосовой ввод Windows (SendInput).");
        }
        catch (Exception ex)
        {
            Failed?.Invoke("Не удалось открыть голосовой ввод Windows: " + ex.Message);
        }
    }

    /// <summary>Esc closes the Voice Typing flyout (Win+H often only pauses the mic).</summary>
    public void Stop()
    {
        if (_disposed) return;
        try { SendKey(VkEscape); }
        catch { /* panel may already be gone */ }
    }

    public void Dispose() => _disposed = true;

    private static bool SendWinH()
    {
        INPUT[] inputs =
        [
            Key(VkLWin, KeyEventExtendedKey),
            Key(VkH, 0),
            Key(VkH, KeyEventKeyUp),
            Key(VkLWin, KeyEventExtendedKey | KeyEventKeyUp)
        ];
        return SendInput((uint)inputs.Length, inputs, InputSize) == inputs.Length;
    }

    private static bool SendKey(ushort vk)
    {
        INPUT[] inputs = [Key(vk, 0), Key(vk, KeyEventKeyUp)];
        return SendInput((uint)inputs.Length, inputs, InputSize) == inputs.Length;
    }

    private static INPUT Key(ushort vk, uint flags) => new()
    {
        type = InputKeyboard,
        U = { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags, dwExtraInfo = GetMessageExtraInfo() } }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    // MOUSEINPUT is the largest arm — required for correct SendInput cbSize.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();
}
