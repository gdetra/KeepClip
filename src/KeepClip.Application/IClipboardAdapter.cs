using KeepClip.Domain.History;

namespace KeepClip.Application;

public interface IClipboardAdapter
{
    Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default);
    Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default);
    // A current-request guard must be safe to evaluate on the clipboard worker.
    Task WriteIfCurrentAsync(
        ClipContent content,
        Guid operationId,
        Func<bool> isCurrent,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!isCurrent())
        {
            throw new ClipboardRequestCancelledException();
        }

        return WriteAsync(content, operationId, ct);
    }
}
