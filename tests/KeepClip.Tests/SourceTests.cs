using KeepClip.Domain.History;
using KeepClip.Storage;
using Microsoft.Data.Sqlite;

namespace KeepClip.Tests;

public sealed class SourceTests
{
    [Fact]
    public void DuplicateUpdatesOriginAndPreservesPin()
    {
        var old = new ClipEntry(Guid.NewGuid(), ClipKind.Text, "hash", DateTimeOffset.UnixEpoch, true, "text", null, 4, new("Old"));
        var incoming = old with
        {
            Id = Guid.NewGuid(),
            CopiedAt = DateTimeOffset.UtcNow,
            Source = new("New", [1, 2])
        };
        var entry = Assert.Single(new HistoryPolicy().Apply([old], incoming).Entries);
        Assert.Equal(old.Id, entry.Id);
        Assert.True(entry.IsPinned);
        Assert.Equal("New", entry.Source?.Name);
        Assert.Null(Assert.Single(new HistoryPolicy().Apply([old], incoming with { Source = null }).Entries).Source);
    }

    [Fact]
    public async Task LegacyDatabaseMigratesAndPersistsOrigin()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (var db = new SqliteConnection($"Data Source={Path.Combine(root, "history.db")};Pooling=False"))
            {
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = "CREATE TABLE clips (id TEXT PRIMARY KEY,kind INTEGER,hash TEXT,copied TEXT,pinned INTEGER,text TEXT,image TEXT,size INTEGER); INSERT INTO clips VALUES ('00000000-0000-0000-0000-000000000001',0,'h','1970-01-01T00:00:00+00:00',1,'text',NULL,4)";
                command.ExecuteNonQuery();
            }

            var repository = new SqliteHistoryRepository(root);
            var legacy = Assert.Single(await repository.LoadAsync());
            Assert.True(legacy.IsPinned);
            Assert.Equal("text", legacy.Text);
            Assert.Null(legacy.Source);
            await repository.SaveAsync([legacy with { Source = new("Caffè 日本語", [1, 2, 3]) }]);
            var saved = Assert.Single(await repository.LoadAsync());
            Assert.Equal("Caffè 日本語", saved.Source?.Name);
            Assert.Equal(new byte[] { 1, 2, 3 }, saved.Source?.IconPng);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
