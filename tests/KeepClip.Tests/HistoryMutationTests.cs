using KeepClip.Application;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public sealed class HistoryMutationTests
{
    [Fact]
    public async Task ClearHistoryCanPreserveOrIncludePinned()
    {
        var service = new HistoryService(new ClipboardCaptureTests.MemoryRepository(), (id, bytes, ct) => Task.FromResult("image"));
        await service.CaptureAsync(new(new(ClipKind.Text, "keep"), 1));
        await service.TogglePinAsync(service.Entries[0].Id);
        await service.CaptureAsync(new(new(ClipKind.Text, "remove"), 2));
        await service.ClearAsync(false);
        Assert.Equal("keep", Assert.Single(service.Entries).Text);
        await service.ClearAsync(true);
        Assert.Empty(service.Entries);
    }

    [Fact]
    public async Task FailedMutationDoesNotUpdateHistory()
    {
        var repository = new ClipboardCaptureTests.MemoryRepository();
        var service = new HistoryService(repository, (id, bytes, ct) => Task.FromResult("image"));
        await service.CaptureAsync(new(new(ClipKind.Text, "saved"), 1));
        repository.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => service.DeleteAsync(service.Entries[0].Id));
        Assert.Single(service.Entries);
    }
}
