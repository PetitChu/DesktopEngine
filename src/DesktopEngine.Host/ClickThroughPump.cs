using System;
using Avalonia.Threading;
using DesktopEngine.Harness;
using DesktopEngine.Platform.Abstractions;
using DesktopEngine.Platform.Windows;

namespace DesktopEngine.Host;

/// <summary>
/// Chosen click-through mechanism for M0: a 60 Hz DispatcherTimer polls the cursor position;
/// when the cursor is over an opaque sprite pixel, click-through is disabled (window captures the
/// click); otherwise it is enabled (click passes through). Simpler and more Avalonia-friendly than
/// a low-level mouse hook; latency is one poll (~16 ms), which the verify-desktop skill accounts for.
/// </summary>
public sealed class ClickThroughPump : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly IWindowEffects _fx;
    private readonly SpriteScene _scene;
    private readonly Func<(int X, int Y)> _windowOrigin;
    private readonly DispatcherTimer _timer;

    public bool ClickThroughEnabled { get; private set; } = true;

    public ClickThroughPump(IntPtr hwnd, IWindowEffects fx, SpriteScene scene, Func<(int X, int Y)> windowOrigin)
    {
        _hwnd = hwnd; _fx = fx; _scene = scene; _windowOrigin = windowOrigin;
        _fx.SetClickThrough(_hwnd, true); // start fully click-through
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Pump();
        _timer.Start();
    }

    private void Pump()
    {
        if (!NativeMethods_GetCursorPos(out var px, out var py)) return;
        var (ox, oy) = _windowOrigin();
        var overSprite = _scene.IsOverSprite(px - ox, py - oy);
        var wantClickThrough = !overSprite;
        if (wantClickThrough != ClickThroughEnabled)
        {
            _fx.SetClickThrough(_hwnd, wantClickThrough);
            ClickThroughEnabled = wantClickThrough;
        }
    }

    public void Dispose() => _timer.Stop();

    // GetCursorPos is in Platform.Windows.NativeMethods which is INTERNAL to that assembly.
    // To avoid exposing it, P/Invoke it locally here.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    private static bool NativeMethods_GetCursorPos(out int x, out int y)
    {
        var ok = GetCursorPos(out var p); x = p.X; y = p.Y; return ok;
    }
}
