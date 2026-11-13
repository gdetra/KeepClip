using System.Windows.Threading;
using KeepClip.App.Desktop;

namespace KeepClip.Windows.Tests;

public class KeyboardHookThreadTests
{
    [Fact]
    public async Task HookThreadPumpsMessagesWhileItsCallerIsBusy()
    {
        using var loop = new KeyboardHookThread();
        var callerThread = Environment.CurrentManagedThreadId;
        var dispatched = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.Invoke(() =>
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(() => dispatched.SetResult(Environment.CurrentManagedThreadId));
            return true;
        });

        // The creating thread does not pump messages while waiting.
        var hookThread = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(callerThread, hookThread);
        Assert.Equal(hookThread, loop.Invoke(() => Environment.CurrentManagedThreadId));
    }

    [Fact]
    public void DisposalStopsTheMessageLoopAndRejectsFurtherWork()
    {
        var loop = new KeyboardHookThread();
        var dispatcher = loop.Invoke(() => Dispatcher.CurrentDispatcher);
        loop.Dispose();
        loop.Dispose();
        Assert.True(dispatcher.HasShutdownFinished);
        Assert.Throws<ObjectDisposedException>(() => loop.Invoke(() => true));
    }
}
