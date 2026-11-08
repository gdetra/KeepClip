using KeepClip.App.Shelf;
using KeepClip.Application;
using KeepClip.Domain.History;
using KeepClip.Storage;

namespace KeepClip.Tests;

public sealed class FileHistoryTests
{
    private static HistoryService History() => new(new ClipboardCaptureTests.MemoryRepository(), (_, _, _) => throw new Exception("Must not save file bytes as images"));

    [Fact]
    public async Task FilesAreCapturedTogetherWithSource()
    {
        var history = History();
        ClipFile[] files = [new(@"C:/Files/report.pdf"), new(@"C:/Files/Folder", true)];
        Assert.True((await history.CaptureAsync(new(new(ClipKind.Files, Files: files), 1, Source: new("Explorer")))).Captured);
        var entry = Assert.Single(history.Entries);
        Assert.Equal(ClipKind.Files, entry.Kind);
        Assert.Equal(files, entry.Files);
        Assert.Equal("Explorer", entry.Source?.Name);
        Assert.Null(entry.ImagePath);
        Assert.Null(entry.Text);
    }

    [Fact]
    public async Task SameSelectionDeduplicatesRegardlessOfOrderAndPreservesPin()
    {
        var history = History();
        ClipFile[] files = [new(@"C:/Files/a.txt"), new(@"C:/Files/b.txt")];
        await history.CaptureAsync(new(new(ClipKind.Files, Files: files), 1));
        var id = Assert.Single(history.Entries).Id;
        await history.TogglePinAsync(id);
        await history.CaptureAsync(new(new(ClipKind.Files, Files: files.Reverse().ToArray()), 2));
        var saved = Assert.Single(history.Entries);
        Assert.Equal(id, saved.Id);
        Assert.True(saved.IsPinned);
    }

    [Fact]
    public async Task CaseSensitiveFileNamesAreNotMerged()
    {
        var history = History();
        await history.CaptureAsync(new(new(ClipKind.Files, Files: [new("C:/Case/a.txt")]), 1));
        await history.CaptureAsync(new(new(ClipKind.Files, Files: [new("C:/Case/A.txt")]), 2));
        Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public async Task DeletingHistoryLeavesOriginalFilesUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Path.Combine(root, "original.txt");
        await File.WriteAllTextAsync(original, "keep");
        try
        {
            var repository = new SqliteHistoryRepository(Path.Combine(root, "history"));
            await repository.SaveAsync([new(Guid.NewGuid(), ClipKind.Files, "f", DateTimeOffset.UtcNow, false, null, null, 20, Files: [new(original)])]);
            await repository.SaveAsync([]);
            Assert.True(File.Exists(original));
            Assert.Equal("keep", await File.ReadAllTextAsync(original));
            Assert.False(StoredClipContentLoader.FilesAvailable([new(original), new(Path.Combine(root, "missing.txt"))]));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RelativePathsAreRejected()
    {
        var history = History();
        Assert.False((await history.CaptureAsync(new(new(ClipKind.Files, Files: [new("relative.txt")]), 1))).Captured);
        Assert.Empty(history.Entries);
    }

    [Fact]
    public void SearchFindsFilename()
    {
        var file = new ClipEntry(
            Guid.NewGuid(),
            ClipKind.Files,
            "f",
            DateTimeOffset.UtcNow,
            false,
            null,
            null,
            20,
            Files: [new(@"C:/Files/Résumé.pdf")]);
        var model = new ShelfViewModel();
        model.Refresh([file]);
        model.Query = "résumé";
        Assert.Equal(file, Assert.Single(model.Entries));
    }

    [Fact]
    public async Task ReferencesPersistWithoutAccessingTheOriginals()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var original = new ClipEntry(
                Guid.NewGuid(),
                ClipKind.Files,
                "f",
                DateTimeOffset.UtcNow,
                true,
                null,
                null,
                20,
                new("Explorer"),
                [new(@"C:/Missing/Résumé.pdf"), new(@"C:/Missing/Folder", true)]);
            var repository = new SqliteHistoryRepository(root);
            await repository.SaveAsync([original]);
            var saved = Assert.Single(await repository.LoadAsync());
            Assert.Equal(original.Files, saved.Files);
            Assert.True(saved.IsPinned);
            Assert.Equal("Explorer", saved.Source?.Name);
            await repository.SaveAsync([]);
            Assert.Empty(await repository.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
