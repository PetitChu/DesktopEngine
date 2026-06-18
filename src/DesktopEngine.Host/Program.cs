using System;
using Avalonia;
using DesktopEngine.Harness;

namespace DesktopEngine.Host;

internal static class Program
{
    /// <summary>Named-pipe name when launched with --harness; null otherwise. Read by App.</summary>
    public static string? HarnessPipe { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        // --headless <out.png>: render the scene with no window and exit (CPU raster, no Avalonia).
        var headlessIdx = Array.IndexOf(args, "--headless");
        if (headlessIdx >= 0 && headlessIdx + 1 < args.Length)
        {
            HeadlessRenderer.RenderToPng(SpriteScene.Default, args[headlessIdx + 1]);
            return 0;
        }

        // --harness <pipeName>: open the overlay window AND a control pipe for the harness.
        var harnessIdx = Array.IndexOf(args, "--harness");
        if (harnessIdx >= 0 && harnessIdx + 1 < args.Length)
            HarnessPipe = args[harnessIdx + 1];

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
