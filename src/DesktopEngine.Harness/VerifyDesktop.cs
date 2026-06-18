using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;

namespace DesktopEngine.Harness;

[SupportedOSPlatform("windows")]
public static class VerifyDesktop
{
    /// <summary>
    /// Launches the victim window then the overlay (via the named pipe), injects a click on the
    /// sprite and a click on a transparent pixel, and verifies:
    ///   (1) the sprite click is captured by the overlay (HitCount increments) and does NOT reach the victim;
    ///   (2) the transparent click is NOT captured and DOES reach the victim.
    /// Returns true only if both hold. Writes a screenshot to <paramref name="screenshotPath"/>.
    /// </summary>
    public static bool Run(string hostExe, string victimExe, string screenshotPath)
    {
        // Resolve to absolute paths: Win32 CreateProcess (UseShellExecute=false) does not reliably
        // resolve relative or forward-slash paths, and the skill docs invoke this with relative paths.
        hostExe = Path.GetFullPath(hostExe);
        victimExe = Path.GetFullPath(victimExe);
        screenshotPath = Path.GetFullPath(screenshotPath);

        var pipe = "desktopengine-harness-" + Guid.NewGuid().ToString("N");
        var victimLog = Path.Combine(Path.GetTempPath(), "m0-victim.log");

        // Overlay is shown at (200,200) sized to the scene; victim covers the same rect, beneath it.
        var s = SpriteScene.Default;
        var victim = Process.Start(new ProcessStartInfo(victimExe,
            $"200 200 {s.Width} {s.Height} \"{victimLog}\"") { UseShellExecute = false })!;
        Thread.Sleep(800); // let victim window appear

        var host = Process.Start(new ProcessStartInfo(hostExe, $"--harness {pipe}") { UseShellExecute = false })!;
        var client = new HarnessClient(pipe);

        try
        {
            var before = client.GetState();               // also confirms the pipe is up
            var (sx, sy) = before.SpriteScreenCenter();
            var (tx, ty) = before.TransparentScreenPoint();

            Thread.Sleep(900); // let Avalonia paint the first GPU frame before screenshotting the live window
            ScreenCapture.CaptureRegion(before.WindowX, before.WindowY, s.Width, s.Height, screenshotPath);

            File.WriteAllText(victimLog, "");             // reset victim log
            InputInjector.ClickAt(sx, sy);                // click ON the sprite
            var afterSprite = client.GetState();
            bool spriteCaptured = afterSprite.HitCount == before.HitCount + 1;
            bool spriteLeaked = LogHasClickNear(victimLog, sx, sy, 6);

            InputInjector.ClickAt(tx, ty);                // click on TRANSPARENT pixel
            var afterTransparent = client.GetState();
            bool transparentNotCaptured = afterTransparent.HitCount == afterSprite.HitCount;
            bool transparentPassedThrough = LogHasClickNear(victimLog, tx, ty, 6);

            var pass = spriteCaptured && !spriteLeaked && transparentNotCaptured && transparentPassedThrough;
            Console.WriteLine($"spriteCaptured={spriteCaptured} spriteLeaked={spriteLeaked} " +
                              $"transparentNotCaptured={transparentNotCaptured} transparentPassedThrough={transparentPassedThrough}");
            Console.WriteLine(pass ? "VERIFY-DESKTOP: PASS" : "VERIFY-DESKTOP: FAIL");
            return pass;
        }
        finally
        {
            try { client.Quit(); } catch { /* fall through to kill */ }
            Thread.Sleep(300);
            if (!host.HasExited) host.Kill();
            if (!victim.HasExited) victim.Kill();
        }
    }

    private static bool LogHasClickNear(string logPath, int x, int y, int tol)
    {
        if (!File.Exists(logPath)) return false;
        foreach (var line in File.ReadAllLines(logPath))
        {
            var parts = line.Split(',');
            if (parts.Length != 2) continue;
            if (int.TryParse(parts[0], out var lx) && int.TryParse(parts[1], out var ly)
                && Math.Abs(lx - x) <= tol && Math.Abs(ly - y) <= tol)
                return true;
        }
        return false;
    }
}
