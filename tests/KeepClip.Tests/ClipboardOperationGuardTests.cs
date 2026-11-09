using System.Runtime.InteropServices;
using KeepClip.Application;

namespace KeepClip.Tests;

public sealed class ClipboardOperationGuardTests
{
    [Fact]
    public async Task InitialSequenceIsNotTrustedBeforeTokenCheck()
    {
        var guard = new ClipboardOperationGuard("expected", () => 10, _ => Task.FromResult<string?>("foreign"));
        Assert.False(await guard.StillOwnedAsync(default));
    }

    [Fact]
    public async Task DelayedRenderingSequenceChangeKeepsSameOperation()
    {
        uint sequence = 10;
        var reads = 0;
        var guard = new ClipboardOperationGuard(
            "expected",
            () => sequence,
            _ =>
            {
                reads++;
                return Task.FromResult<string?>("expected");
            });
        sequence = 11;
        Assert.True(await guard.StillOwnedAsync(default));
        Assert.True(await guard.StillOwnedAsync(default));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task ForeignOrNewOwnOperationIsRejected()
    {
        var guard = new ClipboardOperationGuard("expected", () => 11, _ => Task.FromResult<string?>("different"));
        Assert.False(await guard.StillOwnedAsync(default));
    }

    [Fact]
    public async Task ChangingClipboardDuringTokenReadIsNotTrusted()
    {
        uint sequence = 11;
        var guard = new ClipboardOperationGuard(
            "expected",
            () => sequence,
            _ =>
            {
                sequence = 12;
                return Task.FromResult<string?>("expected");
            });
        var error = await Assert.ThrowsAsync<COMException>(() => guard.StillOwnedAsync(default));
        Assert.Equal(ClipboardFlushRetry.ClipboardBusy, error.HResult);
    }

    [Fact]
    public async Task BusyTokenReadIsRetriedBeforeFlushing()
    {
        var reads = 0;
        var flushes = 0;
        var guard = new ClipboardOperationGuard(
            "expected",
            () => 11,
            _ =>
            {
                if (++reads == 1)
                {
                    throw new COMException("busy", ClipboardFlushRetry.ClipboardBusy);
                }

                return Task.FromResult<string?>("expected");
            });
        await ClipboardFlushRetry.FlushAsync(() => flushes++, guard.StillOwnedAsync, (_, _) => Task.CompletedTask);
        Assert.Equal(2, reads);
        Assert.Equal(1, flushes);
    }
}
