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
