using System.IO;
using SkiaSharp;

namespace DesktopEngine.Harness;

/// <summary>Deterministic CPU-raster rendering of a <see cref="SpriteScene"/> to a PNG file.</summary>
public static class HeadlessRenderer
{
    public static void RenderToPng(SpriteScene scene, string path)
    {
        var info = new SKImageInfo(scene.Width, scene.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        Draw(surface.Canvas, scene);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    /// <summary>Shared drawing routine — the live Avalonia control calls this too.</summary>
    public static void Draw(SKCanvas canvas, SpriteScene scene)
    {
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint
        {
            Color = SKColors.OrangeRed,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(scene.CircleX, scene.CircleY, scene.Radius, paint);
    }
}
