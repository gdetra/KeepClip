using KeepClip.Domain.History;

namespace KeepClip.Tests;

public class HistoryPolicyTests
{
    private static ClipEntry Entry(int key, bool pin = false, long size = 10, ClipKind kind = ClipKind.Text) => new(
        Guid.NewGuid(),
        kind,
        key.ToString(),
        DateTimeOffset.UnixEpoch.AddSeconds(key),
        pin,
        key.ToString(),
        null,
        size);

    [Fact]
    public void DuplicateMovesToFrontPreservingPin()
    {
        var original = Entry(1, true);
        var result = new HistoryPolicy().Apply([original, Entry(2)], Entry(1) with { CopiedAt = DateTimeOffset.UtcNow });
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(original.Id, result.Entries[0].Id);
        Assert.True(result.Entries[0].IsPinned);
    }

    [Fact]
    public void EvictsOldestUnpinnedAt201()
    {
        var entries = Enumerable.Range(1, 200).Select(i => Entry(i, i == 1)).ToArray();
        var result = new HistoryPolicy().Apply(entries, Entry(201));
        Assert.Equal(200, result.Entries.Count);
        Assert.Contains(entries[0], result.Entries);
        Assert.Contains(entries[1].Id, result.RemovedIds);
    }

    [Fact]
    public void AllPinnedRejectsIncoming()
    {
        var entries = Enumerable.Range(1, 200).Select(i => Entry(i, true)).ToArray();
        var result = new HistoryPolicy().Apply(entries, Entry(201));
        Assert.Equal(HistoryRejection.CapacityReservedForFavorites, result.Rejection);
        Assert.Equal(entries.Select(x => x.Id), result.Entries.Select(x => x.Id));
        Assert.Empty(result.RemovedIds);
    }

    [Fact]
    public void RejectsOversizedPng()
    {
        var result = new HistoryPolicy().Apply([], Entry(1, size: 20L * 1024 * 1024 + 1, kind: ClipKind.Image));
        Assert.Empty(result.Entries);
        Assert.Equal(HistoryRejection.ContentTooLarge, result.Rejection);
    }

    [Fact]
    public void EnforcesTotalBytes()
    {
        var pin = Entry(1, true, 100L * 1024 * 1024);
        var old = Entry(2, size: 100L * 1024 * 1024);
        var result = new HistoryPolicy().Apply([pin, old], Entry(3, size: 100L * 1024 * 1024));
        Assert.Null(result.Rejection);
        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(pin, result.Entries);
        Assert.Contains(old.Id, result.RemovedIds);
        Assert.True(result.Entries.Sum(x => x.SizeBytes) <= 250L * 1024 * 1024);
    }

    [Fact]
    public void IncomingTooLargeDoesNotEvictExistingHistory()
    {
        var original = Entry(1);
        var result = new HistoryPolicy().Apply([original], Entry(2, size: 251L * 1024 * 1024));
        Assert.Equal(HistoryRejection.ContentTooLarge, result.Rejection);
        Assert.Equal(original, Assert.Single(result.Entries));
    }
}
