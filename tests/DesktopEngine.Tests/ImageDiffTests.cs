using DesktopEngine.Harness;
using SkiaSharp;
using Xunit;

public class ImageDiffTests
{
    private static SKBitmap Solid(int w, int h, SKColor c)
    {
        var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(c);
        return bmp;
    }

    [Fact]
    public void Identical_images_have_zero_diff_fraction()
    {
        using var a = Solid(10, 10, SKColors.Red);
        using var b = Solid(10, 10, SKColors.Red);
        Assert.Equal(0.0, ImageDiff.FractionDiffering(a, b, channelTolerance: 8));
    }

    [Fact]
    public void Fully_different_images_have_full_diff_fraction()
    {
        using var a = Solid(10, 10, SKColors.Red);
        using var b = Solid(10, 10, SKColors.Blue);
        Assert.Equal(1.0, ImageDiff.FractionDiffering(a, b, channelTolerance: 8));
    }

    [Fact]
    public void Mismatched_dimensions_count_as_fully_different()
    {
        using var a = Solid(10, 10, SKColors.Red);
        using var b = Solid(20, 10, SKColors.Red);
        Assert.Equal(1.0, ImageDiff.FractionDiffering(a, b, channelTolerance: 8));
    }
}
