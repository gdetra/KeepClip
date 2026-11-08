using KeepClip.Domain.History;

namespace KeepClip.Application;

public interface IHistoryRepository
{
    Task<IReadOnlyList<ClipEntry>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default);
}
