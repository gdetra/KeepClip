using KeepClip.App.Shelf;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class ShelfViewModelTests
{
    private static ClipEntry Text(
        string text,
        bool pinned = false) => new(Guid.NewGuid(), ClipKind.Text, text, DateTimeOffset.UtcNow, pinned, text, null, text.Length);

    [Fact]
    public void UnicodeSearchIsCaseInsensitive()
    {
        var vm = new ShelfViewModel();
        vm.Refresh([Text("CAFfÈ"), Text("other")]);
        vm.Query = "caffè";
        Assert.Equal("CAFfÈ", Assert.Single(vm.Entries).Text);
    }

    [Fact]
    public void PinnedFilterOnlyShowsPinnedEntries()
    {
        var vm = new ShelfViewModel();
        vm.Refresh([Text("keep", true), Text("other")]);
        vm.ShowPinnedOnly = true;
        Assert.Equal("keep", Assert.Single(vm.Entries).Text);
    }

    [Fact]
    public void FilteringReplacesInvalidSelection()
    {
        var a = Text("A");
        var b = Text("B");
        var vm = new ShelfViewModel();
        vm.Refresh([a, b]);
        vm.SelectedEntry = a;
        vm.Query = "B";
        Assert.Equal(b.Id, vm.SelectedEntry!.Id);
        vm.Query = "missing";
        Assert.Null(vm.SelectedEntry);
    }
}
