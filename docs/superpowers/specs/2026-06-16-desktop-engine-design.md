# Desktop Engine — Design Spec

> Status: Approved design (brainstorm output). Supersedes the open decisions in
> `desktop_engine_technical_design_document.md` (TDD). Where this spec and the TDD
> disagree, **this spec wins**. The TDD remains the reference for unchanged detail.
>
> Date: 2026-06-16

---

## 1. Goal & non-goals

**Goal.** A cross-platform **Desktop Games Engine** for "desktop idle worlds"
(Desktropolis / desktop fishing / desktop dungeon style): lightweight,
optionally always-running, low-power, deeply integrated with the desktop.

**Non-goals (v1):** AAA 3D rendering; full Unity-like level editor; multiplayer
networking; anti-cheat / competitive security.

**Primary target.** **Windows is the only shipping/test target for v1.**
macOS and Linux are *architecture* targets: the codebase is structured to support
them, but their platform implementations are **stubs** (compile, return safe
defaults, or throw `NotSupportedException` where a feature must be explicit).
**Portability rule:** no OS-specific calls from Engine/Core — all OS behavior
lives behind platform interfaces.

---

## 2. Locked decisions (this brainstorm)

| Area | Decision | Rationale |
|---|---|---|
| **Window model** | **Single transparent canvas**, one window per monitor; per-pixel hit-test routes input | Matches "desktop world" idle games; one click-through mechanism to solve, not N; clean multi-monitor story |
| **Build strategy** | **Vertical slice** — drive everything toward *Desktop Fish Tank running on the desktop* | De-risks integration early; yields a runnable artifact the AI harness can launch & screenshot |
| **Scripting** | **MoonSharp** (Lua 5.2, pure-managed, sandboxed) behind `IScriptHost` | Can't segfault host; far easier to sandbox safely; sim runs at 1–20 Hz so native speed is rarely the bottleneck |
| **Audio** | **Thin slice** — `IAudioService` (one-shot SFX + ambient loop), `Audio.play()` in Lua | Gives worlds life without the surface area of a full mixer |
| **Verification** | **Dual-mode** — headless deterministic (render-to-PNG + state dumps) **+** real-window screenshot/input | Headless covers logic/render/sim; real-window is the *only* coverage of the desktop-behavior P0 |
| **Threading** | UI/render thread + simulation thread w/ snapshot hand-off | Avalonia ties drawing to the compositor/UI thread; a standalone "render thread" is a fiction — dropped |
| **Harness packaging** | In-repo `.claude/` (skills + CLAUDE.md + settings), extractable to a plugin later | Versioned with the engine; simplest for a single project |
| **Control channel** | Named-pipe **JSON-RPC** for real-window harness mode | Robust, scriptable command surface for screenshots/input/state |

### Corrections folded in from the TDD
- Removed the duplicate `§12` repo structure — keep the **split `Platform.*`** layout.
- Removed the duplicate `§15` heading.
- **ECS-lite is a simple component dictionary, not a high-perf archetype ECS** (entity counts are tiny — dozens).
- Audio promoted from an orphaned asset type to a real subsystem (thin slice).
- **Per-pixel click-through moved from an M1 "risk" to the M0 gating spike** (see §6 risks).

---

## 3. Architecture

### 3.1 Layering
1. **Desktop Host** (Avalonia app shell) — window creation/modes (transparent,
   click-through), tray, OS integrations, DPI & multi-monitor. Depends on exactly
   **one** `Platform.*` impl via DI/factory selection.
2. **Engine Core** — game loop (fixed sim + variable render), ECS-lite + scene
   graph, asset management, input routing, scheduler, save/load, scripting bridge.
   **Contains zero OS calls.**
3. **Game Runtime** — world definitions, components + systems, UI overlays,
   content packs.
4. **Tooling** (later) — PackBuilder, live reload, profiler overlay.

### 3.2 Platform abstraction surface
`IWindowEffects` (frameless, transparency, always-on-top, click-through,
hit-test region) · `ITrayService` · `IAutostartService` · `IDisplayInfo`
(monitors, DPI, work area) · `IAppPaths` (save/log/cache) · `INotificationService`
(optional) · `IGlobalHotkeys` (optional, Windows-only v1) · `IAudioService`
(thin slice) · `IScriptHost` (MoonSharp behind an interface).

Windows: real implementations. macOS/Linux: stubs (compile; safe defaults or
`NotSupportedException`).

### 3.3 Process model
- **UI/render thread:** Avalonia UI + window events + Skia draw.
- **Simulation thread:** engine tick + scripting; hands a **snapshot** to the
  renderer (sim and render are decoupled — render pulls the latest snapshot).
- **Scheduler** owns timers, Lua coroutines, delayed tasks, and the per-tick
  "idle budget" (max ms/tick).

### 3.4 Rendering
- Dedicated Avalonia control owns redraw cadence; engine requests redraws per
  **power mode** (no continuous invalidation in Eco).
- Skia 2D, pixel-art-friendly sampling, sprite batching, simple particles,
  optional cheap lighting layer.
- **Scene graph** = view (transform, z-order/layer, SpriteRenderer, optional
  ParticleEmitter/Collider). **ECS-lite** = gameplay state + systems. Render graph
  is a view over ECS-lite data.
- Hit testing: bounding-box default; optional per-pixel alpha threshold.

### 3.5 Power modes
- **Active:** ≤60 FPS render, 20–60 Hz sim.
- **Balanced:** 10–20 FPS idle, 10–20 Hz sim.
- **Eco:** 1 FPS idle, 1–5 Hz sim, event-driven wakeups.
- **Sleep policy:** no animations/timers/pending work → suspend render
  invalidation; wake on timer/input. Eco idle target ~0–1% CPU on a typical laptop.

### 3.6 Content packs & scripting
- Folder-based in dev; zip + manifest for distribution.
- Manifest: id/name/version, entry scripts, asset registry, **requested
  permissions**. Engine enforces **default-deny**; user grants (or trusted
  signature later). v1 perms: `save` (always allowed in engine save folder),
  `notifications` (toggle), `network` (disabled by default).
- MoonSharp Engine API: `Engine.spawn`, `Engine.on(event, fn)`,
  `Engine.time`/`deltaTime`, `Entity:set_component`, `Assets.load_texture`,
  `Audio.play`. Restricted stdlib (no `io`/`os`/`require`); explicit permission
  gates for file IO beyond save, network, process exec (never).

### 3.7 Save & offline progress
- SQLite per profile: `kv_state(key, value_json)`, `entities(id, archetype,
  state_json)`, `meta(last_saved_utc, version)`. JSON for settings.
- Offline progress: `delta = now − last_saved` → **analytical catch-up**
  (not lockstep determinism).

### 3.8 Repo structure
```
DesktopEngine/
  src/
    DesktopEngine.Host/                    # Avalonia app shell (Windows validated)
    DesktopEngine.Core/                    # loop, ECS-lite, assets, input, scheduler, save
    DesktopEngine.Rendering/               # Skia renderers
    DesktopEngine.Scripting/               # MoonSharp bridge + sandbox (IScriptHost)
    DesktopEngine.Content/                 # pack format + loader
    DesktopEngine.Platform.Abstractions/   # interfaces + DTOs (no OS code)
    DesktopEngine.Platform.Windows/        # real impls (P/Invoke, tray, autostart, click-through)
    DesktopEngine.Platform.Mac/            # stubs (compile, not supported at runtime)
    DesktopEngine.Platform.Linux/          # stubs (compile, not supported at runtime)
    DesktopEngine.SampleGame/              # sample packs + launcher
  tools/
    PackBuilder/                           # later
  tests/                                   # Core unit tests, platform integration, golden images
  .claude/                                 # AI harness (skills, CLAUDE.md, settings)
  docs/
```

---

## 4. Milestone plan (vertical-slice driven)

**North Star:** *Desktop Fish Tank* running as a real content pack on the Windows
desktop — transparent, click-through, always-on-top, click-to-feed, idle
generation, saves + offline progress, Eco idle ~0–1% CPU.

### M0 — De-risk & skeleton *(the gate)*
- **Spike 1:** Avalonia+Skia transparent / frameless / always-on-top, GPU-rendered, draws one sprite.
- **Spike 2:** per-pixel **click-through** — pick the winning mechanism (layered + low-level mouse-hook toggle vs `WM_NCHITTEST → HTTRANSPARENT` vs per-frame `SetWindowRgn`). Prove: click on sprite captured; click on transparent pixel passes through to a victim window.
- **Spike 3:** MoonSharp sandbox — restricted stdlib, stub `Engine` API.
- **Harness bootstrap:** `desktop-engine:run` + `desktop-engine:verify-desktop` (needed to verify the spikes).
- **GATE:** if click-through can't be made acceptable → STOP and re-evaluate (software-render fallback / window-model change) before building further.
- **Deliverable:** "it runs, it's transparent, clicks pass through, a sprite is clickable" + a decision record naming the chosen click-through mechanism.

### M1 — Engine core skeleton
- Game loop (fixed sim + variable render, snapshot-decoupled); power modes + adaptive tick + sleep policy.
- ECS-lite (component dictionary) + systems; scene graph (transforms/z-order); Skia sprite rendering.
- Input routing (hit-test sprites → else click-through).
- Platform abstractions defined; Windows real, Mac/Linux stubs.
- **Harness:** `desktop-engine:add-platform-service`; **architecture-guard** hook + CI.
- **Deliverable:** hard-coded animated sprites on the transparent desktop, clickable, with a tray menu (show/hide, pause, power mode, quit).

### M2 — Content packs + the Fish Tank pack
- Pack format (folder dev) + manifest + **default-deny permissions**; asset loading + registry.
- MoonSharp Engine API + sandbox + permission gates; **thin audio slice**.
- Hot reload (dev) + in-engine script-error console.
- **Desktop Fish Tank** authored as a content pack (idle generation, click-to-feed, Lua behaviors).
- **Harness:** `desktop-engine:new-content-pack`.
- **Deliverable:** Fish Tank runs as a content pack, fully scripted, hot-reloadable.

### M3 — Save, offline progress & power polish
- SQLite saves + JSON settings; ECS-lite + scene serialization.
- Offline progress via analytical catch-up.
- Eco hardening: near-zero idle CPU, event-driven wakeups, tick-budget enforcement.
- **Harness:** `desktop-engine:profile-power`; finalize headless deterministic mode (seeded RNG, CPU raster, golden images).
- **Deliverable:** Fish Tank saves, computes offline progress on relaunch, idles at ~0–1% CPU.

### M4 — Desktop-citizen hardening + 2nd pack
- Windows compat matrix: auto-hide taskbar, fullscreen-exclusive (no focus steal), Alt-Tab, snapping, **multi-monitor + mixed DPI**, DPI changes (100/125/150%), HDR/RDP best-effort.
- Window modes (Normal/Overlay/Widget), focus & z-order rules; autostart, notifications, global hotkeys.
- **Desktop Dungeon** pack (proves the engine generalizes; Desktop City optional/later).
- **Harness:** extend `verify-desktop` across the compat matrix.
- **Deliverable:** passes the compat checklist; two distinct sample packs run.

### M5 — Tooling, packaging & v1 polish
- Debug overlay (FPS, tick time, entity count, script errors + stack traces).
- Serilog structured logs (Host/Engine/PackLoader/Script); crash handler + minimal report (opt-in).
- PackBuilder (folder → zip); **Velopack** per-user install + update pipeline (code-signing documented).
- Telemetry hooks (opt-in); compile-only CI for Mac/Linux stubs.
- **Deliverable:** v1 Definition of Done met.

---

## 5. AI harness

**Principle:** the harness is *half engine-code, half Claude config.* The engine
exposes two purpose-built modes; the skills wrap them.

### 5.1 Engine-side harness modes
**Headless deterministic mode** *(M0 → finalized M3)*
`DesktopEngine --headless --pack <path> --seed <N> --ticks <K> --dump-frames <dir> --dump-state <file>`
- Seeded RNG, fixed-step sim, **Skia CPU raster** → byte-stable frames for
  exact/low-tolerance hashing. Dumps entity/world state as JSON. No real window.
- The **fast inner loop**: agent asserts on exact state values + golden-image diffs.

**Real-window harness mode** *(M0 → extended M4)*
`DesktopEngine --harness` launches the real transparent/always-on-top GPU window
plus a **named-pipe JSON-RPC** control channel: `screenshot`, `inject_click x y`,
`spawn_victim`, `get_state`, `set_power_mode`, `quit`.
- GPU path → screenshots compared with **perceptual tolerance** (not exact).
- The **only** coverage of the P0 behaviors.

### 5.2 Skills (in-repo `.claude/skills/desktop-engine/`)
| Skill | Milestone | Purpose |
|---|---|---|
| `run` | M0 | Build + launch (headless or real-window), capture screenshots/state, return to agent |
| `verify-desktop` | M0 | Real-window: spawn victim window, inject clicks over sprite vs transparent pixel, confirm capture vs pass-through; transparency/always-on-top/DPI screenshot checks |
| `add-platform-service` | M1 | Scaffold interface + Windows impl + Mac/Linux stubs in one consistent shot |
| `new-content-pack` | M2 | Scaffold pack (manifest + entry Lua + sample assets), validate manifest vs schema |
| `profile-power` | M3 | Run idle, sample CPU, assert Eco + tick-time budgets |

### 5.3 Guardrails *(M1)*
- **architecture-guard** — `PreToolUse` + CI check failing if Core/Engine gains a
  forbidden dependency or OS API call (P/Invoke, `System.Runtime.InteropServices`,
  Win32 namespaces) outside `Platform.Windows`.
- **Golden-image regression** — reference PNGs from headless mode stored in-repo;
  `run` diffs against them with tolerance so visual regressions surface per change.

### 5.4 Verification loop & CI posture
- **Inner loop (fast, every change):** edit → `dotnet build` → headless run →
  assert state + golden diff. CI-safe.
- **Outer loop (smoke gate, on-demand):** real-window run → `verify-desktop`.
- **CI constraint (honest):** real-window screenshot/input tests need an
  **interactive desktop session with a GPU**, which most hosted runners lack.
  Therefore: headless tests run in hosted CI on every push; real-window behavior
  tests run on the **dev machine or a self-hosted Windows runner**. Documented,
  not pretended-around.
- *(Optional, later)* a `verify-slice` workflow chaining build → headless asserts
  → real-window smoke for autonomous milestone checks.

### 5.5 Project conventions (`.claude/CLAUDE.md`)
Encodes: layering & dependency direction, the **no-OS-in-Core** rule, naming
conventions, and how to invoke the two harness modes — so any agent (or session)
inherits the rules without re-deriving them.

---

## 6. Risks & mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| **Per-pixel click-through + transparency + always-on-top via Avalonia/Skia GPU on Windows** — the whole product rests on it | **P0** | M0 gating spike with a victim-window test; named candidate mechanisms; software-render fallback identified; **STOP gate** if unworkable |
| High background CPU | High | Adaptive tick throttling + event-driven idle + tick budget; `profile-power` asserts it |
| Scripting security (mods gaining OS access) | High | MoonSharp restricted stdlib + default-deny permissions + explicit gates |
| Porting debt (Windows leaking into Core) | Med | `no-OS-in-Core` rule + **architecture-guard** hook/CI + compile-only Mac/Linux CI |
| Real-window tests can't run in hosted CI | Med | Documented split: headless in hosted CI, real-window on dev/self-hosted runner |
| GPU nondeterminism in frame hashing | Low | Headless mode uses **CPU raster** for stable output; real-window uses perceptual tolerance |

---

## 7. Definition of Done (v1)
- Windows-only shipping target.
- Runs a sample desktop idle world (Fish Tank) as a content pack; a second pack
  (Dungeon) proves generality.
- Transparent/frameless window with interactive sprites; click-through verified.
- Tray controls + clean shutdown.
- Save/load + offline progress.
- Stable performance in Eco mode (throttling + sleep policy), idle ~0–1% CPU.
- Windows packaging path working (Velopack); signing documented.
- macOS/Linux projects compile as stubs (compile-only CI).
- AI harness operational: headless inner loop in CI; real-window smoke on
  dev/self-hosted; the five skills + architecture-guard in place.
