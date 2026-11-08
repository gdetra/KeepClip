using System.Runtime.InteropServices;
using KeepClip.Application;

namespace KeepClip.Tests;

public sealed class ClipboardFlushRetryTests
{
    [Fact]
    public async Task BusyFlushRetriesWithoutRepeatingTheWrite()
    {
        var attempts = 0;
        var waits = new List<int>();
        await ClipboardFlushRetry.FlushAsync(
            () =>
            {
                if (++attempts < 3)
                {
                    throw new COMException("busy", ClipboardFlushRetry.ClipboardBusy);
                }
            },
            () => true,
            (ms, _) =>
            {
                waits.Add(ms);
                return Task.CompletedTask;
            });
        Assert.Equal(3, attempts);
        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public async Task NewCopyDuringWaitStopsFlush()
    {
        var attempts = 0;
        var owned = true;
        await Assert.ThrowsAsync<ClipboardWriteSupersededException>(() => ClipboardFlushRetry.FlushAsync(
            () =>
            {
                attempts++;
                throw new COMException("busy", ClipboardFlushRetry.ClipboardBusy);
            },
            () => owned,
            (_, _) =>
            {
                owned = false;
                return Task.CompletedTask;
            }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task BusyAttemptsAreBounded()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<COMException>(() => ClipboardFlushRetry.FlushAsync(
            () =>
            {
                attempts++;
                throw new COMException("busy", ClipboardFlushRetry.ClipboardBusy);
            },
            () => true,
            (_, _) => Task.CompletedTask));
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task OtherErrorsAreNotRetried()
    {
        var attempts = 0;
        var waits = 0;
        await Assert.ThrowsAsync<COMException>(() => ClipboardFlushRetry.FlushAsync(
            () =>
            {
                attempts++;
                throw new COMException("other", unchecked((int)0x80004005));
            },
            () => true,
            (_, _) =>
            {
                waits++;
                return Task.CompletedTask;
            }));
        Assert.Equal(1, attempts);
        Assert.Equal(0, waits);
    }

    [Fact]
    public async Task AlreadySupersededDoesNotFlush()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<ClipboardWriteSupersededException>(() => ClipboardFlushRetry.FlushAsync(() => attempts++, () => false, (_, _) => Task.CompletedTask));
        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task CancellationStopsFlush()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ClipboardFlushRetry.FlushAsync(() => attempts++, () => true, Task.Delay, cancel.Token));
        Assert.Equal(0, attempts);
    }
}
