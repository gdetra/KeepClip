using KeepClip.Application.Paste;

namespace KeepClip.Tests;

public class PasteSessionTests
{
    [Fact]
    public void ReopeningInvalidatesDeferredPasteAndPreservesOriginalTarget()
    {
        var session = new PasteSession();
        session.Open(10);
        var original = session.Snapshot();
        session.Open(20);
        Assert.Equal((nint)10, original.Destination);
        Assert.False(session.IsCurrent(original));
        Assert.Equal((nint)20, session.Snapshot().Destination);
    }

    [Fact]
    public void DismissInvalidatesPendingSelection()
    {
        var session = new PasteSession();
        session.Open(10);
        var original = session.Snapshot();
        session.Cancel();
        Assert.False(session.IsCurrent(original));
    }
}
