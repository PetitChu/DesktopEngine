using System;
using SkiaSharp;

namespace DesktopEngine.Harness;

/// <summary>Perceptual-tolerance image comparison for golden/regression checks.</summary>
public static class ImageDiff
{
    /// <summary>
    /// Fraction (0..1) of pixels whose max per-channel (R/G/B/A) difference exceeds
    /// <paramref name="channelTolerance"/>. Mismatched dimensions return 1.0.
    /// </summary>
    public static double FractionDiffering(SKBitmap a, SKBitmap b, int channelTolerance)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return 1.0;

        long differing = 0;
        long total = (long)a.Width * a.Height;
        for (var y = 0; y < a.Height; y++)
        for (var x = 0; x < a.Width; x++)
        {
            var pa = a.GetPixel(x, y);
            var pb = b.GetPixel(x, y);
            var d = Math.Max(Math.Max(Math.Abs(pa.Red - pb.Red), Math.Abs(pa.Green - pb.Green)),
                             Math.Max(Math.Abs(pa.Blue - pb.Blue), Math.Abs(pa.Alpha - pb.Alpha)));
            if (d > channelTolerance) differing++;
        }
        return total == 0 ? 0.0 : (double)differing / total;
    }

    /// <summary>Convenience overload that loads two PNG files from disk.</summary>
    public static double FractionDiffering(string pathA, string pathB, int channelTolerance)
    {
        using var a = SKBitmap.Decode(pathA);
        using var b = SKBitmap.Decode(pathB);
        if (a is null || b is null) return 1.0;
        return FractionDiffering(a, b, channelTolerance);
    }
}
