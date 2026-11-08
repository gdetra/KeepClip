using KeepClip.Application;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class ClipboardCaptureTests
{
    internal sealed class MemoryRepository : IHistoryRepository
    {
        public IReadOnlyList<ClipEntry> Stored = [];
        public bool FailSave;

        public Task<IReadOnlyList<ClipEntry>> LoadAsync(CancellationToken ct = default) => Task.FromResult(Stored);

        public Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default)
        {
            if (FailSave)
            {
                throw new IOException("disk full");
            }

            Stored = entries.ToArray();
            return Task.CompletedTask;
        }
    }

    private static HistoryService Service(
        MemoryRepository? repository = null) => new(repository ?? new(), (id, bytes, ct) => Task.FromResult(id + ".png"));

    [Fact]
    public async Task LateOlderReadCannotReplaceLatestCopy()
    {
        var service = Service();
        await service.CaptureAsync(new(new(ClipKind.Text, "B"), 102));
        await service.CaptureAsync(new(new(ClipKind.Text, "A"), 101));
        Assert.Equal("B", Assert.Single(service.Entries).Text);
    }

    [Fact]
    public async Task OwnWriteAndPausedEventsAreIgnored()
    {
        var service = Service();
        await service.CaptureAsync(new(new(ClipKind.Text, "own"), 1, true));
        service.SetPaused(true);
        await service.CaptureAsync(new(new(ClipKind.Text, "paused"), 2));
        Assert.Empty(service.Entries);
        service.SetPaused(false);
        await service.CaptureAsync(new(new(ClipKind.Text, "visible"), 3));
        Assert.Equal("visible", Assert.Single(service.Entries).Text);
    }

    [Fact]
    public async Task EmptyAndUnsupportedAreIgnored()
    {
        var service = Service();
        await service.CaptureAsync(new(new(ClipKind.Text, ""), 1));
        await service.CaptureAsync(new(new(ClipKind.Image), 2));
        Assert.Empty(service.Entries);
    }

    [Fact]
    public async Task DuplicateDoesNotCreateExtraEntry()
    {
        var service = Service();
        await service.CaptureAsync(new(new(ClipKind.Text, "same"), 1));
        var id = service.Entries[0].Id;
        await service.TogglePinAsync(id);
        await service.CaptureAsync(new(new(ClipKind.Text, "same"), 2));
        Assert.Equal(id, Assert.Single(service.Entries).Id);
        Assert.True(service.Entries[0].IsPinned);
    }

    [Fact]
    public async Task FailedSaveLeavesVisibleHistoryUnchanged()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        await service.CaptureAsync(new(new(ClipKind.Text, "saved"), 1));
        repository.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => service.CaptureAsync(new(new(ClipKind.Text, "lost"), 2)));
        Assert.Equal("saved", Assert.Single(service.Entries).Text);
    }

    [Fact]
    public async Task BusyClipboardStopsAfterThreeAttempts()
    {
        var adapter = new BusyAdapter();
        await Assert.ThrowsAsync<System.Runtime.InteropServices.COMException>(() => ClipboardReader.ReadWithRetryAsync(adapter));
        Assert.Equal(3, adapter.Reads);
    }

    [Fact]
    public async Task DeferredReadCannotBeCapturedAfterPauseResume()
    {
        var service = Service();
        var generation = service.CaptureGeneration;
        service.SetPaused(true);
        service.SetPaused(false);
        await service.CaptureAsync(new(new(ClipKind.Text, "late"), 1, CaptureGeneration: generation));
        Assert.Empty(service.Entries);
        await service.CaptureAsync(new(new(ClipKind.Text, "fresh"), 2, CaptureGeneration: service.CaptureGeneration));
        Assert.Equal("fresh", Assert.Single(service.Entries).Text);
    }

    [Fact]
    public async Task ImageCapturePersistsPreparedContentAndReusesTheImageOnDuplicate()
    {
        var repository = new MemoryRepository();
        byte[] png = [1, 2, 3];
        var source = new ClipSource("Screen capture");
        var writes = new List<Guid>();
        var service = new HistoryService(
            repository,
            (id, bytes, _) =>
            {
                Assert.Equal(png, bytes);
                writes.Add(id);
                return Task.FromResult(id + ".png");
            },
            _ => Task.FromResult<ClipSource?>(source));
        ClipboardSnapshot Snapshot(uint sequence) => new(
            new(ClipKind.Image),
            sequence,
            SourceContext: new(sequence, 10, 20),
            PrepareContent: () => new(ClipKind.Image, Png: png));
        Assert.True((await service.CaptureAsync(Snapshot(1))).Captured);
        Assert.True((await service.CaptureAsync(Snapshot(2))).Captured);
        var saved = Assert.Single(repository.Stored);
        Assert.Equal(saved.Id, Assert.Single(writes));
        Assert.Equal(saved.Id + ".png", saved.ImagePath);
        Assert.Equal(source, saved.Source);
    }

    [Fact]
    public async Task PauseAndResumeDuringContentPreparationDiscardTheOldCapture()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var snapshot = new ClipboardSnapshot(
            new(ClipKind.Text),
            1,
            CaptureGeneration: service.CaptureGeneration,
            PrepareContent: () =>
            {
                service.SetPaused(true);
                service.SetPaused(false);
                return new(ClipKind.Text, "Before the pause");
            });
        Assert.False((await service.CaptureAsync(snapshot)).Captured);
        Assert.Empty(service.Entries);
        Assert.Empty(repository.Stored);
    }

    [Fact]
    public async Task PauseAndResumeDuringSourceLookupDiscardTheOldCapture()
    {
        var repository = new MemoryRepository();
        var source = new TaskCompletionSource<ClipSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new HistoryService(repository, (_, _, _) => throw new NotSupportedException(), _ => source.Task);
        var pending = service.CaptureAsync(new(
            new(ClipKind.Text, "Before the pause"),
            1,
            CaptureGeneration: service.CaptureGeneration,
            SourceContext: new(1, 10, 20)));
        Assert.False(pending.IsCompleted);
        service.SetPaused(true);
        service.SetPaused(false);
        source.SetResult(new ClipSource("Editor"));
        Assert.False((await pending).Captured);
        Assert.Empty(service.Entries);
        Assert.Empty(repository.Stored);
    }

    private sealed class BusyAdapter : IClipboardAdapter
    {
        public int Reads;

        public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
        {
            Reads++;
            throw new System.Runtime.InteropServices.COMException("busy");
        }

        public Task WriteAsync(
            ClipContent content,
            Guid operationId,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
