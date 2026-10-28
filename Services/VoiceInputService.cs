using System.Runtime.InteropServices;

namespace PlainDraft.Services;

/// <summary>
/// Opens Windows Voice Typing (Win+H) into the focused editor.
/// </summary>
public sealed class VoiceInputService : IDisposable
{
    private bool _disposed;

    public event Action<string>? Failed;
    public event Action? ListeningEnded;

    public void Start()
    {
        if (_disposed) return;

        try
        {
            SendWinH();
            ListeningEnded?.Invoke();
        }
        catch (Exception ex)
        {
            Failed?.Invoke("Не удалось открыть голосовой ввод Windows: " + ex.Message);
        }
    }

    public void Stop()
    {
        // System voice typing is controlled by Windows.
    }

    public void Dispose() => _disposed = true;

    private static void SendWinH()
    {
        const byte vkLWin = 0x5B;
        const byte vkH = 0x48;
        const uint keyUp = 0x0002;

        keybd_event(vkLWin, 0, 0, UIntPtr.Zero);
        keybd_event(vkH, 0, 0, UIntPtr.Zero);
        keybd_event(vkH, 0, keyUp, UIntPtr.Zero);
        keybd_event(vkLWin, 0, keyUp, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
