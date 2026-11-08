using KeepClip.Domain.History;
using KeepClip.Storage;

namespace KeepClip.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RoundtripPreservesUnicodeAndImageBytes()
    {
        byte[] bytes = [137, 80, 78, 71, 1, 2, 3];
        var id = Guid.NewGuid();
        var path = await new ImageStore(_root).SaveAsync(id, bytes);
        var entries = new[]
        {
            new ClipEntry(Guid.NewGuid(), ClipKind.Text, "t", DateTimeOffset.UnixEpoch, true, "Caffè\n日本語", null, 18),
            new ClipEntry(id, ClipKind.Image, "i", DateTimeOffset.UtcNow, false, null, path, 7)
        };
        await new SqliteHistoryRepository(_root).SaveAsync(entries);
        var loaded = await new SqliteHistoryRepository(_root).LoadAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, x => x.Text == "Caffè\n日本語" && x.IsPinned);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(loaded.Single(x => x.Kind == ClipKind.Image).ImagePath!));
    }

    [Fact]
    public async Task MissingImageIsExcludedAndOrphansAreCleaned()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        var orphan = Path.Combine(_root, "images", "orphan.png");
        await File.WriteAllBytesAsync(orphan, [1]);
        var repository = new SqliteHistoryRepository(_root);
        await repository.SaveAsync([new ClipEntry(
            Guid.NewGuid(),
            ClipKind.Image,
            "missing",
            DateTimeOffset.UtcNow,
            false,
            null,
            Path.Combine(_root, "images", "missing.png"),
            1)]);
        Assert.Empty(await repository.LoadAsync());
        Assert.False(File.Exists(orphan));
        Assert.Empty(await new SqliteHistoryRepository(_root).LoadAsync());
    }

    [Fact]
    public async Task CorruptDatabaseIsPreserved()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "history.db");
        byte[] bad = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(path, bad);
        await Assert.ThrowsAsync<HistoryStorageException>(() => new SqliteHistoryRepository(_root).LoadAsync());
        Assert.Equal(bad, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task UnwritableStorageDoesNotReportSuccess()
    {
        Directory.CreateDirectory(_root);
        var occupied = Path.Combine(_root, "file");
        await File.WriteAllTextAsync(occupied, "keep");
        await Assert.ThrowsAnyAsync<Exception>(() => new SqliteHistoryRepository(occupied).SaveAsync([]));
        Assert.Equal("keep", await File.ReadAllTextAsync(occupied));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
