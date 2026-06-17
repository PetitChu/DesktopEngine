using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Avalonia.Threading;
using DesktopEngine.Harness;

namespace DesktopEngine.Host;

/// <summary>
/// Hosts a named-pipe server answering harness requests (ping/get_state/quit). The client opens a
/// fresh connection per request, so the server loops: accept one connection, service its requests
/// until the client disconnects, then accept the next. A "quit" ends the loop and shuts the app down.
/// Line-delimited JSON. Engine state is read on the UI thread.
/// </summary>
public sealed class HarnessServer
{
    private readonly string _pipeName;
    private readonly OverlayWindow _window;
    private readonly Action _quit;

    public HarnessServer(string pipeName, OverlayWindow window, Action quit)
    {
        _pipeName = pipeName; _window = window; _quit = quit;
    }

    public void Start() => Task.Run(ServeLoopAsync);

    private async Task ServeLoopAsync()
    {
        try
        {
            while (true)
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                var quitRequested = await HandleConnectionAsync(server);
                if (quitRequested) return;
            }
        }
        catch (Exception ex)
        {
            // A silently dead harness server is the worst outcome for an automated proof.
            // Surface it so Task 8 gets a diagnosable failure instead of a hang/timeout.
            Console.Error.WriteLine($"[HarnessServer] fatal: {ex}");
        }
    }

    /// <summary>Services one client connection. Returns true if the client asked the app to quit.</summary>
    private async Task<bool> HandleConnectionAsync(NamedPipeServerStream server)
    {
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            HarnessRequest req;
            try { req = HarnessProtocol.Deserialize<HarnessRequest>(line); }
            catch
            {
                await writer.WriteLineAsync(HarnessProtocol.Serialize(new { ok = false, error = "malformed" }));
                continue;
            }

            switch (req.Cmd)
            {
                case "ping":
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(new { ok = true }));
                    break;
                case "get_state":
                    var state = await Dispatcher.UIThread.InvokeAsync(ReadState);
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(state));
                    break;
                case "quit":
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(new { ok = true }));
                    Dispatcher.UIThread.Post(_quit);
                    return true;
                default:
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(new { ok = false, error = "unknown cmd" }));
                    break;
            }
        }
        return false;
    }

    private EngineState ReadState()
    {
        var (x, y) = _window.Origin;
        return new EngineState
        {
            WindowX = x, WindowY = y,
            CircleX = SpriteScene.Default.CircleX,
            CircleY = SpriteScene.Default.CircleY,
            Radius = SpriteScene.Default.Radius,
            ClickThroughEnabled = _window.ClickThroughEnabled,
            HitCount = _window.HitCount,
        };
    }
}
