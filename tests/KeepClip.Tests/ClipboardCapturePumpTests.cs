using KeepClip.Application;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class ClipboardCapturePumpTests
{
    [Fact]
    public async Task CapturesDistinctCopiesInOrderWhileFirstSaveIsBlocked()
    {
        var repository = new SlowRepository();
        var history = new HistoryService(repository, (_, _, _) => throw new NotSupportedException());
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var copies = new List<string>();
        var text = "A";
        uint sequence = 1;
        using var pump = new ClipboardCapturePump(history, _ => Task.FromResult<ClipboardSnapshot?>(new(new(ClipKind.Text, text), sequence)));
        history.HistoryChanged += () =>
        {
            copies.Add(history.Entries[0].Text!);
            if (copies.Count == 3)
            {
                saved.TrySetResult();
            }
        };
        pump.Notify();
        await repository.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        text = "B";
        sequence++;
        pump.Notify();
        text = "C";
        sequence++;
        pump.Notify();
        repository.ReleaseFirstSave.SetResult();
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "A", "B", "C" }, copies);
        Assert.Equal(new[] { "C", "B", "A" }, history.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public async Task FailedSlowSaveDoesNotDiscardCopiesAlreadyRead()
    {
        var failure = new IOException("First save failed.");
        var repository = new SlowRepository(failure);
        var history = new HistoryService(repository, (_, _, _) => throw new NotSupportedException());
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var text = "A";
        uint sequence = 1;
        using var pump = new ClipboardCapturePump(history, _ => Task.FromResult<ClipboardSnapshot?>(new(new(ClipKind.Text, text), sequence)));
        pump.Failed += error => failed.TrySetResult(error);
        history.HistoryChanged += () =>
        {
            if (history.Entries.Count == 2)
            {
                saved.TrySetResult();
            }
        };
        pump.Notify();
        await repository.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        text = "B";
        sequence++;
        pump.Notify();
        text = "C";
        sequence++;
        pump.Notify();
        repository.ReleaseFirstSave.SetResult();
        Assert.Same(failure, await failed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "C", "B" }, history.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public async Task PauseAndResumeDuringSlowSaveDiscardOnlyQueuedCopiesFromBeforeThePause()
    {
        var repository = new SlowRepository();
        var history = new HistoryService(repository, (_, _, _) => throw new NotSupportedException());
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var text = "A";
        uint sequence = 1;
        using var pump = new ClipboardCapturePump(history, _ => Task.FromResult<ClipboardSnapshot?>(new(new(ClipKind.Text, text), sequence)));
        history.HistoryChanged += () =>
        {
            if (history.Entries[0].Text == "C")
            {
                saved.TrySetResult();
            }
        };
        pump.Notify();
        await repository.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        text = "B";
        sequence++;
        pump.Notify();
        history.SetPaused(true);
        history.SetPaused(false);
        text = "C";
        sequence++;
        pump.Notify();
        repository.ReleaseFirstSave.SetResult();
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "C", "A" }, history.Entries.Select(entry => entry.Text));
    }

    private sealed class SlowRepository(Exception? firstSaveFailure = null) : IHistoryRepository
    {
        public TaskCompletionSource FirstSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<ClipEntry>> LoadAsync(
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ClipEntry>>([]);

        public async Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default)
        {
            if (FirstSaveStarted.TrySetResult())
            {
                await ReleaseFirstSave.Task.WaitAsync(ct);
                if (firstSaveFailure is not null)
                {
                    throw firstSaveFailure;
                }
            }
        }
    }
}
