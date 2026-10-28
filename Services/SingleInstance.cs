using System.IO.Pipes;
using System.Text;

namespace PlainDraft.Services;

public sealed class SingleInstance : IDisposable
{
    public const string MutexName = "Local\\PlainDraft.QuickNotes.SingleInstance";
    public const string PipeName = "PlainDraft.QuickNotes.Activate";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private bool _ownsMutex;

    public bool IsFirstInstance => _ownsMutex;

    public event Action? ActivatedBySecondInstance;

    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out _ownsMutex);
    }

    public void StartListening()
    {
        if (!_ownsMutex) return;
        _ = Task.Run(ListenLoopAsync);
    }

    public static void SignalFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            var bytes = Encoding.UTF8.GetBytes("activate");
            client.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // First instance may be exiting; ignore.
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cts.Token);
                var buffer = new byte[64];
                _ = await server.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token);
                ActivatedBySecondInstance?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200, _cts.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (_ownsMutex)
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
        }
        _mutex.Dispose();
        _cts.Dispose();
    }
}
