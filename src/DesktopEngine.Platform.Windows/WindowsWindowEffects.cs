using System;
using DesktopEngine.Platform.Abstractions;

namespace DesktopEngine.Platform.Windows;

public sealed class WindowsWindowEffects : IWindowEffects
{
    public void ApplyOverlayStyles(IntPtr hwnd)
    {
        var ex = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TOOLWINDOW
            | NativeMethods.WS_EX_NOACTIVATE
            | NativeMethods.WS_EX_TOPMOST;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        var ex = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        ex = enabled ? (ex | NativeMethods.WS_EX_TRANSPARENT)
                     : (ex & ~NativeMethods.WS_EX_TRANSPARENT);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public bool IsClickThrough(IntPtr hwnd)
    {
        var ex = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        return (ex & NativeMethods.WS_EX_TRANSPARENT) != 0;
    }
}
