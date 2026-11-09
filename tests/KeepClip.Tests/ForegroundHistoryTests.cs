using KeepClip.Application.Paste;

namespace KeepClip.Tests;

public class ForegroundHistoryTests
{
    [Fact]
    public void OpeningTrayOrOwnMenuRetainsPreviousApplication()
    {
        var history = new ForegroundHistory(window => window == 100);
        history.Observe(100);
        history.Observe(200); // 200: taskbar/menu, not a paste destination.
        Assert.Equal((nint)100, history.Resolve(0)); // our hidden message window owns focus during menu action.
        Assert.Equal((nint)100, history.Resolve(200));
    }

    [Fact]
    public void HotkeyUsesCurrentApplicationInsteadOfOlderEvent()
    {
        var history = new ForegroundHistory(window => window == 100 || window == 300);
        history.Observe(100);
        Assert.Equal((nint)300, history.Resolve(300));
        Assert.Equal((nint)300, history.Resolve(0));
    }

    [Fact]
    public void ClosedPreviousApplicationIsNeverReturned()
    {
        var open = true;
        var history = new ForegroundHistory(window => open && window == 100);
        history.Observe(100);
        open = false;
        Assert.Equal((nint)0, history.Resolve(200));
    }

    [Fact]
    public void NoObservedApplicationFallsBackWithoutGuessing()
    {
        var history = new ForegroundHistory(window => window == 100);
        Assert.Equal((nint)0, history.Resolve(0));
    }
}
