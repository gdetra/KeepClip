using KeepClip.Application;
using KeepClip.Domain.History;
using KeepClip.Storage;

namespace KeepClip.Tests;

public sealed class RecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "KeepClip.Recovery", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FailedImageBackupLeavesOriginalDatabaseAndImagesIntact()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        var db = Path.Combine(_root, "history.db");
        var image = Path.Combine(_root, "images", "locked.png");
        await File.WriteAllTextAsync(db, "original-db");
        await File.WriteAllBytesAsync(image, [1, 2, 3]);
        using (var held = File.Open(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => new HistoryRecovery(_root).BackupAsync());
        }

        Assert.Equal("original-db", await File.ReadAllTextAsync(db));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(image));
    }

    [Fact]
    public async Task SuccessfulBackupPreservesCompleteOriginalBeforeReset()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        await File.WriteAllTextAsync(Path.Combine(_root, "history.db"), "database");
        await File.WriteAllBytesAsync(Path.Combine(_root, "images", "one.png"), [4, 5]);
        var backup = await new HistoryRecovery(_root).BackupAsync();
        Assert.Equal("database", await File.ReadAllTextAsync(Path.Combine(backup, "history.db")));
        Assert.Equal(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(Path.Combine(backup, "images", "one.png")));
        Assert.True(File.Exists(Path.Combine(_root, "history.db")));
        Assert.True(File.Exists(Path.Combine(_root, "images", "one.png")));
    }

    [Fact]
    public async Task CleanupFailureAfterCommitDoesNotLeaveHistoryStale()
    {
        var repository = new SqliteHistoryRepository(_root, new DeniedCleanup());
        var service = new HistoryService(repository, (id, bytes, ct) => Task.FromResult("unused"));
        await service.CaptureAsync(new(new(ClipKind.Text, "committed"), 1));
        Assert.Equal("committed", Assert.Single(service.Entries).Text);
        Assert.Equal("committed", Assert.Single(await new SqliteHistoryRepository(_root).LoadAsync()).Text);
    }

    private sealed class DeniedCleanup : IImageCleanup
    {
        public void Clean(
            IReadOnlyList<ClipEntry> retained) => throw new UnauthorizedAccessException("enumeration denied");
    }

    [Fact]
    public async Task CorruptDatabaseCanBeResetOnlyAfterCompleteBackup()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(Path.Combine(_root, "history.db"), [1, 2, 3, 4]);
        var service = new HistoryService(new SqliteHistoryRepository(_root), (id, bytes, ct) => Task.FromResult("unused"));
        await service.ResetWithBackupAsync(new HistoryRecovery(_root).PrepareResetAsync);
        Assert.Empty(await new SqliteHistoryRepository(_root).LoadAsync());
        var backup = Assert.Single(Directory.GetDirectories(_root, "backup-*"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(backup, "history.db")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
