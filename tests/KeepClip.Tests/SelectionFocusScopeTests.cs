using KeepClip.Application.Selection;

namespace KeepClip.Tests;

public sealed class SelectionFocusScopeTests
{
    [Fact]
    public void SiblingControlsSharingOnlyLayoutCannotReuseOldSelection()
    {
        Assert.False(SelectionFocusScope.SharesTextOwner(
            new[] { "slider", "pane" },
            new[] { "old-editor", "pane" },
            value => value == "old-editor",
            (a, b) => a == b));
    }

    [Fact]
    public void SameTextOwnerCanSupplySelection()
    {
        Assert.True(SelectionFocusScope.SharesTextOwner(
            new[] { "text", "document", "pane" },
            new[] { "caret", "document", "pane" },
            value => value == "document",
            (a, b) => a == b));
    }
}
