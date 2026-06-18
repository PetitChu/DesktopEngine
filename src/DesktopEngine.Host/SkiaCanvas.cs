using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using DesktopEngine.Harness;
using SkiaSharp;

namespace DesktopEngine.Host;

/// <summary>Draws the shared <see cref="SpriteScene"/> directly with SkiaSharp on the GPU surface.</summary>
public sealed class SkiaCanvas : Control
{
    private readonly SpriteScene _scene;
    public SkiaCanvas(SpriteScene scene) => _scene = scene;

    public override void Render(DrawingContext context)
        => context.Custom(new SceneDrawOp(new Rect(0, 0, _scene.Width, _scene.Height), _scene));

    private sealed class SceneDrawOp : ICustomDrawOperation
    {
        private readonly SpriteScene _scene;
        public SceneDrawOp(Rect bounds, SpriteScene scene) { Bounds = bounds; _scene = scene; }
        public Rect Bounds { get; }
        public bool HitTest(Point p) => false; // hit-testing handled by ClickThroughPump, not Avalonia
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (lease is null) return;
            using var l = lease.Lease();
            HeadlessRenderer.Draw(l.SkCanvas, _scene); // same draw routine as headless => goldens match
        }
    }
}
