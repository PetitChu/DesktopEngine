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
