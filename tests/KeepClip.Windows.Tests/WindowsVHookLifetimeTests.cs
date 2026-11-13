using KeepClip.App.Desktop;

namespace KeepClip.Windows.Tests;

public class WindowsVHookLifetimeTests
{
    [Fact]
    public void HookCanBeEnabledDisabledAndReenabledWithoutAcceptingOldMessages()
    {
        using var hook = new WindowsVHotkey(0);
        Assert.True(hook.SetEnabled(true));
        Assert.True(hook.IsCurrent(1));
        Assert.True(hook.SetEnabled(true));
        Assert.True(hook.IsCurrent(1));
        Assert.True(hook.SetEnabled(false));
        Assert.False(hook.IsCurrent(1));
        Assert.True(hook.SetEnabled(true));
        Assert.False(hook.IsCurrent(1));
        Assert.True(hook.IsCurrent(3));
    }

    [Fact]
    public void DisposingAHookTwiceIsSafe()
    {
        var hook = new WindowsVHotkey(0);
        Assert.True(hook.SetEnabled(true));
        hook.Dispose();
        hook.Dispose();
    }
}
