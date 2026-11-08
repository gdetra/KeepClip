namespace KeepClip.Domain.History;

public sealed class HistoryPolicy
{
    public const int MaxEntries = 200;
    public const long MaxImageBytes = 20L * 1024 * 1024;
    public const long MaxTotalBytes = 250L * 1024 * 1024;

    public static IReadOnlyList<ClipEntry> TogglePin(
        IReadOnlyList<ClipEntry> entries,
        Guid id) => entries.Select(entry => entry.Id == id ? entry with { IsPinned = !entry.IsPinned } : entry).ToArray();

    public static IReadOnlyList<ClipEntry> Delete(
        IReadOnlyList<ClipEntry> entries,
        Guid id) => entries.Where(entry => entry.Id != id).ToArray();

    public static IReadOnlyList<ClipEntry> Clear(
        IReadOnlyList<ClipEntry> entries,
        bool includePinned) => entries.Where(entry => !includePinned && entry.IsPinned).ToArray();

    public HistoryDecision Apply(IReadOnlyList<ClipEntry> entries, ClipEntry incoming)
    {
        HistoryDecision Reject(HistoryRejection reason) => new(entries, [], reason);
        if (incoming.SizeBytes < 0 || incoming.SizeBytes > MaxTotalBytes || (incoming.Kind == ClipKind.Image && incoming.SizeBytes > MaxImageBytes))
        {
            return Reject(HistoryRejection.ContentTooLarge);
        }

        var duplicate = entries.FirstOrDefault(x => x.Hash == incoming.Hash && x.Kind == incoming.Kind);
        if (duplicate is not null)
        {
            var moved = duplicate with
            {
                CopiedAt = incoming.CopiedAt,
                Source = incoming.Source
            };
            return new(
                new[] { moved }.Concat(entries.Where(x => x.Id != duplicate.Id).OrderByDescending(x => x.CopiedAt)).ToArray(),
                [],
                null);
        }

        var result = entries.Append(incoming).OrderByDescending(x => x.CopiedAt).ToList();
        var removed = new List<Guid>();
        while (result.Count > MaxEntries || result.Sum(x => x.SizeBytes) > MaxTotalBytes)
        {
            var oldest = result.Where(x => !x.IsPinned && x.Id != incoming.Id).MinBy(x => x.CopiedAt);
            if (oldest is null)
            {
                return Reject(HistoryRejection.CapacityReservedForFavorites);
            }

            result.Remove(oldest);
            removed.Add(oldest.Id);
        }

        return new(result, removed, null);
    }
}
