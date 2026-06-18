namespace DesktopEngine.Harness;

/// <summary>
/// The canonical M0 scene: a transparent canvas of <see cref="Width"/> x <see cref="Height"/>
/// with one opaque circle (the "sprite") centered at (<see cref="CircleX"/>, <see cref="CircleY"/>)
/// with radius <see cref="Radius"/>. Shared by the headless renderer and the live control so a
/// click on the circle is "over the sprite" and anywhere else is transparent.
/// </summary>
public sealed record SpriteScene(
    int Width = 400,
    int Height = 300,
    float CircleX = 120,
    float CircleY = 150,
    float Radius = 60)
{
    public static SpriteScene Default { get; } = new();

    /// <summary>True if a canvas-local point falls on an opaque sprite pixel.</summary>
    public bool IsOverSprite(double localX, double localY)
    {
        var dx = localX - CircleX;
        var dy = localY - CircleY;
        return dx * dx + dy * dy <= Radius * (double)Radius;
    }
}
