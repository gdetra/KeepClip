using System.Windows.Threading;
using KeepClip.App.Clipboard;

namespace KeepClip.Windows.Tests;

// These tests only exercise managed delegates and the message pump. They never
// instantiate the clipboard backend, access clipboard data, or open a window.
public sealed class StaClipboardExecutorTests
{
    [Fact]
    public async Task OperationsAndAwaitContinuationsStayOnTheSameStaThread()
    {
        using var executor = new StaClipboardExecutor();
        var caller = Environment.CurrentManagedThreadId;
        var state = await executor.RunAsync(async () =>
        {
            var before = Environment.CurrentManagedThreadId;
            var apartment = Thread.CurrentThread.GetApartmentState();
            var context = SynchronizationContext.Current;
            await Task.Delay(10);
            return (before, after: Environment.CurrentManagedThreadId, apartment, context, afterContext: SynchronizationContext.Current);
        }, default);
        var next = await executor.RunAsync(() => Task.FromResult(Environment.CurrentManagedThreadId), default);
        Assert.Equal(ApartmentState.STA, state.apartment);
        Assert.NotEqual(caller, state.before);
        Assert.Equal(state.before, state.after);
        Assert.Equal(state.before, next);
        Assert.IsType<DispatcherSynchronizationContext>(state.context);
        Assert.IsType<DispatcherSynchronizationContext>(state.afterContext);
    }

    [Fact]
    public async Task DisposalWaitsForAnActiveAsyncOperationToFinish()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var executor = new StaClipboardExecutor();
        var operation = executor.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
            return Thread.CurrentThread.GetApartmentState();
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        executor.Dispose();
        release.SetResult();
        Assert.Equal(ApartmentState.STA, await operation.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => executor.RunAsync(() => Task.FromResult(1), default));
    }

    [Fact]
    public async Task FailedAndCancelledRequestsDoNotStopTheExecutor()
    {
        using var executor = new StaClipboardExecutor();
        await Assert.ThrowsAsync<InvalidDataException>(() => executor.RunAsync<int>(() => throw new InvalidDataException(), default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var ran = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.RunAsync(() =>
        {
            ran = true;
            return Task.FromResult(1);
        }, cancellation.Token));
        Assert.False(ran);
        Assert.Equal(2, await executor.RunAsync(() => Task.FromResult(2), default));
    }
}
