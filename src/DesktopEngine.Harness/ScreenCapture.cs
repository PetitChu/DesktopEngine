using System.Drawing;             // System.Drawing.Common, Windows-only
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace DesktopEngine.Harness;

[SupportedOSPlatform("windows")]
public static class ScreenCapture
{
    /// <summary>Capture a screen rectangle to a PNG file.</summary>
    public static void CaptureRegion(int x, int y, int width, int height, string pngPath)
    {
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        bmp.Save(pngPath, ImageFormat.Png);
    }
}
