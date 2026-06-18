# 0001 — Click-through mechanism for the transparent overlay

Status: **Accepted**
Date: 2026-06-17
Milestone: M0 (De-risk & skeleton — the gate)

## Context
The single-canvas overlay must be transparent + always-on-top while letting clicks on opaque
sprite pixels be captured and clicks on transparent pixels pass through to the window beneath. This
is the M0 gate and the project's P0 technical risk (whether Avalonia + GPU Skia can do this on
Windows at all).

## Mechanisms evaluated
1. **Layered window + WS_EX_TRANSPARENT toggled by cursor-over-sprite polling (CHOSEN).**
   A 60 Hz `DispatcherTimer` (`ClickThroughPump`) polls `GetCursorPos`; it toggles `WS_EX_TRANSPARENT`
   on the window's extended style based on the sprite hit-test (`SpriteScene.IsOverSprite`). The
   window also carries `WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`.
   Transparency itself comes from Avalonia `TransparencyLevelHint = Transparent` + DWM.
   Pros: simple, Avalonia-friendly, no WndProc subclassing. Cons: ~16 ms toggle latency (the verify
   harness settles 80 ms before clicking, which covers it).
2. Low-level mouse hook (WH_MOUSE_LL) toggling the same style. Lower latency; more footguns (hook
   lifetime, reentrancy, message-loop dependency). **Not needed** — polling proved sufficient.
3. WM_NCHITTEST → HTTRANSPARENT via Win32 subclassing of the Avalonia HWND. Most "native", most
   invasive. **Not needed.**

## Evidence (from the harness, real Windows 11, .NET 9)
- **Spike 1 — transparency + sprite render + topmost: PASS.** Screenshot `0001-clickthrough-proof.png`
  (captured from the live GPU window) shows the OrangeRed sprite circle rendered crisply, with the
  green victim window visible everywhere outside the circle (true per-pixel transparency, NOT a
  black/gray box), and the overlay sitting on top.
- **Spike 2 — per-pixel click-through: PASS.** `VERIFY-DESKTOP: PASS` with
  `spriteCaptured=True, spriteLeaked=False, transparentNotCaptured=True, transparentPassedThrough=True`
  — i.e., a click on the sprite is captured by the overlay and does not reach the window beneath, and
  a click on a transparent pixel is not captured and does reach the window beneath.
- **Spike 3 — MoonSharp sandbox: PASS.** `SandboxedScriptHostTests` green: `io`, `os.execute`,
  `require`, `load`, `dofile` are nil; Basic/String/Table/Math/Coroutine/Json available; a stub
  `Engine.spawn` is callable from Lua. (`dotnet test` → 16/16 across the suite.)

## GPU transparency note
The reviewer-flagged risk — that a self-applied `WS_EX_LAYERED` could conflict with Avalonia's
DirectComposition transparency and produce an opaque box — **did NOT materialize**. With
`WS_EX_LAYERED` present, transparency rendered correctly (see screenshot). No software-render
fallback was needed.

## DPI scope
M0 is intentionally **DPI-unaware** (Host/Harness/Victim manifests `dpiAware=false`; WinForms
`ApplicationHighDpiMode=DpiUnaware`) so cursor / window-position / victim coordinates share one 1:1
space and the harness's coordinate math is correct. Real **PerMonitorV2 DPI handling is deferred to
M1/M4** (M4 explicitly covers the DPI compat matrix). This is a known, documented limitation, not a
silent gap.

## Runtime-discovered fixes (logged for posterity)
The first live runs surfaced three integration bugs unit tests could not (runtime data is ground
truth): `Process.Start` needs absolute paths (Win32 CreateProcess); the pipe client must wrap the
stream with `leaveOpen:true` so reader/writer disposal doesn't close the pipe prematurely; and the
screenshot needs a paint-settle delay to capture the first GPU frame. All fixed and committed.

## Decision
**Accept mechanism 1. Proceed to M1.** The P0 risk is retired: transparent, always-on-top, frameless
GPU rendering with per-pixel click-through works on real Windows, and the dual-mode AI harness
(headless render + real-window verify with synthetic input and a victim window) can prove it
automatically. No fallback (software render / mechanism 2–3 / window-model change) is required.
