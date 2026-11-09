using KeepClip.Application;
using KeepClip.Application.Paste;
using KeepClip.Application.Selection;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public sealed class AsyncClipboardServiceTests
{
    [Fact]
    public async Task RequestInvalidatedDuringBackendPreparationNeverCommits()
    {
        var backend = new PreparingBackend();
        using var service = new AsyncClipboardService(backend, new Executor(), () => 1);
        var current = true;
        var write = service.WriteIfCurrentAsync(new(ClipKind.Files, Files: [new("C:\\file.txt")]), Guid.NewGuid(), () => current);
        await backend.Entered.Task;
        current = false;
        backend.Release.SetResult();
        await Assert.ThrowsAsync<ClipboardRequestCancelledException>(() => write);
        Assert.Equal(0, backend.Writes);
    }

    [Theory]
    [InlineData("gesture")]
    [InlineData("pause")]
    [InlineData("disabled")]
    public async Task InvalidatedSelectionInQueueNeverWritesOrNotifies(string reason)
    {
        var backend = new Backend
        {
            ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var service = new AsyncClipboardService(backend, new Executor(), () => 1);
        var history = new HistoryService(new ClipboardCaptureTests.MemoryRepository(), (_, _, _) => Task.FromResult("unused.png"));
        await history.CaptureAsync(new(new(ClipKind.Text, "previous"), 1));
        var read = service.ReadAsync();
        var current = true;
        var enabled = true;
        var sounds = 0;
        var selection = new SelectionCopyCoordinator(service, history, () => enabled, () => 1, () => sounds++);
        var copy = selection.CopyAsync(new("stale selection"), () => current);
        if (reason == "gesture")
        {
            current = false;
        }
        else if (reason == "pause")
        {
            history.SetPaused(true);
            history.SetPaused(false);
        }
        else
        {
            enabled = false;
        }

        backend.ReadCompletion.SetResult(null);
        await read;
        Assert.False(await copy);
        Assert.Empty(backend.Writes);
        Assert.Equal("previous", Assert.Single(history.Entries).Text);
        Assert.Equal(0, sounds);
    }

    [Fact]
    public async Task CancelledShelfSessionInQueueDoesNotReplaceClipboardOrSendInput()
    {
        var backend = new Backend
        {
            ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var service = new AsyncClipboardService(backend, new Executor(), () => 1);
        var read = service.ReadAsync();
        var session = new PasteSession();
        session.Open(12);
        var request = session.Snapshot();
        var hidden = 0;
        var paste = new PasteCoordinator(service, new NoTarget(), new NoInput(), () => hidden++);
        var operation = paste.PasteAsync(new(ClipKind.Text, "cancelled card"), 12, canContinue: () => session.IsCurrent(request));
        session.Cancel();
        backend.ReadCompletion.SetResult(null);
        await read;
        var result = await operation;
        Assert.Equal(PasteFailure.Cancelled, result.Failure);
        Assert.Equal(PasteOutcome.Cancelled, result.Outcome);
        Assert.Empty(backend.Writes);
        Assert.Equal(0, hidden);
    }

    [Fact]
    public async Task ReadAndWriteAreSerializedAndPayloadsArePreserved()
    {
        var backend = new Backend
        {
            ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var executor = new Executor();
        using var service = new AsyncClipboardService(backend, executor, () => 1);
        var read = service.ReadAsync();
        var content = new ClipContent(ClipKind.Files, Files: [new("C:\\example.txt", false)]);
        var operation = Guid.NewGuid();
        var write = service.WriteAsync(content, operation);
        Assert.Equal(1, backend.Reads);
        Assert.Empty(backend.Writes);
        var snapshot = new ClipboardSnapshot(new(ClipKind.Text, "Caffè 日本語"), 1, Source: new("Blocco note"));
        backend.ReadCompletion.SetResult(snapshot);
        Assert.Same(snapshot, await read);
        await write;
        var actual = Assert.Single(backend.Writes);
        Assert.Same(content, actual.Content);
        Assert.Equal(operation, actual.Operation);
        Assert.Equal(2, executor.Executions);
    }

    [Fact]
    public async Task NewCopyWhileWriteIsQueuedIsNeverOverwritten()
    {
        uint sequence = 1;
        var backend = new Backend
        {
            ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var service = new AsyncClipboardService(backend, new Executor(), () => sequence);
        var read = service.ReadAsync();
        var write = service.WriteAsync(new(ClipKind.Text, "stale"), Guid.NewGuid());
        sequence = 2;
        backend.ReadCompletion.SetResult(null);
        await read;
        await Assert.ThrowsAsync<ClipboardWriteSupersededException>(() => write);
        Assert.Empty(backend.Writes);
    }

    [Fact]
    public async Task CancellationWhileQueuedNeverStartsTheWrite()
    {
        var backend = new Backend
        {
            ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var service = new AsyncClipboardService(backend, new Executor(), () => 1);
        var read = service.ReadAsync();
        using var cancellation = new CancellationTokenSource();
        var write = service.WriteAsync(new(ClipKind.Text, "cancelled"), Guid.NewGuid(), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        backend.ReadCompletion.SetResult(null);
        await read;
        Assert.Empty(backend.Writes);
    }

    [Fact]
    public async Task ChangeDuringDispatchAlsoPreventsTheWrite()
    {
        uint sequence = 1;
        var backend = new Backend();
        using var service = new AsyncClipboardService(backend, new Executor { BeforeRun = () => sequence++ }, () => sequence);
        await Assert.ThrowsAsync<ClipboardWriteSupersededException>(() => service.WriteAsync(new(ClipKind.Text, "stale"), Guid.NewGuid()));
        Assert.Empty(backend.Writes);
    }

    [Fact]
    public async Task FailureReleasesTheQueueForTheNextOperation()
    {
        var backend = new Backend
        {
            ReadError = new InvalidDataException("bad image")
        };
        using var service = new AsyncClipboardService(backend, new Executor(), () => 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync());
        await service.WriteAsync(new(ClipKind.Image, Png: [1, 2, 3]), Guid.NewGuid());
        Assert.Single(backend.Writes);
    }

    [Fact]
    public async Task DisposalCancelsActiveAndQueuedOperationsAndClosesExecutorOnce()
    {
        var backend = new Backend
        {
            ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var executor = new Executor();
        var service = new AsyncClipboardService(backend, executor, () => 1);
        var read = service.ReadAsync();
        var write = service.WriteAsync(new(ClipKind.Text, "queued"), Guid.NewGuid());
        service.Dispose();
        service.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.ReadAsync());
        Assert.Empty(backend.Writes);
        Assert.Equal(1, executor.Disposals);
    }

    [Fact]
    public async Task BusyReadRetriesWithinTheExecutorAndThenReturnsOwnMarker()
    {
        var backend = new Backend
        {
            BusyReads = 2,
            Snapshot = new(new(ClipKind.Text), 1, true)
        };
        var executor = new Executor();
        using var service = new AsyncClipboardService(backend, executor, () => 1);
        Assert.True((await service.ReadAsync())!.IsOwnWrite);
        Assert.Equal(3, backend.Reads);
        Assert.Equal(1, executor.Executions);
    }

    private sealed class Executor : IClipboardExecutor
    {
        public int Executions, Disposals;
        public Action? BeforeRun;

        public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Executions++;
            BeforeRun?.Invoke();
            return action();
        }

        public void Dispose() => Disposals++;
    }

    private sealed class Backend : IClipboardAdapter
    {
        public int Reads, BusyReads;
        public Exception? ReadError;
        public ClipboardSnapshot? Snapshot;
        public TaskCompletionSource<ClipboardSnapshot?>? ReadCompletion;
        public List<(ClipContent Content, Guid Operation)> Writes = [];

        public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
        {
            Reads++;
            if (Reads <= BusyReads)
            {
                throw new System.Runtime.InteropServices.COMException("busy", ClipboardFlushRetry.ClipboardBusy);
            }

            if (ReadError is not null)
            {
                throw ReadError;
            }

            return ReadCompletion?.Task.WaitAsync(ct) ?? Task.FromResult(Snapshot);
        }

        public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Writes.Add((content, operationId));
            return Task.CompletedTask;
        }
    }

    private sealed class PreparingBackend : IClipboardAdapter
    {
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Writes;

        public Task<ClipboardSnapshot?> ReadAsync(
            CancellationToken ct = default) => Task.FromResult<ClipboardSnapshot?>(null);

        public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default)
        {
            Writes++;
            return Task.CompletedTask;
        }

        public async Task WriteIfCurrentAsync(
            ClipContent content,
            Guid operationId,
            Func<bool> isCurrent,
            CancellationToken ct = default)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(ct);
            if (!isCurrent())
            {
                throw new ClipboardRequestCancelledException();
            }

            Writes++;
        }
    }

    private sealed class NoTarget : IWindowTarget
    {
        public nint Capture() => throw new InvalidOperationException();

        public bool IsValid(nint handle) => throw new InvalidOperationException();

        public Task<bool> TryActivateAsync(nint handle, CancellationToken ct) => throw new InvalidOperationException();

        public bool IsForeground(nint handle) => throw new InvalidOperationException();
    }

    private sealed class NoInput : IPasteInput
    {
        public Task<bool> WaitForModifiersReleasedAsync(
            TimeSpan timeout,
            CancellationToken ct) => throw new InvalidOperationException();

        public bool SendPaste() => throw new InvalidOperationException();
    }
}
