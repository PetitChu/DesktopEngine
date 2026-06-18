using System.Text.Json;

namespace DesktopEngine.Harness;

/// <summary>A control request sent from the harness to the engine over the named pipe.</summary>
public sealed class HarnessRequest
{
    /// <summary>One of: "ping", "get_state", "quit".</summary>
    public string Cmd { get; set; } = "";
}

/// <summary>Snapshot of engine state the harness needs to drive and assert click-through.</summary>
public sealed class EngineState
{
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public float CircleX { get; set; }
    public float CircleY { get; set; }
    public float Radius { get; set; }
    public bool ClickThroughEnabled { get; set; }
    /// <summary>Number of pointer-press events the window has captured (i.e., landed on the sprite).</summary>
    public int HitCount { get; set; }

    /// <summary>Screen-space center of the sprite (window origin + circle center).</summary>
    public (int X, int Y) SpriteScreenCenter() => (WindowX + (int)CircleX, WindowY + (int)CircleY);

    /// <summary>A screen point guaranteed to be a transparent (non-sprite) pixel: top-right inset.</summary>
    public (int X, int Y) TransparentScreenPoint() => (WindowX + (int)(CircleX + Radius + 40), WindowY + 10);
}

public static class HarnessProtocol
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
