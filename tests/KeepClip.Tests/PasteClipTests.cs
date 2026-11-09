using KeepClip.Application;
using KeepClip.Application.Paste;
using KeepClip.Domain.History;
using KeepClip.Storage;

namespace KeepClip.Tests;

public class PasteClipTests
{
    private static readonly ClipEntry _entry = new(
        Guid.NewGuid(),
        ClipKind.Files,
        "identity",
        DateTimeOffset.UnixEpoch,
        false,
        null,
        null,
        10,
        Files: [new(@"C:\KeepClipTests\file.txt")]);

    [Fact]
    public async Task UnavailableOriginalsDoNotWriteTheClipboardOrSendInput()
    {
        var desktop = new UnusedDesktop();
        var coordinator = new PasteCoordinator(desktop, desktop, desktop, () => throw new InvalidOperationException());
        var paste = new PasteClip(coordinator, new Loader(() => Task.FromResult<ClipContent?>(null)));
        var result = await paste.ExecuteAsync(_entry, 42, () => true);
        Assert.Equal(PasteFailure.OriginalFilesUnavailable, result?.Failure);
    }

    [Fact]
    public async Task ChangedSessionWhileLoadingDoesNotWriteTheClipboardOrSendInput()
    {
        var desktop = new UnusedDesktop();
        var coordinator = new PasteCoordinator(desktop, desktop, desktop, () => throw new InvalidOperationException());
        var loaded = new TaskCompletionSource<ClipContent?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var paste = new PasteClip(coordinator, new Loader(() => loaded.Task));
        var current = true;
        var pending = paste.ExecuteAsync(_entry, 42, () => current);
        current = false;
        loaded.SetResult(new(ClipKind.Files, Files: _entry.Files));
        Assert.Null(await pending);
    }

    [Fact]
    public async Task StorageLoaderRejectsTheWholeFileSelectionWhenAnOriginalDisappears()
    {
        var file = Path.GetTempFileName();
        try
        {
            var entry = _entry with
            {
                Files = [new(file), new(file + ".missing")]
            };
            Assert.Null(await new StoredClipContentLoader().LoadAsync(entry));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private sealed class Loader(Func<Task<ClipContent?>> load) : IPasteContentLoader
    {
        public Task<ClipContent?> LoadAsync(ClipEntry entry, CancellationToken ct = default) => load();
    }

    private sealed class UnusedDesktop : IClipboardAdapter, IWindowTarget, IPasteInput
    {
        public Task<ClipboardSnapshot?> ReadAsync(
            CancellationToken ct = default) => throw new InvalidOperationException();

        public Task WriteAsync(
            ClipContent content,
            Guid operationId,
            CancellationToken ct = default) => throw new InvalidOperationException();

        public nint Capture() => throw new InvalidOperationException();

        public bool IsValid(nint handle) => throw new InvalidOperationException();

        public Task<bool> TryActivateAsync(nint handle, CancellationToken ct) => throw new InvalidOperationException();

        public bool IsForeground(nint handle) => throw new InvalidOperationException();

        public Task<bool> WaitForModifiersReleasedAsync(
            TimeSpan timeout,
            CancellationToken ct) => throw new InvalidOperationException();

        public bool SendPaste() => throw new InvalidOperationException();
    }
}
