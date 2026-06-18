---
name: desktop-engine-verify-desktop
description: Prove the desktop-native behaviors (transparency, always-on-top, per-pixel click-through) on real Windows by launching a victim window beneath the overlay, injecting clicks, and checking capture vs pass-through. Use to verify the M0 gate and any change touching windowing/click-through.
---

# desktop-engine: verify-desktop

## What it proves
1. A click on an opaque sprite pixel is captured by the overlay (engine HitCount increments) and
   does NOT reach the window beneath.
2. A click on a transparent pixel is NOT captured and DOES reach the window beneath.
Both must hold for click-through to be considered working. It also captures a screenshot of the live
transparent window so you can confirm transparency + sprite rendering visually.

## Prerequisites
- An **interactive desktop session with a GPU** (NOT a headless CI runner).
- **Do not move the mouse/keyboard while it runs** — synthetic input shares the real cursor (~5s).
- Build first: `dotnet build -c Debug`.

## Run it
```
"src/DesktopEngine.Harness/bin/Debug/net9.0-windows/DesktopEngine.Harness.exe" verify ^
  "src/DesktopEngine.Host/bin/Debug/net9.0-windows/DesktopEngine.Host.exe" ^
  "tools/HarnessVictim/bin/Debug/net9.0-windows/HarnessVictim.exe" ^
  "harness-out/clickthrough.png"
```
(Relative paths are fine — the runner resolves them to absolute internally.) Equivalent via the SDK:
`dotnet run --project src/DesktopEngine.Harness -- verify <hostExe> <victimExe> <screenshot.png>`.

## Read the result
- Exit code 0 and `VERIFY-DESKTOP: PASS` ⇒ click-through works.
- Then open `harness-out/clickthrough.png` (Read tool) and confirm: an OrangeRed circle over a
  light-green field, with green visible everywhere outside the circle (= true transparency, not a box).
- On `FAIL`, the four booleans localize the fault:
  - `spriteCaptured=false` ⇒ the WS_EX_TRANSPARENT toggle/poll didn't disable click-through in time.
  - `spriteLeaked=true` ⇒ the sprite click also hit the victim (overlay not capturing).
  - `transparentNotCaptured=false` ⇒ the overlay wrongly captured a transparent-pixel click.
  - `transparentPassedThrough=false` ⇒ transparency/pass-through not functioning.

## Notes
- The runner launches the victim, then the overlay (with a control pipe), injects a click on the
  sprite center and on a guaranteed-transparent point, screenshots after a paint settle, then quits
  both processes (it always cleans up, even on failure).
- CI: headless tests run anywhere; this skill needs a real desktop. Run it on the dev machine or a
  self-hosted Windows runner, not on hosted CI.
