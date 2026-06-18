# Desktop Engine — project conventions

A Windows-first desktop idle-game engine (.NET 9, Avalonia 11, SkiaSharp, MoonSharp). Single
transparent-canvas overlay model. See `docs/superpowers/specs/2026-06-16-desktop-engine-design.md`
for the full design and `docs/superpowers/plans/` for milestone plans.

## Architecture rules (enforced harder in M1 via architecture-guard)
- **No OS calls in Core/Engine.** All OS behavior goes through `DesktopEngine.Platform.Abstractions`
  interfaces; real implementations live in `DesktopEngine.Platform.Windows`. P/Invoke,
  `System.Runtime.InteropServices`, and Win32 namespaces must not appear outside `Platform.Windows`
  and the harness/tools.
- `Platform.Abstractions` and `Scripting` stay `net9.0` (portable). Windows-bound projects use
  `net9.0-windows`.
- The harness (`DesktopEngine.Harness`) and `tools/` are test tooling — OS calls are allowed there.
- Scripts run in a MoonSharp `Preset_SoftSandbox` (no io / os.execute / require / load). Keep it that way.

## Harness modes
- Headless: `dotnet run --project src/DesktopEngine.Host -- --headless <out.png>` (CPU raster,
  deterministic, no window, never hangs).
- Real-window + control pipe: `--harness <pipeName>` (named-pipe JSON: ping / get_state / quit;
  the server accepts repeated connections, one request per connection).
- Verify click-through: `DesktopEngine.Harness.exe verify <hostExe> <victimExe> <png>` — needs a
  real desktop session and takes over the mouse for ~5s.

## Skills
- `desktop-engine:run` — build + launch (headless or real-window) + capture.
- `desktop-engine:verify-desktop` — the click-through proof (needs a real desktop session).

## DPI (M0 decision)
M0 is intentionally **DPI-unaware** (manifest `dpiAware=false` on Host, Harness, and Victim;
WinForms `ApplicationHighDpiMode=DpiUnaware`) so cursor/window/victim coordinates share one 1:1
space and the naive harness math is correct. **Real PerMonitorV2 DPI handling is M1/M4 work** — do
not assume scaled-display correctness until then.

## Click-through mechanism (M0, accepted)
WS_EX_LAYERED + WS_EX_TRANSPARENT, with WS_EX_TRANSPARENT toggled by a 60 Hz cursor-position poll
(`ClickThroughPump`) against the sprite hit-test. Transparency via Avalonia
`TransparencyLevelHint = Transparent` + DWM. Proven on real Windows in M0 (see
`docs/decisions/0001-clickthrough-mechanism.md`).

## Testing split
- Pure logic (scripting sandbox, image diff, protocol) ⇒ xUnit, `dotnet test`.
- Desktop/visual behavior (transparency, click-through) ⇒ runtime acceptance via the harness;
  **runtime data is ground truth.** These need a real desktop and cannot run on hosted CI.
