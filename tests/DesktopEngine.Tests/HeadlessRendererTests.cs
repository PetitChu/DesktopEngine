using System.IO;
using DesktopEngine.Harness;
using SkiaSharp;
using Xunit;

public class HeadlessRendererTests
{
    [Fact]
    public void Renders_scene_to_png_with_opaque_circle_and_transparent_corner()
    {
        var scene = SpriteScene.Default;
        var path = Path.Combine(Path.GetTempPath(), "m0-headless.png");
        HeadlessRenderer.RenderToPng(scene, path);

        Assert.True(File.Exists(path));
        using var bmp = SKBitmap.Decode(path);
        Assert.Equal(scene.Width, bmp.Width);
        Assert.Equal(scene.Height, bmp.Height);

        // Circle center is opaque; top-right corner is transparent.
        Assert.True(bmp.GetPixel((int)scene.CircleX, (int)scene.CircleY).Alpha > 250);
        Assert.True(bmp.GetPixel(scene.Width - 1, 0).Alpha < 5);
    }
}
