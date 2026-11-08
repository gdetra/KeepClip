using KeepClip.Domain.History;

namespace KeepClip.Application.Paste;

public sealed class PasteClip(PasteCoordinator coordinator, IPasteContentLoader loader)
{
    public async Task<PasteResult?> ExecuteAsync(
        ClipEntry entry,
        nint destination,
        Func<bool> isCurrent,
        CancellationToken ct = default)
    {
        var content = await loader.LoadAsync(entry, ct);
        if (content is null)
        {
            return new(PasteOutcome.Cancelled, Failure: PasteFailure.OriginalFilesUnavailable);
        }

        if (!isCurrent())
        {
            return null;
        }

        return await coordinator.PasteAsync(content, destination, ct, isCurrent);
    }
}
