using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DesktopEngine.Harness;
using DesktopEngine.Platform.Abstractions;

namespace DesktopEngine.Host;

public sealed class OverlayWindow : Window
{
    private readonly IWindowEffects _fx;
    private readonly SpriteScene _scene;
    private ClickThroughPump? _pump;

    public int HitCount { get; private set; }
    public bool ClickThroughEnabled => _pump?.ClickThroughEnabled ?? true;

    public OverlayWindow(IWindowEffects fx, SpriteScene scene)
    {
        _fx = fx; _scene = scene;

        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        Width = scene.Width;
        Height = scene.Height;
        Position = new PixelPoint(200, 200); // fixed, known position for the harness
        Content = new SkiaCanvas(scene);

        PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => HitCount++;

    public (int X, int Y) Origin => (Position.X, Position.Y);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var handle = TryGetPlatformHandle();
        if (handle is null || handle.HandleDescriptor != "HWND")
            throw new InvalidOperationException($"Expected an HWND, got {handle?.HandleDescriptor ?? "null"}");
        var hwnd = handle.Handle;
        _fx.ApplyOverlayStyles(hwnd);
        _pump = new ClickThroughPump(hwnd, _fx, _scene, () => Origin);
    }

    protected override void OnClosed(EventArgs e)
    {
        _pump?.Dispose();
        base.OnClosed(e);
    }
}
