using System.IO;
using System.IO.Pipes;

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
        using var writer = new StreamWriter(client) { AutoFlush = true };
        using var reader = new StreamReader(client);
        writer.WriteLine(HarnessProtocol.Serialize(req));
        var line = reader.ReadLine() ?? "{}";
        return HarnessProtocol.Deserialize<T>(line);
    }
}
