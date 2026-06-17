using System;

namespace DesktopEngine.Platform.Abstractions;

/// <summary>
/// OS window behaviors the engine needs but Core must never call directly. M0 surface only;
/// M1 expands this (tray, hit-test regions, etc.). All methods take the native window handle.
/// </summary>
public interface IWindowEffects
{
    /// <summary>Apply frameless/transparent/topmost/no-activate/tool-window extended styles.</summary>
    void ApplyOverlayStyles(IntPtr hwnd);

    /// <summary>
    /// Enable or disable whole-window click-through (WS_EX_TRANSPARENT). When enabled, clicks pass
    /// through to the window beneath; when disabled, the window captures clicks.
    /// </summary>
    void SetClickThrough(IntPtr hwnd, bool enabled);

    /// <summary>Returns true if click-through (WS_EX_TRANSPARENT) is currently set on the window.</summary>
    bool IsClickThrough(IntPtr hwnd);
}
