# Desktop Engine — M0: De-risk & Skeleton (the Gate) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove on real Windows that the engine's make-or-break behaviors work — a transparent, frameless, always-on-top GPU window that draws a sprite, with per-pixel click-through (clicks on the sprite are captured, clicks on transparent pixels pass through to the window beneath) — plus a sandboxed MoonSharp runtime and the AI harness needed to verify all of it; then decide at an explicit STOP-gate whether to proceed to M1.

**Architecture:** A minimal slice of the full repo. Pure-logic units (MoonSharp sandbox, headless Skia render-to-PNG, image diff, RPC protocol) are built with strict TDD and xUnit. The OS/visual behaviors (transparency, click-through) **cannot** be unit-tested — they are proven by *runtime acceptance checks* driven by the harness: launch the real app, drive synthetic input, capture screenshots, and read a "victim window" click log. This split is deliberate and matches the spec's dual-mode verification and the project rule "runtime data is ground truth." The chosen click-through mechanism is **layered window + WS_EX_TRANSPARENT toggled by cursor-over-sprite polling**; two alternatives (low-level mouse hook, `WM_NCHITTEST`) are documented as fallbacks evaluated in the decision record.

**Tech Stack:** .NET 8, Avalonia 11.2.x (+ Avalonia.Skia), SkiaSharp 2.88.x, MoonSharp 2.0.0, xUnit, System.Drawing.Common (Windows-only harness capture), WinForms (victim-window tool). Win32 via P/Invoke. Windows-first; cross-platform projects keep `net8.0`, Windows-bound ones use `net8.0-windows`.

**Scope guard (what M0 is NOT):** No game loop, no ECS, no power modes, no content-pack format, no save system, no DI/factory platform selection. Those are M1+. M0 builds only what's needed to clear the gate and stand up the harness.

---

## Reference: target file structure for M0

```
DesktopEngine.sln
src/
  DesktopEngine.Platform.Abstractions/   # IWindowEffects (net8.0)
  DesktopEngine.Scripting/               # SandboxedScriptHost over MoonSharp (net8.0)
  DesktopEngine.Platform.Windows/        # WindowsWindowEffects + ClickThroughController (net8.0-windows)
  DesktopEngine.Host/                    # Avalonia app: transparent window, SkiaCanvas, CLI, RPC server (net8.0-windows)
  DesktopEngine.Harness/                 # headless render, image diff, screen capture, input injection, RPC client (net8.0-windows)
tools/
  HarnessVictim/                         # WinForms exe that logs received clicks (net8.0-windows)
tests/
  DesktopEngine.Tests/                   # xUnit (net8.0-windows)
.claude/
  CLAUDE.md                              # how to run the harness modes + M0 conventions
  skills/desktop-engine/run/SKILL.md
  skills/desktop-engine/verify-desktop/SKILL.md
docs/
  decisions/0001-clickthrough-mechanism.md   # written at the gate (Task 10)
```

---

## Task 0: Solution & project skeleton

**Files:**
- Create: `DesktopEngine.sln`
- Create: `src/DesktopEngine.Platform.Abstractions/DesktopEngine.Platform.Abstractions.csproj`
- Create: `src/DesktopEngine.Scripting/DesktopEngine.Scripting.csproj`
- Create: `src/DesktopEngine.Platform.Windows/DesktopEngine.Platform.Windows.csproj`
- Create: `src/DesktopEngine.Host/DesktopEngine.Host.csproj`
- Create: `src/DesktopEngine.Harness/DesktopEngine.Harness.csproj`
- Create: `tools/HarnessVictim/HarnessVictim.csproj`
- Create: `tests/DesktopEngine.Tests/DesktopEngine.Tests.csproj`

- [ ] **Step 1: Verify the .NET SDK is present**

Run: `dotnet --version`
Expected: prints `8.0.x` (or higher). If missing, install the .NET 8 SDK before continuing.

- [ ] **Step 2: Create the solution and class-library/app projects**

```bash
cd "G:/Claude/Desktop Engine"
dotnet new sln -n DesktopEngine
dotnet new classlib -n DesktopEngine.Platform.Abstractions -o src/DesktopEngine.Platform.Abstractions -f net8.0
dotnet new classlib -n DesktopEngine.Scripting           -o src/DesktopEngine.Scripting           -f net8.0
dotnet new classlib -n DesktopEngine.Platform.Windows    -o src/DesktopEngine.Platform.Windows    -f net8.0-windows
dotnet new classlib -n DesktopEngine.Harness             -o src/DesktopEngine.Harness             -f net8.0-windows
dotnet new console  -n DesktopEngine.Host                -o src/DesktopEngine.Host                -f net8.0-windows
dotnet new winforms -n HarnessVictim                     -o tools/HarnessVictim                   -f net8.0-windows
dotnet new xunit    -n DesktopEngine.Tests               -o tests/DesktopEngine.Tests             -f net8.0-windows
```

- [ ] **Step 3: Delete the default `Class1.cs` / template stub files**

```bash
rm -f src/DesktopEngine.Platform.Abstractions/Class1.cs
rm -f src/DesktopEngine.Scripting/Class1.cs
rm -f src/DesktopEngine.Platform.Windows/Class1.cs
rm -f src/DesktopEngine.Harness/Class1.cs
```

- [ ] **Step 4: Add all projects to the solution**

```bash
dotnet sln add src/DesktopEngine.Platform.Abstractions src/DesktopEngine.Scripting src/DesktopEngine.Platform.Windows src/DesktopEngine.Harness src/DesktopEngine.Host
dotnet sln add tools/HarnessVictim tests/DesktopEngine.Tests
```

- [ ] **Step 5: Wire project references**

```bash
# Windows platform impl depends on the abstractions
dotnet add src/DesktopEngine.Platform.Windows reference src/DesktopEngine.Platform.Abstractions
# Host depends on abstractions, windows impl, scripting
dotnet add src/DesktopEngine.Host reference src/DesktopEngine.Platform.Abstractions src/DesktopEngine.Platform.Windows src/DesktopEngine.Scripting
# Tests reference everything they assert on
dotnet add tests/DesktopEngine.Tests reference src/DesktopEngine.Scripting src/DesktopEngine.Harness src/DesktopEngine.Platform.Abstractions
```

- [ ] **Step 6: Build the empty solution**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors (warnings about empty assemblies are fine).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore(m0): scaffold solution and project skeleton"
```

---

## Task 1: MoonSharp sandbox (Spike 3) — fully TDD

**Files:**
- Modify: `src/DesktopEngine.Scripting/DesktopEngine.Scripting.csproj` (add MoonSharp package)
- Create: `src/DesktopEngine.Scripting/SandboxedScriptHost.cs`
- Test: `tests/DesktopEngine.Tests/SandboxedScriptHostTests.cs`

- [ ] **Step 1: Add the MoonSharp package**

```bash
dotnet add src/DesktopEngine.Scripting package MoonSharp --version 2.0.0
```

- [ ] **Step 2: Write the failing tests**

Create `tests/DesktopEngine.Tests/SandboxedScriptHostTests.cs`:

```csharp
using DesktopEngine.Scripting;
using Xunit;

public class SandboxedScriptHostTests
{
    [Fact]
    public void Runs_basic_arithmetic()
    {
        var host = new SandboxedScriptHost();
        var result = host.Run("return 1 + 1");
        Assert.Equal(2, (int)result.Number);
    }

    [Fact]
    public void Exposes_stub_engine_spawn_to_lua()
    {
        var host = new SandboxedScriptHost();
        host.Run("Engine.spawn('fish'); Engine.spawn('bubble')");
        Assert.Equal(new[] { "fish", "bubble" }, host.Spawned);
    }

    [Theory]
    [InlineData("return os.execute")]   // dangerous os.* must be absent
    [InlineData("return io")]           // io module must be absent
    [InlineData("return require")]      // module loading must be absent
    [InlineData("return load")]         // arbitrary code loading must be absent
    [InlineData("return dofile")]       // file loading must be absent
    public void Dangerous_globals_are_nil(string code)
    {
        var host = new SandboxedScriptHost();
        var result = host.Run(code);
        Assert.True(result.IsNil(), $"Expected nil for `{code}` but got {result.Type}");
    }

    [Fact]
    public void Safe_modules_are_available()
    {
        var host = new SandboxedScriptHost();
        Assert.Equal(4.0, host.Run("return math.sqrt(16)").Number);
        Assert.Equal("HI", host.Run("return string.upper('hi')").String);
        Assert.Equal(3, (int)host.Run("local c=0; for _ in pairs({1,2,3}) do c=c+1 end; return c").Number);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter SandboxedScriptHostTests`
Expected: FAIL — `SandboxedScriptHost` does not exist (compile error).

- [ ] **Step 4: Implement `SandboxedScriptHost`**

Create `src/DesktopEngine.Scripting/SandboxedScriptHost.cs`:

```csharp
using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;

namespace DesktopEngine.Scripting;

/// <summary>
/// Wraps a MoonSharp <see cref="Script"/> configured as a soft sandbox: Basic, String,
/// Table, Math, Coroutine, Json, Metatables and os.time are available; io, os.execute,
/// require, load/loadstring and dofile are not. Exposes a minimal stub Engine API so the
/// M0 spike can prove scripts can drive engine state without OS access.
/// </summary>
public sealed class SandboxedScriptHost
{
    private readonly Script _script;
    private readonly List<string> _spawned = new();

    public SandboxedScriptHost()
    {
        _script = new Script(CoreModules.Preset_SoftSandbox);

        var engine = new Table(_script);
        engine["version"] = "0.0.1-m0";
        engine["spawn"] = (Func<string, string>)(name =>
        {
            _spawned.Add(name);
            return name;
        });
        _script.Globals["Engine"] = engine;
    }

    /// <summary>Names passed to Engine.spawn, in call order.</summary>
    public IReadOnlyList<string> Spawned => _spawned;

    /// <summary>Runs Lua source and returns its result. Throws ScriptRuntimeException on Lua errors.</summary>
    public DynValue Run(string luaSource) => _script.DoString(luaSource);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter SandboxedScriptHostTests`
Expected: PASS — all 9 cases green.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(m0): sandboxed MoonSharp script host (Spike 3) with restricted stdlib"
```

---

## Task 2: Headless Skia render-to-PNG + image diff (harness foundation) — TDD

**Files:**
- Modify: `src/DesktopEngine.Harness/DesktopEngine.Harness.csproj` (add SkiaSharp)
- Create: `src/DesktopEngine.Harness/SpriteScene.cs`
- Create: `src/DesktopEngine.Harness/HeadlessRenderer.cs`
- Create: `src/DesktopEngine.Harness/ImageDiff.cs`
- Test: `tests/DesktopEngine.Tests/HeadlessRendererTests.cs`
- Test: `tests/DesktopEngine.Tests/ImageDiffTests.cs`

This task creates the single shared definition of "the M0 scene" (a transparent canvas with one opaque circle = the sprite). Both the headless renderer and the live Avalonia control draw from this same definition so headless goldens stay meaningful.

- [ ] **Step 1: Add SkiaSharp to the harness**

```bash
dotnet add src/DesktopEngine.Harness package SkiaSharp --version 2.88.8
```

- [ ] **Step 2: Create the shared scene definition**

Create `src/DesktopEngine.Harness/SpriteScene.cs`:

```csharp
namespace DesktopEngine.Harness;

/// <summary>
/// The canonical M0 scene: a transparent canvas of <see cref="Width"/> x <see cref="Height"/>
/// with one opaque circle (the "sprite") centered at (<see cref="CircleX"/>, <see cref="CircleY"/>)
/// with radius <see cref="Radius"/>. Shared by the headless renderer and the live control so a
/// click on the circle is "over the sprite" and anywhere else is transparent.
/// </summary>
public sealed record SpriteScene(
    int Width = 400,
    int Height = 300,
    float CircleX = 120,
    float CircleY = 150,
    float Radius = 60)
{
    public static SpriteScene Default { get; } = new();

    /// <summary>True if a canvas-local point falls on an opaque sprite pixel.</summary>
    public bool IsOverSprite(double localX, double localY)
    {
        var dx = localX - CircleX;
        var dy = localY - CircleY;
        return dx * dx + dy * dy <= Radius * (double)Radius;
    }
}
```

- [ ] **Step 3: Write the failing headless-render test**

Create `tests/DesktopEngine.Tests/HeadlessRendererTests.cs`:

```csharp
using System.IO;
using DesktopEngine.Harness;
using SkiaSharp;
using Xunit;

public class HeadlessRendererTests
{
    [Fact]
    public void Renders_scene_to_png_with_opaque_circle_and_transparent_corner()
    {
        var scene = SpriteScene.Default;
        var path = Path.Combine(Path.GetTempPath(), "m0-headless.png");
        HeadlessRenderer.RenderToPng(scene, path);

        Assert.True(File.Exists(path));
        using var bmp = SKBitmap.Decode(path);
        Assert.Equal(scene.Width, bmp.Width);
        Assert.Equal(scene.Height, bmp.Height);

        // Circle center is opaque; top-right corner is transparent.
        Assert.True(bmp.GetPixel((int)scene.CircleX, (int)scene.CircleY).Alpha > 250);
        Assert.True(bmp.GetPixel(scene.Width - 1, 0).Alpha < 5);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test --filter HeadlessRendererTests`
Expected: FAIL — `HeadlessRenderer` does not exist.

- [ ] **Step 5: Implement the headless renderer (CPU raster for byte-stable output)**

Create `src/DesktopEngine.Harness/HeadlessRenderer.cs`:

```csharp
using System.IO;
using SkiaSharp;

namespace DesktopEngine.Harness;

/// <summary>Deterministic CPU-raster rendering of a <see cref="SpriteScene"/> to a PNG file.</summary>
public static class HeadlessRenderer
{
    public static void RenderToPng(SpriteScene scene, string path)
    {
        var info = new SKImageInfo(scene.Width, scene.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        Draw(surface.Canvas, scene);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    /// <summary>Shared drawing routine — the live Avalonia control calls this too.</summary>
    public static void Draw(SKCanvas canvas, SpriteScene scene)
    {
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint
        {
            Color = SKColors.OrangeRed,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(scene.CircleX, scene.CircleY, scene.Radius, paint);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test --filter HeadlessRendererTests`
Expected: PASS.

- [ ] **Step 7: Write the failing image-diff tests**

Create `tests/DesktopEngine.Tests/ImageDiffTests.cs`:

```csharp
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
```

- [ ] **Step 8: Run the tests to verify they fail**

Run: `dotnet test --filter ImageDiffTests`
Expected: FAIL — `ImageDiff` does not exist.

- [ ] **Step 9: Implement the image diff (tolerant per-pixel comparison)**

Create `src/DesktopEngine.Harness/ImageDiff.cs`:

```csharp
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
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test --filter ImageDiffTests`
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat(m0): headless Skia render-to-PNG + tolerant image diff (harness foundation)"
```

---

## Task 3: Engine state model + named-pipe control channel — TDD for the protocol

**Files:**
- Create: `src/DesktopEngine.Harness/HarnessProtocol.cs` (shared DTOs + JSON helpers)
- Test: `tests/DesktopEngine.Tests/HarnessProtocolTests.cs`

The control channel is line-delimited JSON over a named pipe. The engine hosts the server (Task 5); the harness hosts the client (Task 6). Both share the DTOs here. Only the *serialization* is unit-testable; the pipe wiring is exercised by the runtime checks in Task 9.

- [ ] **Step 1: Write the failing protocol round-trip tests**

Create `tests/DesktopEngine.Tests/HarnessProtocolTests.cs`:

```csharp
using DesktopEngine.Harness;
using Xunit;

public class HarnessProtocolTests
{
    [Fact]
    public void State_round_trips_through_json()
    {
        var state = new EngineState
        {
            WindowX = 100, WindowY = 50,
            CircleX = 120, CircleY = 150, Radius = 60,
            ClickThroughEnabled = true,
            HitCount = 2,
        };
        var json = HarnessProtocol.Serialize(state);
        var back = HarnessProtocol.Deserialize<EngineState>(json);

        Assert.Equal(100, back.WindowX);
        Assert.Equal(120, back.CircleX);
        Assert.Equal(2, back.HitCount);
        Assert.True(back.ClickThroughEnabled);
    }

    [Fact]
    public void Request_round_trips_through_json()
    {
        var req = new HarnessRequest { Cmd = "get_state" };
        var back = HarnessProtocol.Deserialize<HarnessRequest>(HarnessProtocol.Serialize(req));
        Assert.Equal("get_state", back.Cmd);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter HarnessProtocolTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the protocol DTOs and JSON helpers**

Create `src/DesktopEngine.Harness/HarnessProtocol.cs`:

```csharp
using System.Text.Json;

namespace DesktopEngine.Harness;

/// <summary>A control request sent from the harness to the engine over the named pipe.</summary>
public sealed class HarnessRequest
{
    /// <summary>One of: "ping", "get_state", "quit".</summary>
    public string Cmd { get; set; } = "";
}

/// <summary>Snapshot of engine state the harness needs to drive and assert click-through.</summary>
public sealed class EngineState
{
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public float CircleX { get; set; }
    public float CircleY { get; set; }
    public float Radius { get; set; }
    public bool ClickThroughEnabled { get; set; }
    /// <summary>Number of pointer-press events the window has captured (i.e., landed on the sprite).</summary>
    public int HitCount { get; set; }

    /// <summary>Screen-space center of the sprite (window origin + circle center).</summary>
    public (int X, int Y) SpriteScreenCenter() => (WindowX + (int)CircleX, WindowY + (int)CircleY);

    /// <summary>A screen point guaranteed to be a transparent (non-sprite) pixel: top-right inset.</summary>
    public (int X, int Y) TransparentScreenPoint() => (WindowX + (int)(CircleX + Radius + 40), WindowY + 10);
}

public static class HarnessProtocol
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter HarnessProtocolTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(m0): harness control protocol DTOs + JSON serialization"
```

---

## Task 4: Win32 window effects + click-through controller (Spike 2 core code)

**Files:**
- Create: `src/DesktopEngine.Platform.Abstractions/IWindowEffects.cs`
- Create: `src/DesktopEngine.Platform.Windows/NativeMethods.cs`
- Create: `src/DesktopEngine.Platform.Windows/WindowsWindowEffects.cs`
- Test: `tests/DesktopEngine.Tests/WindowEffectsContractTests.cs`

No UI here — just the interface and the Win32 implementation. The runtime proof comes in Task 9. We unit-test only the contract shape that doesn't require a window.

- [ ] **Step 1: Define the minimal platform interface**

Create `src/DesktopEngine.Platform.Abstractions/IWindowEffects.cs`:

```csharp
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
```

- [ ] **Step 2: Implement the Win32 P/Invoke surface**

Create `src/DesktopEngine.Platform.Windows/NativeMethods.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace DesktopEngine.Platform.Windows;

internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    public const long WS_EX_LAYERED     = 0x00080000;
    public const long WS_EX_TRANSPARENT = 0x00000020;
    public const long WS_EX_TOOLWINDOW  = 0x00000080; // hide from Alt-Tab
    public const long WS_EX_NOACTIVATE  = 0x08000000; // don't steal focus
    public const long WS_EX_TOPMOST     = 0x00000008;

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP   = 0x0004;

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
}
```

- [ ] **Step 3: Implement `WindowsWindowEffects`**

Create `src/DesktopEngine.Platform.Windows/WindowsWindowEffects.cs`:

```csharp
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
```

- [ ] **Step 4: Write a contract test (zero-handle guard, no real window)**

Create `tests/DesktopEngine.Tests/WindowEffectsContractTests.cs`:

```csharp
using System;
using DesktopEngine.Platform.Windows;
using Xunit;

public class WindowEffectsContractTests
{
    // With IntPtr.Zero (no window) the API must not throw; GetWindowLongPtr returns 0,
    // so click-through reads as false. This guards the bit-manipulation logic.
    [Fact]
    public void IsClickThrough_on_null_handle_is_false_and_does_not_throw()
    {
        var fx = new WindowsWindowEffects();
        Assert.False(fx.IsClickThrough(IntPtr.Zero));
    }
}
```

- [ ] **Step 5: Run the test to verify it passes (after implementing — type now exists)**

Run: `dotnet test --filter WindowEffectsContractTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(m0): IWindowEffects + Win32 overlay styles & click-through toggle (Spike 2 core)"
```

---

## Task 5: Avalonia transparent overlay window + SkiaCanvas + RPC server (Spikes 1 & 2 host)

**Files:**
- Modify: `src/DesktopEngine.Host/DesktopEngine.Host.csproj` (Avalonia packages, harness ref, OutputType)
- Create: `src/DesktopEngine.Host/App.axaml`
- Create: `src/DesktopEngine.Host/App.axaml.cs`
- Create: `src/DesktopEngine.Host/SkiaCanvas.cs`
- Create: `src/DesktopEngine.Host/OverlayWindow.cs`
- Create: `src/DesktopEngine.Host/ClickThroughPump.cs`
- Create: `src/DesktopEngine.Host/HarnessServer.cs`
- Create: `src/DesktopEngine.Host/Program.cs` (replace template)

- [ ] **Step 1: Configure the Host project**

Replace `src/DesktopEngine.Host/DesktopEngine.Host.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.3" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.3" />
    <PackageReference Include="Avalonia.Skia" Version="11.2.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DesktopEngine.Platform.Abstractions\DesktopEngine.Platform.Abstractions.csproj" />
    <ProjectReference Include="..\DesktopEngine.Platform.Windows\DesktopEngine.Platform.Windows.csproj" />
    <ProjectReference Include="..\DesktopEngine.Scripting\DesktopEngine.Scripting.csproj" />
    <ProjectReference Include="..\DesktopEngine.Harness\DesktopEngine.Harness.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add a DPI-aware application manifest**

Create `src/DesktopEngine.Host/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 3: Create the Avalonia App**

Create `src/DesktopEngine.Host/App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="DesktopEngine.Host.App">
</Application>
```

Create `src/DesktopEngine.Host/App.axaml.cs` (window creation belongs here — the idiomatic Avalonia 11 lifetime hook, where `ApplicationLifetime` is reliably available):

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DesktopEngine.Harness;
using DesktopEngine.Platform.Windows;

namespace DesktopEngine.Host;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new OverlayWindow(new WindowsWindowEffects(), SpriteScene.Default);
            desktop.MainWindow = window;
            if (Program.HarnessPipe is { } pipe)
                new HarnessServer(pipe, window, () => desktop.Shutdown()).Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: Create the SkiaCanvas control (draws the shared scene via GPU Skia)**

Create `src/DesktopEngine.Host/SkiaCanvas.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using DesktopEngine.Harness;
using SkiaSharp;

namespace DesktopEngine.Host;

/// <summary>Draws the shared <see cref="SpriteScene"/> directly with SkiaSharp on the GPU surface.</summary>
public sealed class SkiaCanvas : Control
{
    private readonly SpriteScene _scene;
    public SkiaCanvas(SpriteScene scene) => _scene = scene;

    public override void Render(DrawingContext context)
        => context.Custom(new SceneDrawOp(new Rect(0, 0, _scene.Width, _scene.Height), _scene));

    private sealed class SceneDrawOp : ICustomDrawOperation
    {
        private readonly SpriteScene _scene;
        public SceneDrawOp(Rect bounds, SpriteScene scene) { Bounds = bounds; _scene = scene; }
        public Rect Bounds { get; }
        public bool HitTest(Point p) => false; // hit-testing handled by ClickThroughPump, not Avalonia
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (lease is null) return;
            using var l = lease.Lease();
            HeadlessRenderer.Draw(l.SkCanvas, _scene); // same draw routine as headless => goldens match
        }
    }
}
```

- [ ] **Step 5: Create the ClickThroughPump (the chosen mechanism: poll cursor, toggle WS_EX_TRANSPARENT)**

Create `src/DesktopEngine.Host/ClickThroughPump.cs`:

```csharp
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
        if (!NativeMethods.GetCursorPos(out var p)) return;
        var (ox, oy) = _windowOrigin();
        var overSprite = _scene.IsOverSprite(p.X - ox, p.Y - oy);
        var wantClickThrough = !overSprite;
        if (wantClickThrough != ClickThroughEnabled)
        {
            _fx.SetClickThrough(_hwnd, wantClickThrough);
            ClickThroughEnabled = wantClickThrough;
        }
    }

    public void Dispose() => _timer.Stop();
}
```

- [ ] **Step 6: Create the OverlayWindow (transparent, frameless, topmost; records hits)**

Create `src/DesktopEngine.Host/OverlayWindow.cs`:

```csharp
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
```

- [ ] **Step 7: Create the HarnessServer (named-pipe, line-delimited JSON)**

Create `src/DesktopEngine.Host/HarnessServer.cs`:

```csharp
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using DesktopEngine.Harness;

namespace DesktopEngine.Host;

/// <summary>
/// Hosts a named-pipe server that answers harness requests (ping/get_state/quit). One client,
/// line-delimited JSON. Runs on a background thread; reads engine state on the UI thread.
/// </summary>
public sealed class HarnessServer
{
    private readonly string _pipeName;
    private readonly OverlayWindow _window;
    private readonly Action _quit;

    public HarnessServer(string pipeName, OverlayWindow window, Action quit)
    {
        _pipeName = pipeName; _window = window; _quit = quit;
    }

    public void Start() => Task.Run(ServeAsync);

    private async Task ServeAsync()
    {
        using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync();
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            HarnessRequest req;
            try { req = HarnessProtocol.Deserialize<HarnessRequest>(line); }
            catch { continue; }

            switch (req.Cmd)
            {
                case "ping":
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(new { ok = true }));
                    break;
                case "get_state":
                    var state = await Dispatcher.UIThread.InvokeAsync(ReadState);
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(state));
                    break;
                case "quit":
                    await writer.WriteLineAsync(HarnessProtocol.Serialize(new { ok = true }));
                    Dispatcher.UIThread.Post(_quit);
                    return;
            }
        }
    }

    private EngineState ReadState()
    {
        var (x, y) = _window.Origin;
        return new EngineState
        {
            WindowX = x, WindowY = y,
            CircleX = SpriteScene.Default.CircleX,
            CircleY = SpriteScene.Default.CircleY,
            Radius = SpriteScene.Default.Radius,
            ClickThroughEnabled = _window.ClickThroughEnabled,
            HitCount = _window.HitCount,
        };
    }
}
```

- [ ] **Step 8: Create Program.cs with CLI modes (`--harness`, `--headless`, default show)**

Replace `src/DesktopEngine.Host/Program.cs` with (parses CLI, exposes the pipe name to `App` via a static, then starts the classic desktop lifetime — `App.OnFrameworkInitializationCompleted` does the rest):

```csharp
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
```

- [ ] **Step 9: Build**

Run: `dotnet build src/DesktopEngine.Host`
Expected: `Build succeeded.`

- [ ] **Step 10: Manual smoke (human or harness, runtime check — Spike 1)**

Run: `dotnet run --project src/DesktopEngine.Host`
Expected: an orange-red circle appears floating on the desktop with **no window chrome and no opaque background rectangle** — only the circle is visible, it stays on top of other windows, and it does not appear in the taskbar or Alt-Tab. Close it with Alt+F4 (it has no title bar).

**If a gray/black rectangle shows instead of true transparency** (the P0 risk): the most likely cause is that our self-applied `WS_EX_LAYERED` conflicts with Avalonia's DirectComposition-based transparency. Try this first before declaring failure — in `WindowsWindowEffects.ApplyOverlayStyles`, drop `WS_EX_LAYERED` from the OR (keep `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`), rebuild, and re-run; on modern Windows `WS_EX_TRANSPARENT` alone (toggled by the pump) usually suffices for pass-through over a DirectComposition window. If transparency works without `WS_EX_LAYERED`, keep it removed and note it in the decision record. If neither variant yields true transparency, that is a genuine Spike-1 failure — record it for the gate (Task 10).

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat(m0): transparent topmost overlay window + GPU Skia sprite + click-through pump + RPC server (Spikes 1 & 2)"
```

---

## Task 6: Harness Win32 helpers (screen capture + input injection) + RPC client

**Files:**
- Modify: `src/DesktopEngine.Harness/DesktopEngine.Harness.csproj` (System.Drawing.Common)
- Create: `src/DesktopEngine.Harness/ScreenCapture.cs`
- Create: `src/DesktopEngine.Harness/InputInjector.cs`
- Create: `src/DesktopEngine.Harness/HarnessClient.cs`

These are Windows-only test tools (not Core), so OS calls here are allowed by the architecture rules.

- [ ] **Step 1: Add System.Drawing.Common**

```bash
dotnet add src/DesktopEngine.Harness package System.Drawing.Common --version 8.0.10
```

- [ ] **Step 2: Implement screen capture (region → PNG)**

Create `src/DesktopEngine.Harness/ScreenCapture.cs`:

```csharp
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
```

- [ ] **Step 3: Implement synthetic input injection**

Create `src/DesktopEngine.Harness/InputInjector.cs`:

```csharp
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
```

- [ ] **Step 4: Implement the RPC client**

Create `src/DesktopEngine.Harness/HarnessClient.cs`:

```csharp
using System.IO;
using System.IO.Pipes;

namespace DesktopEngine.Harness;

/// <summary>Client side of the named-pipe control channel.</summary>
public sealed class HarnessClient
{
    private readonly string _pipeName;
    public HarnessClient(string pipeName) => _pipeName = pipeName;

    public EngineState GetState(int connectTimeoutMs = 5000)
        => Request<EngineState>(new HarnessRequest { Cmd = "get_state" }, connectTimeoutMs);

    public void Quit(int connectTimeoutMs = 5000)
        => Request<object>(new HarnessRequest { Cmd = "quit" }, connectTimeoutMs);

    private T Request<T>(HarnessRequest req, int connectTimeoutMs)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
        client.Connect(connectTimeoutMs);
        using var writer = new StreamWriter(client) { AutoFlush = true };
        using var reader = new StreamReader(client);
        writer.WriteLine(HarnessProtocol.Serialize(req));
        var line = reader.ReadLine() ?? "{}";
        return HarnessProtocol.Deserialize<T>(line);
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build src/DesktopEngine.Harness`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(m0): harness screen capture, input injection, and RPC client"
```

---

## Task 7: Victim window tool (proves pass-through)

**Files:**
- Replace: `tools/HarnessVictim/Form1.cs` (and remove designer files) with a single `Program.cs`
- Modify: `tools/HarnessVictim/HarnessVictim.csproj`

The victim is a borderless WinForms window placed beneath the overlay. Every click it receives is appended (screen coords) to a log file the harness reads. A click captured by the overlay sprite will NOT appear here; a pass-through click WILL.

- [ ] **Step 1: Replace the WinForms template with a minimal logging form**

Delete `tools/HarnessVictim/Form1.cs`, `tools/HarnessVictim/Form1.Designer.cs`, `tools/HarnessVictim/Program.cs` if present, then create `tools/HarnessVictim/Program.cs`:

```csharp
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HarnessVictim;

// Usage: HarnessVictim.exe <x> <y> <width> <height> <logPath>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 5) { Console.Error.WriteLine("need: x y w h logPath"); return; }
        int x = int.Parse(args[0]), y = int.Parse(args[1]), w = int.Parse(args[2]), h = int.Parse(args[3]);
        string log = args[4];
        File.WriteAllText(log, ""); // truncate

        ApplicationConfiguration.Initialize();
        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Color.LightGreen,
            ShowInTaskbar = false,
            TopMost = false,
        };
        form.MouseDown += (_, e) =>
        {
            var sp = form.PointToScreen(e.Location);
            File.AppendAllText(log, $"{sp.X},{sp.Y}{Environment.NewLine}");
        };
        Application.Run(form);
    }
}
```

- [ ] **Step 2: Ensure the csproj is a WinForms exe**

Confirm `tools/HarnessVictim/HarnessVictim.csproj` contains:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Build**

Run: `dotnet build tools/HarnessVictim`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(m0): victim-window tool that logs received clicks for pass-through proof"
```

---

## Task 8: End-to-end click-through proof (runtime acceptance — the Spike 2 verdict)

**Files:**
- Create: `src/DesktopEngine.Harness/VerifyDesktop.cs`
- Create: `src/DesktopEngine.Harness/Program.cs` (CLI entry for the harness runner)
- Modify: `src/DesktopEngine.Harness/DesktopEngine.Harness.csproj` (make it an exe)

This is the heart of M0: an automated runtime check that launches the victim + overlay, injects two clicks, and produces a pass/fail verdict. It is NOT an xUnit test (it needs a real desktop session); it is a console runner the `verify-desktop` skill invokes.

- [ ] **Step 1: Make the harness project an exe too (library + runner)**

Edit `src/DesktopEngine.Harness/DesktopEngine.Harness.csproj` — set `<OutputType>Exe</OutputType>`. The runner launches the Host and Victim build outputs by file path via `Process.Start`, so no project reference to them is needed (which also keeps the harness free of a compile-time dependency on the GUI app). The csproj after edit:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SkiaSharp" Version="2.88.8" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.10" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Implement the verification routine**

Create `src/DesktopEngine.Harness/VerifyDesktop.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
```

- [ ] **Step 3: Implement the harness CLI runner**

Create `src/DesktopEngine.Harness/Program.cs`:

```csharp
using System;
using System.Runtime.Versioning;
using DesktopEngine.Harness;

// Usage:
//   harness headless <hostExe> <out.png>
//   harness screenshot <hostExe> <pipeName> <out.png>   (host already running not required; launches it)
//   harness verify <hostExe> <victimExe> <screenshot.png>
[SupportedOSPlatform("windows")]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("commands: headless | verify"); return 2; }
        switch (args[0])
        {
            case "verify" when args.Length == 4:
                return VerifyDesktop.Run(args[1], args[2], args[3]) ? 0 : 1;
            default:
                Console.Error.WriteLine("unknown command or wrong arg count");
                return 2;
        }
    }
}
```

- [ ] **Step 4: Build everything**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 5: Run the end-to-end click-through verification (THE Spike 2 verdict)**

```bash
dotnet build -c Debug
HOST="src/DesktopEngine.Host/bin/Debug/net8.0-windows/DesktopEngine.Host.exe"
VICTIM="tools/HarnessVictim/bin/Debug/net8.0-windows/HarnessVictim.exe"
dotnet run --project src/DesktopEngine.Harness -- verify "$HOST" "$VICTIM" "harness-out/clickthrough.png"
```

Expected: console prints `VERIFY-DESKTOP: PASS` and exit code 0, and `harness-out/clickthrough.png` shows the orange-red circle over a light-green field. **Do not move the mouse during this run** — synthetic input shares the real cursor.
If it prints `FAIL`, read the four booleans: `spriteCaptured=false` ⇒ click-through toggle/poll timing problem; `transparentPassedThrough=false` ⇒ transparency/pass-through not working ⇒ Spike-2 concern for the gate (Task 10).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(m0): end-to-end click-through verification runner (Spike 2 verdict)"
```

---

## Task 9: Harness skills + project CLAUDE.md

**Files:**
- Create: `.claude/skills/desktop-engine/run/SKILL.md`
- Create: `.claude/skills/desktop-engine/verify-desktop/SKILL.md`
- Create: `.claude/CLAUDE.md`

- [ ] **Step 1: Write the `run` skill**

Create `.claude/skills/desktop-engine/run/SKILL.md`:

```markdown
---
name: desktop-engine-run
description: Build and launch the Desktop Engine in headless (render-to-PNG) or real-window mode and capture output. Use when you need to see what the engine renders or smoke-test that it launches.
---

# desktop-engine: run

## Build
Run `dotnet build -c Debug` from the repo root. Fix any compile errors before launching.

## Headless render (fast, no window, CI-safe)
Produces a deterministic CPU-raster PNG of the current scene.

```
dotnet run --project src/DesktopEngine.Host -- --headless harness-out/headless.png
```
Then inspect `harness-out/headless.png` (use the Read tool on the image). Expect the orange-red
circle on a transparent background at the scene coordinates.

## Real-window launch (visual smoke)
```
dotnet run --project src/DesktopEngine.Host
```
A frameless, transparent, always-on-top orange-red circle should appear on the desktop with no
window chrome and no opaque background. Close with Alt+F4. If you need a screenshot programmatically,
use the verify-desktop skill (it launches with a control pipe and captures the region).

## Golden comparison
To check against a stored golden, render headless then diff with `ImageDiff.FractionDiffering`
(tolerance ~0.02). Goldens live under `tests/**/golden/`; never ignore that folder in git.
```

- [ ] **Step 2: Write the `verify-desktop` skill**

Create `.claude/skills/desktop-engine/verify-desktop/SKILL.md`:

```markdown
---
name: desktop-engine-verify-desktop
description: Prove the desktop-native behaviors (transparency, always-on-top, per-pixel click-through) on real Windows by launching a victim window beneath the overlay, injecting clicks, and checking capture vs pass-through. Use to verify the M0 gate and any change touching windowing/click-through.
---

# desktop-engine: verify-desktop

## What it proves
1. A click on an opaque sprite pixel is captured by the overlay (engine HitCount increments) and
   does NOT reach the window beneath.
2. A click on a transparent pixel is NOT captured and DOES reach the window beneath.
Both must hold for click-through to be considered working.

## Run it
This requires an interactive desktop session with a GPU (NOT a headless CI runner). Do not move
the mouse while it runs — synthetic input shares the real cursor.

```
dotnet build -c Debug
dotnet run --project src/DesktopEngine.Harness -- verify \
  "src/DesktopEngine.Host/bin/Debug/net8.0-windows/DesktopEngine.Host.exe" \
  "tools/HarnessVictim/bin/Debug/net8.0-windows/HarnessVictim.exe" \
  "harness-out/clickthrough.png"
```

## Read the result
- Exit code 0 and `VERIFY-DESKTOP: PASS` ⇒ click-through works.
- On `FAIL`, the four booleans localize the fault:
  - `spriteCaptured=false` ⇒ the WS_EX_TRANSPARENT toggle/poll didn't disable click-through in time.
  - `spriteLeaked=true` ⇒ the sprite click also hit the victim (overlay not capturing).
  - `transparentNotCaptured=false` ⇒ the overlay wrongly captured a transparent-pixel click.
  - `transparentPassedThrough=false` ⇒ transparency/pass-through not functioning.
- Always open `harness-out/clickthrough.png` to confirm the visual (circle over green field).

## CI note
Headless tests run anywhere; this skill needs a real desktop. Run it on the dev machine or a
self-hosted Windows runner, not on hosted CI.
```

- [ ] **Step 3: Write the project CLAUDE.md**

Create `.claude/CLAUDE.md`:

```markdown
# Desktop Engine — project conventions (M0)

## Architecture rules (enforced harder in M1 via architecture-guard)
- **No OS calls in Core/Engine.** All OS behavior goes through `DesktopEngine.Platform.Abstractions`
  interfaces; real impls live in `DesktopEngine.Platform.Windows`. P/Invoke, `System.Runtime.InteropServices`,
  and Win32 namespaces must not appear outside `Platform.Windows` and the harness/tools.
- `Platform.Abstractions` and `Scripting` stay `net8.0` (portable). Windows-bound projects use `net8.0-windows`.
- The harness (`DesktopEngine.Harness`) and `tools/` are test tooling — OS calls are allowed there.

## Harness modes
- Headless: `dotnet run --project src/DesktopEngine.Host -- --headless <out.png>` (CPU raster, deterministic).
- Real-window + control pipe: `--harness <pipeName>` (named-pipe JSON: ping/get_state/quit).
- Verify click-through: `dotnet run --project src/DesktopEngine.Harness -- verify <hostExe> <victimExe> <png>`.

## Skills
- `desktop-engine:run` — build + launch + capture.
- `desktop-engine:verify-desktop` — the click-through proof (needs a real desktop session).

## Testing split
- Pure logic (scripting sandbox, image diff, protocol) ⇒ xUnit, `dotnet test`.
- Desktop/visual behavior ⇒ runtime acceptance via the harness; runtime data is ground truth.
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(m0): desktop-engine run + verify-desktop skills and project CLAUDE.md"
```

---

## Task 10: STOP-gate evaluation + decision record

**Files:**
- Create: `docs/decisions/0001-clickthrough-mechanism.md`

This is the gate. Do not start M1 until this is filled in with real results from Tasks 5, 8.

- [ ] **Step 1: Run the full verification suite and collect evidence**

```bash
dotnet test
dotnet run --project src/DesktopEngine.Host -- --headless harness-out/headless.png
dotnet run --project src/DesktopEngine.Harness -- verify \
  "src/DesktopEngine.Host/bin/Debug/net8.0-windows/DesktopEngine.Host.exe" \
  "tools/HarnessVictim/bin/Debug/net8.0-windows/HarnessVictim.exe" \
  "harness-out/clickthrough.png"
```
Expected: `dotnet test` all green; headless PNG shows the transparent circle; verify prints PASS.

- [ ] **Step 2: Write the decision record (fill brackets with the ACTUAL observed results)**

Create `docs/decisions/0001-clickthrough-mechanism.md`:

```markdown
# 0001 — Click-through mechanism for the transparent overlay

Status: [Accepted | Rejected — needs fallback]
Date: [fill in]

## Context
The single-canvas overlay must be transparent + always-on-top while letting clicks on opaque
sprite pixels be captured and clicks on transparent pixels pass through. This is the M0 gate.

## Mechanisms evaluated
1. **Layered window + WS_EX_TRANSPARENT toggled by cursor-over-sprite polling (CHOSEN).**
   60 Hz DispatcherTimer polls GetCursorPos; toggles WS_EX_TRANSPARENT based on the sprite hit-test.
   Pros: simple, Avalonia-friendly, no WndProc subclassing. Cons: ~16 ms latency.
2. Low-level mouse hook (WH_MOUSE_LL) toggling the same style. Lower latency; more footguns
   (hook lifetime, reentrancy, message-loop dependency). Fallback if polling latency is felt.
3. WM_NCHITTEST → HTTRANSPARENT. Requires Win32 subclassing of the Avalonia HWND. Most "native"
   but most invasive. Fallback if (1) and (2) are insufficient.

## Evidence (from the harness)
- Spike 1 (transparency/topmost): [PASS/FAIL + screenshot path]
- Spike 2 (verify-desktop): [PASS/FAIL + the four booleans + harness-out/clickthrough.png]
- Spike 3 (MoonSharp sandbox): [PASS/FAIL — `dotnet test --filter SandboxedScriptHostTests`]
- GPU transparency note: [did true per-pixel transparency render, or did a black/gray box appear?]

## Decision
[If all spikes PASS: "Accept mechanism 1; proceed to M1." ]
[If Spike 1/2 FAIL: STOP. Do NOT proceed to M1. Options to evaluate before continuing:
  - Software-render fallback (Avalonia render to bitmap → UpdateLayeredWindow CPU path).
  - Try mechanism 2 or 3.
  - Reconsider the single-canvas window model.
 Record which option is chosen and re-run the gate.]
```

- [ ] **Step 3: Evaluate the gate and decide (explicit STOP)**

- If `dotnet test` is green AND `VERIFY-DESKTOP: PASS` AND the headless/real screenshots show true transparency:
  mark the decision record **Accepted** and proceed to plan M1.
- If transparency shows a black/gray box, or click-through `FAIL`s and cannot be fixed by adjusting the
  poll interval/settle delay: mark **Rejected — needs fallback**, **STOP**, and bring the decision record
  to the user to choose a fallback (software render / mechanism 2-3 / window-model change) BEFORE M1.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs(m0): click-through mechanism decision record + M0 gate evaluation"
```

---

## Self-Review (completed during authoring)

**Spec coverage:** Spike 1 → Tasks 5, 10. Spike 2 → Tasks 4, 5, 6, 7, 8, 10. Spike 3 → Task 1.
Harness `run` skill → Tasks 5 (headless mode), 9. Harness `verify-desktop` skill → Tasks 6, 7, 8, 9.
Dual-mode verification (headless + real-window) → Tasks 2, 5, 8. Named-pipe JSON-RPC → Tasks 3, 5, 6.
Victim-window + input injection → Tasks 6, 7, 8. STOP-gate → Task 10. CLAUDE.md → Task 9.

**Placeholder scan:** No "TBD/TODO/handle edge cases" in implementation steps. The bracketed fields in
the decision record (Task 10) are intentionally filled with real runtime results at the gate, not code.

**Type consistency:** `SpriteScene` (Width/Height/CircleX/CircleY/Radius/IsOverSprite), `EngineState`
(WindowX/WindowY/CircleX/CircleY/Radius/ClickThroughEnabled/HitCount + SpriteScreenCenter/TransparentScreenPoint),
`HarnessRequest.Cmd`, `IWindowEffects` (ApplyOverlayStyles/SetClickThrough/IsClickThrough),
`HeadlessRenderer` (RenderToPng/Draw), `ImageDiff.FractionDiffering`, `HarnessClient` (GetState/Quit),
`InputInjector.ClickAt`, `ScreenCapture.CaptureRegion`, `VerifyDesktop.Run` — names are consistent across tasks.
The headless `--harness`/`--headless` flags and the harness `verify` command match between Host and Harness runners.

**Known environment caveats (call out, don't pretend around):**
- `verify-desktop` needs a real interactive desktop + GPU; it will not pass on a hosted CI runner. This is by design.
- Synthetic input uses the shared system cursor — the machine must be left alone during a verify run.
- If Avalonia GPU compositing yields a non-transparent box, that is the exact P0 risk the gate exists to catch.
```
