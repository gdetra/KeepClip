using KeepClip.Application;
using KeepClip.Application.Selection;
using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class SelectionCopyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task SelectingEditableOrUncertainTextPreservesPreviousClipboardAndHistory(bool? readOnly)
    {
        var clipboard = new Writer
        {
            Text = "Testo da incollare"
        };
        var history = History();
        var sounds = 0;
        await history.CaptureAsync(new(new(ClipKind.Text, "Testo da incollare"), 9));
        var previous = Assert.Single(history.Entries);
        var copy = new SelectionCopyCoordinator(clipboard, history, () => true, () => 10, () => sounds++);
        Assert.False(await copy.CopyAsync(new("Parola da sostituire", IsReadOnly: readOnly), () => true));
        Assert.Equal("Testo da incollare", clipboard.Text);
        Assert.Equal(0, clipboard.Writes);
        Assert.Equal(previous, Assert.Single(history.Entries));
        Assert.Equal(0, sounds);
    }

    [Theory]
    [InlineData("text", true, false, true, false)]
    [InlineData("text", false, true, true, false)]
    [InlineData("", false, false, true, false)]
    [InlineData("   ", false, false, true, false)]
    [InlineData("text", false, false, false, false)]
    [InlineData("text", false, false, true, true)]
    public async Task IneligibleSelectionDoesNotWriteOrNotify(
        string text,
        bool password,
        bool own,
        bool enabled,
        bool paused)
    {
        var clipboard = new Writer();
        var history = History();
        history.SetPaused(paused);
        var sounds = 0;
        var copy = new SelectionCopyCoordinator(clipboard, history, () => enabled, () => 10, () => sounds++);
        Assert.False(await copy.CopyAsync(new(text, password, own), () => true));
        Assert.Equal(0, clipboard.Writes);
        Assert.Empty(history.Entries);
        Assert.Equal(0, sounds);
    }

    [Fact]
    public async Task ValidSelectionWritesAndSavesExactlyOnce()
    {
        var clipboard = new Writer();
        var history = History();
        var sounds = 0;
        var copy = new SelectionCopyCoordinator(clipboard, history, () => true, () => 10, () => sounds++);
        Assert.True(await copy.CopyAsync(new("Caffè 日本語"), () => true));
        Assert.Equal("Caffè 日本語", clipboard.Text);
        Assert.Equal("Caffè 日本語", Assert.Single(history.Entries).Text);
        Assert.Equal(1, sounds);
    }

    [Fact]
    public async Task StaleSelectionAndOversizeSelectionNeverWrite()
    {
        var clipboard = new Writer();
        var copy = new SelectionCopyCoordinator(clipboard, History(), () => true, () => 10, () =>
        {
        });
        Assert.False(await copy.CopyAsync(new("stale"), () => false));
        Assert.False(await copy.CopyAsync(new(new string('a', 100_001)), () => true));
        Assert.Equal(0, clipboard.Writes);
    }

    [Fact]
    public async Task PauseDuringDeferredWritePreventsSavingAndSound()
    {
        var clipboard = new Writer
        {
            Deferred = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var history = History();
        var sounds = 0;
        var copy = new SelectionCopyCoordinator(clipboard, history, () => true, () => 10, () => sounds++);
        var task = copy.CopyAsync(new("late"), () => true);
        history.SetPaused(true);
        history.SetPaused(false);
        clipboard.Deferred.SetResult();
        Assert.False(await task);
        Assert.Empty(history.Entries);
        Assert.Equal(0, sounds);
    }

    [Fact]
    public async Task FailedSaveDoesNotNotify()
    {
        var repository = new ClipboardCaptureTests.MemoryRepository
        {
            FailSave = true
        };
        var history = History(repository);
        var sounds = 0;
        var messages = new List<string>();
        var copy = new SelectionCopyCoordinator(new Writer(), history, () => true, () => 10, () => sounds++, messages.Add);
        Assert.False(await copy.CopyAsync(new("text"), () => true));
        Assert.Equal(0, sounds);
        Assert.Single(messages);
        Assert.Empty(history.Entries);
    }

    [Fact]
    public async Task FullPinnedHistoryReportsRejectionWithoutSound()
    {
        var repository = new ClipboardCaptureTests.MemoryRepository
        {
            Stored = Enumerable.Range(0, 200).Select(i => new ClipEntry(Guid.NewGuid(), ClipKind.Text, i.ToString(), DateTimeOffset.UnixEpoch, true, "pinned", null, 1)).ToArray()
        };
        var history = History(repository);
        await history.InitializeAsync();
        var messages = new List<string>();
        var sounds = 0;
        var copy = new SelectionCopyCoordinator(new Writer(), history, () => true, () => 10, () => sounds++, messages.Add);
        Assert.False(await copy.CopyAsync(new("new selection"), () => true));
        Assert.Single(messages);
        Assert.Equal(0, sounds);
        Assert.Equal(200, history.Entries.Count);
    }

    [Fact]
    public async Task ChangedFocusDuringDeferredWriteDoesNotSaveOrNotify()
    {
        var clipboard = new Writer
        {
            Deferred = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var history = History();
        var current = true;
        var sounds = 0;
        var copy = new SelectionCopyCoordinator(clipboard, history, () => true, () => 10, () => sounds++);
        var task = copy.CopyAsync(new("late"), () => current);
        current = false;
        clipboard.Deferred.SetResult();
        Assert.False(await task);
        Assert.Empty(history.Entries);
        Assert.Equal(0, sounds);
    }

    private static HistoryService History(
        ClipboardCaptureTests.MemoryRepository? repository = null) => new(repository ?? new(), (_, _, _) => Task.FromResult("unused"));

    private sealed class Writer : IClipboardAdapter
    {
        public int Writes;
        public string? Text;
        public TaskCompletionSource? Deferred;

        public Task<ClipboardSnapshot?> ReadAsync(
            CancellationToken ct = default) => Task.FromResult<ClipboardSnapshot?>(null);

        public Task WriteAsync(ClipContent content, Guid operation, CancellationToken ct = default)
        {
            Writes++;
            Text = content.Text;
            return Deferred?.Task ?? Task.CompletedTask;
        }
    }
}
