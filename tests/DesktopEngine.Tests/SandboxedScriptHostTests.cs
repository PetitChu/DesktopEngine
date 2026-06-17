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
    [InlineData("return os.execute")]
    [InlineData("return io")]
    [InlineData("return require")]
    [InlineData("return load")]
    [InlineData("return dofile")]
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
