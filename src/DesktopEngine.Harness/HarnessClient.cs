using System.IO;
using System.IO.Pipes;
using System.Text;

namespace DesktopEngine.Harness;

/// <summary>Client side of the named-pipe control channel. Opens a fresh connection per request.</summary>
public sealed class HarnessClient
{
    private readonly string _pipeName;
    public HarnessClient(string pipeName) => _pipeName = pipeName;

    public EngineState GetState(int connectTimeoutMs = 5000)
        => Request<EngineState>(new HarnessRequest { Cmd = "get_state" }, connectTimeoutMs);

    public void Quit(int connectTimeoutMs = 5000)
        => Request<object>(new HarnessRequest { Cmd = "quit" }, connectTimeoutMs);

    private T Request<T>(HarnessRequest req, int connectTimeoutMs)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
        client.Connect(connectTimeoutMs);
        // leaveOpen: true so neither wrapper closes the pipe on dispose — the `using client` owns it.
        // (Otherwise the reader's dispose closes the pipe and the writer's flush hits a closed pipe.)
        using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        writer.WriteLine(HarnessProtocol.Serialize(req));
        var line = reader.ReadLine() ?? "{}";
        return HarnessProtocol.Deserialize<T>(line);
    }
}
