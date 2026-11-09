namespace KeepClip.Domain.History;

public sealed record HistoryDecision(
    IReadOnlyList<ClipEntry> Entries,
    IReadOnlyList<Guid> RemovedIds,
    HistoryRejection? Rejection);
