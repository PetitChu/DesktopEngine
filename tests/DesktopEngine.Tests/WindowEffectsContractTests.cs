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
