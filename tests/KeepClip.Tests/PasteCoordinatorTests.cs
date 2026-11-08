using KeepClip.Application;
using KeepClip.Application.Paste;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class PasteCoordinatorTests
{
    [Fact]
    public async Task CancelledSessionAfterDeferredWriteDoesNotHideOrSend()
    {
        var clipboard = new DeferredClipboard();
        var desktop = new Desktop("");
        var current = true;
        var task = new PasteCoordinator(clipboard, desktop, desktop, () => desktop.Hidden = true).PasteAsync(new(ClipKind.Image, Png: [1]), 42, canContinue: () => current);
        current = false;
        clipboard.Written.SetResult();
        var result = await task;
        Assert.Equal(PasteFailure.Cancelled, result.Failure);
        Assert.False(desktop.Hidden);
        Assert.Equal(0, desktop.Sent);
    }

    private sealed class DeferredClipboard : IClipboardAdapter
    {
        public TaskCompletionSource Written = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ClipboardSnapshot?> ReadAsync(
            CancellationToken ct = default) => Task.FromResult<ClipboardSnapshot?>(null);

        public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default) => Written.Task;
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("activate")]
    [InlineData("focus")]
    [InlineData("modifiers")]
    [InlineData("input")]
    public async Task UnsafeDestinationFallsBackToCopy(string failure)
    {
        var desktop = new Desktop(failure);
        var clipboard = new RecordingClipboard();
        var result = await new PasteCoordinator(clipboard, desktop, desktop, () => desktop.Hidden = true).PasteAsync(new(ClipKind.Text, "hello"), 42);
        Assert.Equal(PasteOutcome.CopiedOnly, result.Outcome);
        Assert.Equal("hello", clipboard.Content!.Text);
        Assert.True(desktop.Hidden);
        Assert.Equal(failure == "input" ? 1 : 0, desktop.Sent);
        Assert.Equal(failure switch
        {
            "closed" => PasteFailure.MissingDestination,
            "activate" => PasteFailure.ActivationRejected,
            "focus" => PasteFailure.FocusChanged,
            "modifiers" => PasteFailure.ModifiersHeld,
            _ => PasteFailure.InputRejected
        }, result.Failure);
    }

    [Fact]
    public async Task ValidDestinationGetsOnePasteAfterCopyAndHide()
    {
        var desktop = new Desktop("");
        var clipboard = new RecordingClipboard();
        var result = await new PasteCoordinator(clipboard, desktop, desktop, () => desktop.Hidden = true).PasteAsync(new(ClipKind.Text, "hello"), 42);
        Assert.Equal(PasteOutcome.InputSent, result.Outcome);
        Assert.Equal("hello", clipboard.Content!.Text);
        Assert.Equal(1, desktop.Sent);
    }

    [Fact]
    public async Task StoredTextFollowsTheExistingCopyHideAndPasteFlow()
    {
        var clipboard = new RecordingClipboard();
        var desktop = new Desktop("");
        var coordinator = new PasteCoordinator(clipboard, desktop, desktop, () => desktop.Hidden = true);
        var paste = new PasteClip(coordinator, new KeepClip.Storage.StoredClipContentLoader());
        var entry = new ClipEntry(Guid.NewGuid(), ClipKind.Text, "identity", DateTimeOffset.UnixEpoch, false, "stored text", null, 11);
        var result = await paste.ExecuteAsync(entry, 42, () => true);
        Assert.Equal(PasteOutcome.InputSent, result?.Outcome);
        Assert.Equal("stored text", clipboard.Content?.Text);
        Assert.True(desktop.Hidden);
        Assert.Equal(1, desktop.Sent);
    }

    private sealed class RecordingClipboard : IClipboardAdapter
    {
        public ClipContent? Content;

        public Task<ClipboardSnapshot?> ReadAsync(
            CancellationToken ct = default) => Task.FromResult<ClipboardSnapshot?>(null);

        public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default)
        {
            Content = content;
            return Task.CompletedTask;
        }
    }

    private sealed class Desktop(string failure) : IWindowTarget, IPasteInput
    {
        public bool Hidden;
        public int Sent;

        public nint Capture() => 42;

        public bool IsValid(nint handle) => failure != "closed" && handle == 42;

        public Task<bool> TryActivateAsync(
            nint handle,
            CancellationToken ct) => Task.FromResult(Hidden && failure != "activate");

        public bool IsForeground(nint handle) => failure != "focus";

        public Task<bool> WaitForModifiersReleasedAsync(
            TimeSpan timeout,
            CancellationToken ct) => Task.FromResult(failure != "modifiers");

        public bool SendPaste()
        {
            Sent++;
            return failure != "input";
        }
    }
}
