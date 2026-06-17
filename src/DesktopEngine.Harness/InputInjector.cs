using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace DesktopEngine.Harness;

[SupportedOSPlatform("windows")]
public static class InputInjector
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
    private const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;

    /// <summary>
    /// Move the cursor to (x,y), wait for the engine's click-through poll to settle, then left-click.
    /// The settle delay must exceed the ClickThroughPump interval (16 ms) — default 80 ms is safe.
    /// </summary>
    public static void ClickAt(int x, int y, int settleMs = 80)
    {
        SetCursorPos(x, y);
        Thread.Sleep(settleMs);
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
    }
}
