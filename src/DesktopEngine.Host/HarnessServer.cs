using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Avalonia.Threading;
using DesktopEngine.Harness;

namespace DesktopEngine.Host;

/// <summary>
/// Hosts a named-pipe server that answers harness requests (ping/get_state/quit). One client,
/// line-delimited JSON. Runs on a background thread; reads engine state on the UI thread.
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

    public void Start() => Task.Run(ServeAsync);

    private async Task ServeAsync()
    {
        using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync();
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            HarnessRequest req;
            try { req = HarnessProtocol.Deserialize<HarnessRequest>(line); }
            catch { continue; }

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
                    return;
            }
        }
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
