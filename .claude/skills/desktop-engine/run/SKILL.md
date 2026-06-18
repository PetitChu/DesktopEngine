---
name: desktop-engine-run
description: Build and launch the Desktop Engine in headless (render-to-PNG) or real-window mode and capture output. Use when you need to see what the engine renders or smoke-test that it launches.
---

# desktop-engine: run

## Build
Run `dotnet build -c Debug` from the repo root. Fix any compile errors before launching.

## Headless render (fast, no window, CI-safe)
Produces a deterministic CPU-raster PNG of the current scene. Does NOT initialize Avalonia,
so it is safe to run anywhere (no desktop session needed) and never hangs.

```
dotnet run --project src/DesktopEngine.Host -- --headless harness-out/headless.png
```
Then inspect `harness-out/headless.png` (use the Read tool on the image). Expect the OrangeRed
circle on a transparent background at the scene coordinates.

## Real-window launch (visual smoke)
The windowed app runs until closed, so do NOT launch it from an automated/agent session expecting
it to return — it will block. To observe the real transparent window programmatically (screenshot,
input), use the `desktop-engine:verify-desktop` skill, which launches it with a control pipe, drives
it, captures a screenshot, and shuts it down.

A human can smoke it directly:
```
dotnet run --project src/DesktopEngine.Host
```
Expect a frameless, transparent, always-on-top OrangeRed circle with no window chrome, no taskbar
entry, and no Alt-Tab entry. Close with Alt+F4.

## Golden comparison
To check a headless render against a stored golden, render headless then diff with
`DesktopEngine.Harness.ImageDiff.FractionDiffering(actualPath, goldenPath, channelTolerance: 8)`
(treat < ~0.02 differing as a match). Golden reference images live under `tests/**/golden/` and are
tracked in git; the `.gitignore` only excludes the `actual/` outputs, never the goldens.

## DPI note (M0)
M0 runs DPI-UNAWARE on purpose (so the harness's cross-process coordinate math is 1:1). Real
PerMonitorV2 DPI handling arrives in M1/M4.
