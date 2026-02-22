# Desktop Engine — Technical Design Document

## 0. Goal and non-goals

### Goal
Build a cross-platform **Desktop Games Engine** for “desktop idle worlds” (Desktropolis / desktop fishing / desktop dungeons style): lightweight, always-running (optional), low-power, and deeply integrated with the desktop.

### Non-goals (for v1)
- AAA 3D rendering
- Full-featured level editor (Unity-like)
- Multiplayer networking
- Anti-cheat / competitive security

### Primary targets
- **Windows is the only shipping/test target for v1** (first-class: overlay-ish behavior, click-through, DPI, tray, autostart).
- **macOS and Linux are architecture targets**: the codebase is structured as if we will support them, but their implementations are **stubbed** initially (clean interfaces, compile-time projects, runtime `NotSupportedException` where appropriate).
- **Portability rule:** no Windows-only calls from Engine/Core; all OS-specific behavior lives behind platform interfaces.


## 1. Key product requirements

### Desktop-native behavior
- Frameless window mode
- Optional transparent background
- Always-on-top option
- Optional click-through background (only sprites receive input)
- Per-pixel hit testing for interactive sprites
- Multi-monitor support + DPI scaling
- Tray icon + context menu
- Autostart (optional)
- Auto-update support (optional)

### Desktop overlay contract
A small but strict contract that defines how a “desktop game” behaves as a desktop citizen.
- **Window modes:** Normal / Overlay / Widget (preset bundles of window flags)
- **Focus rules:** avoid stealing focus; only focus on explicit user intent
- **Z-order rules:** Always-on-top vs standard; define behavior around full-screen apps (Windows: best-effort)
- **Input rules:** click-through transparent pixels; interactive sprites capture input
- **Hit testing:** bounding-box default; optional per-pixel alpha hit

### Runtime performance & power
**Power modes (user-selectable):**
- **Active:** up to 60 FPS render, 20–60 Hz simulation
- **Balanced:** 10–20 FPS when idle, 10–20 Hz simulation
- **Eco:** 1 FPS render when idle, 1–5 Hz simulation; event-driven wakeups

**Budgets (Windows v1 targets, best-effort):**
- Eco idle: aim for ~0–1% CPU average on a typical laptop when nothing animates
- Tick budget: configurable max ms per sim tick (default 2–4 ms in Eco)

**Sleep policy:** when no animations, timers, or pending work → suspend render invalidation and wake on timer/input.

- Idle CPU near-zero when nothing animates
- Adaptive tick rates:
  - Active/interactive: 60 FPS (or user setting)
  - Passive idle: 5–15 FPS
  - Fully idle: 1 FPS or event-driven
- Background simulation step separate from render step
- Deterministic-ish simulation for save/load

### Engine platform features
- Content packs (assets + scripts) loadable at runtime
- Scripting API for games (safe-ish sandbox)
- Save system (SQLite + JSON)
- Telemetry hooks (opt-in)
- Crash reporting hook (opt-in)


## 2. Recommended tech stack

### Core
- **.NET 8/9** (C#)
- **Avalonia UI** (windowing + app shell)
- **SkiaSharp** (2D rendering)

> **Windows-first policy:** we keep Avalonia/Skia because they’re cross-platform, but we only validate behavior on Windows for now. macOS/Linux code exists as stubs behind platform interfaces.

### Scripting & modding
- **Lua** (KeraLua or MoonSharp)
  - Prefer KeraLua for performance + native Lua 5.4
  - Prefer MoonSharp for pure managed simplicity

### Storage
- **SQLite** (Microsoft.Data.Sqlite)
- JSON (System.Text.Json) for settings and small configs

### Packaging / Updates
- **Velopack** (recommended) for Windows auto-updates outside Steam
- Optional Steam builds later

Windows v1 distribution plan (explicit):
- Install type: Velopack (or classic installer) with per-user install
- Save location: `%AppData%/DesktopEngine/` (via `IAppPaths`)
- Logs: `%LocalAppData%/DesktopEngine/logs/`
- Signing: plan for code-signing certificate later (documented but not required for M0–M2)


### Logging & diagnostics
- Serilog (structured logs)
- MiniDump/Crash handler hooks per platform


## 3. High-level architecture

### Cross-platform posture (Windows now, others stubbed)
All OS integrations live behind **platform interfaces**. The Windows implementation is real; macOS/Linux implementations compile but are **stubs**.

Stub rules:
- Allowed: return safe defaults, do nothing, or throw `NotSupportedException` for features that must be explicit (e.g., click-through).
- Not allowed: sprinkling `#if WINDOWS` inside Engine/Core logic.

Platform abstraction surface (initial):
- `IWindowEffects` (frameless, transparency, always-on-top, click-through, hit-test region)
- `ITrayService` (icon, menu, notifications hook)
- `IAutostartService` (enable/disable)
- `INotificationService` (optional; can be stubbed)
- `IDisplayInfo` (monitors, DPI, work area)
- `IAppPaths` (save/log/cache locations)
- `IGlobalHotkeys` (optional; likely Windows-only v1)

### Layering
1. **Desktop Host Layer**
   - Window creation + modes (transparent, click-through)
   - Tray integration
   - OS integrations (autostart, notifications)
   - DPI and multi-monitor

2. **Engine Core Layer**
   - Game loop (fixed simulation + variable render)
   - ECS-lite entity model (or scene graph)
   - Asset management
   - Input routing
   - Save/load
   - Scripting bridge

3. **Game Runtime Layer**
   - World definitions
   - Components + systems
   - UI overlays
   - Content packs

4. **Tooling Layer** (later)
   - Pack builder
   - Live reload
   - Profiler overlay

### Dev loop (make it pleasant early)
Even without a full editor, the engine should support a tight iteration loop:
- Dev mode watches an unpacked content-pack folder
- On change: reload assets + restart Lua VM (or re-run entry script) safely
- In-engine console/log window for script errors (with clickable stack traces)
- “Reload Pack” action in tray/context menu (Windows)



## 4. Process model: the “Desktop Game Loop”

### Threads
- **UI thread**: Avalonia UI + window events
- **Render thread** (optional): Skia draw scheduling; on some platforms you keep it on UI thread
- **Simulation thread**: engine tick + scripting

### Ticking
- Simulation tick: e.g. 20 Hz fixed-step (configurable)
- Render tick: vsync or fixed FPS (configurable)
- Background throttle rules:
  - Not visible / minimized: clamp to 1–5 FPS render
  - No input & no animated entities: event-driven / 1 FPS

### Scheduling
- Central scheduler owns:
  - Timers
  - Coroutines (Lua)
  - Delayed tasks
  - “Idle budget” (max ms per tick)


## 5. Rendering model

### Render goals
- Pixel art friendly
- Sprite batching
- Simple particles
- Optional lighting layer (cheap)

### Avalonia rendering surface strategy (decision)
- Use a dedicated Avalonia control that owns the render invalidation cadence.
- Rendering backend: Skia via Avalonia’s Skia pipeline.
- The engine requests redraws based on the active power mode (avoid continuous invalidation in Eco).
- Keep simulation and rendering decoupled: render pulls the latest snapshot/state.

### Entity model (decision: Hybrid)
- **Scene graph** for transforms, parenting, and draw ordering
- **ECS-lite** for gameplay state + systems (idle progression, timers, behaviors)

### Scene representation (render graph)
- Render nodes provide:
  - Transform (pos/scale/rot)
  - Z-order / layer
  - SpriteRenderer (texture + UVs)
  - Optional ParticleEmitter (simple)
  - Optional Collider (for hit testing)

Gameplay data lives in ECS-lite components; render graph is a view.

### Hit testing
- Default: bounding box hit
- Optional: per-pixel alpha hit (texture alpha threshold)

### Window transparency
- Transparent window with drawn sprites only
- Click-through transparent pixels (platform-dependent):
  - Windows: layered window / hit-test region
  - macOS: NSWindow ignores mouse events / custom hit regions
  - Linux: varies by compositor


## 6. Input routing

### Sources
- Mouse position + click
- Keyboard shortcuts
- Global hotkeys (optional, careful)

### Routing rules
- First try “interactive sprites” (hit test)
- If no sprite hit and click-through enabled → pass to OS
- Dragging behavior for sprites that support it


## 7. Assets & content packs

### Asset types
- Textures (png/webp)
- Sprite sheets + metadata
- Audio (wav/ogg)
- Fonts
- Scripts (Lua)
- Config (JSON)

### Content pack format
- Folder-based in dev
- Packed distribution: zip with manifest

### Manifest
- Pack id, name, version
- Entry scripts
- Asset registry
- Permissions requested (filesystem? network? notifications?)

### Permissions model (default deny)
- Content packs declare requested permissions in the manifest.
- Engine enforces **default deny**; permissions must be granted by the user (or by a trusted signature later).
- v1 scope (recommended):
  - `save` (always allowed within engine save folder)
  - `notifications` (user-toggle)
  - `network` (stubbed/disabled by default)


## 8. Scripting sandbox & API

### Philosophy
Scripts can create entities, subscribe to events, and mutate game state, but should not get arbitrary OS access by default.

### Lua integration
- Engine exposes:
  - `Engine.spawn()`
  - `Engine.on(event, fn)`
  - `Engine.time` / `Engine.deltaTime`
  - `Entity:set_component()`
  - `Assets.load_texture()` etc.

### Sandbox options
- Default Lua standard libs restricted
- Explicit permission gates for:
  - File IO beyond save folder
  - Network access
  - Process execution (likely never)


## 9. Save system

### Requirements
- Quick autosaves
- “Offline progress” calculation (idle games)

### Approach
- SQLite DB per user profile
- Tables:
  - `kv_state(key, value_json)`
  - `entities(id, archetype, state_json)`
  - `meta(last_saved_utc, version)`

### Offline progress
- On load: compute `delta = now - last_saved`
- Run an accelerated simulation step or apply analytical formulas


## 10. Desktop integrations

### Tray
- Show/hide window
- Pause/resume
- FPS / power mode toggle
- Quit

### Autostart
- Windows: registry / startup folder
- macOS: launch agent
- Linux: desktop entry / autostart

### Notifications
- Optional per game

### Windows compatibility test matrix (minimum)
A checklist to validate desktop behavior doesn’t get weird in common situations:
- Explorer desktop + taskbar (including auto-hide taskbar)
- Full-screen exclusive games (engine should not steal focus)
- Alt-Tab behavior
- Window snapping + multi-monitor docking/undocking
- DPI scaling changes (100%/125%/150%+)
- HDR on/off (best-effort)
- Multi-monitor with mixed DPI
- Remote Desktop / streaming sessions (best-effort)

## 11. Observability

### Logging
- Structured logs with categories:
  - Host
  - Engine
  - PackLoader
  - Script

### Debug overlay
- FPS, tick time, entity count
- Script errors + stack traces

### Crash handling
- Minimal crash report capture
- Optional user prompt to send


## 12. Repo structure (recommended)

```
DesktopEngine/
  src/
    DesktopEngine.Host/                # Avalonia app shell (Windows validated)
    DesktopEngine.Core/                # loop, entities, assets, input, scheduler
    DesktopEngine.Rendering/           # Skia renderers
    DesktopEngine.Scripting/           # Lua bridge + sandbox
    DesktopEngine.Content/             # pack format + loader

    DesktopEngine.Platform.Abstractions/  # interfaces + DTOs (no OS code)
    DesktopEngine.Platform.Windows/       # real implementations (P/Invoke, tray, autostart, click-through)
    DesktopEngine.Platform.Mac/           # stub implementations (compile, not supported at runtime)
    DesktopEngine.Platform.Linux/         # stub implementations (compile, not supported at runtime)

    DesktopEngine.SampleGame/          # sample pack + launcher
  tools/
    PackBuilder/                       # later
  docs/
    TDD.md
```

Design note: Host depends on exactly one Platform.* project via DI/factory selection.

```
DesktopEngine/
  src/
    DesktopEngine.Host/          # Avalonia app, windowing, tray
    DesktopEngine.Core/          # loop, entities, assets, input, scheduler
    DesktopEngine.Rendering/     # Skia renderers
    DesktopEngine.Scripting/     # Lua bridge + sandbox
    DesktopEngine.Content/       # pack format + loader
    DesktopEngine.SampleGame/    # sample pack + launcher
  tools/
    PackBuilder/                 # later
  docs/
    TDD.md
```


## 13. Milestones

### Sample packs (reference projects / regression tests)
- **Desktop Fish Tank:** idle generation + click-to-feed interactions
- **Desktop Dungeon:** procedural room loop + timed events
- **Desktop City:** growth + pop-up interactions + simple economy

### M0 — Skeleton (runs)
- Avalonia window + Skia surface drawing a sprite
- Fixed simulation loop (no content packs)
- Basic input (mouse click)

### M1 — Desktop behaviors (Windows implemented; others stubbed)
- Frameless + transparent window
- Always-on-top
- Tray menu (Windows)
- Click-through background (Windows)
- macOS/Linux: stub platform services compile + return safe defaults / throw NotSupportedException where required

### M2 — Runtime core
- Entity model + components
- Asset loading
- Simple animation

### M3 — Content packs
- Manifest + zip loader
- Hot reload (dev only)

### M4 — Scripting
- Lua runtime
- Engine API + sandbox
- Script-driven entities

### M5 — Save + idle progression
- SQLite saves
- Offline progress support

### M6 — Tooling + polish
- Pack builder
- Debug overlay
- Installer/update pipeline


## 14. Risks & mitigations

### Transparency + click-through cross-platform
- Risk: inconsistent behavior on macOS/Linux compositors
- Mitigation: Windows-first; keep strict platform interfaces; stub non-Windows; feature flags per platform later

### Power usage
- Risk: high background CPU
- Mitigation: adaptive tick throttling + event-driven idle

### Scripting security
- Risk: mods gaining OS access
- Mitigation: restricted Lua libs + explicit permissions

### Porting debt
- Risk: Windows implementation leaks into Core via convenience hacks
- Mitigation: enforce “no OS calls in Core” rule; platform abstractions; unit tests around platform boundaries; CI that at least builds Mac/Linux stub projects

## 15. Initial API sketches (C#). Initial API sketches (C#)

```csharp
public interface IEngineHost
{
    IWindowController Window { get; }
    ITrayController Tray { get; }
    IPlatformServices Platform { get; }
}

public sealed class Engine
{
    public Engine(IEngineHost host, EngineConfig config);

    public void LoadPack(string path);
    public void Run();
    public void Stop();
}

public sealed record EngineConfig(
    int SimulationHz,
    int MaxRenderFps,
    PowerMode PowerMode);

public enum PowerMode { Active, Balanced, Eco }
```


## 16. Definition of Done (v1)
- Windows-only shipping target
- Can run a sample desktop idle world as a content pack
- Transparent/frameless window with interactive sprites
- Tray controls + clean shutdown
- Save/load + offline progress
- Stable performance in Eco mode (throttling + sleep policy)
- Windows packaging path documented (Velopack recommended)
- macOS/Linux projects compile as stubs (compile-only CI)

---

## Appendix A — Platform plan

### The rule
Engine/Core must never call OS APIs directly. Everything goes through **DesktopEngine.Platform.Abstractions**.

### Windows (implemented)
- Click-through transparent pixels
- Hit-test region management / layered window interop
- Tray icon + menu
- Autostart integration
- Optional global hotkeys

### macOS (stub for v1)
- Project compiles
- Platform services return safe defaults or throw `NotSupportedException`
- TODO list maintained per feature

### Linux (stub for v1)
- Project compiles
- Platform services return safe defaults or throw `NotSupportedException`
- TODO list maintained per feature (Wayland/X11/compositor differences)

### CI / build posture
- Windows: full test + packaging
- macOS/Linux: compile-only (stubs) so portability regressions get caught early

