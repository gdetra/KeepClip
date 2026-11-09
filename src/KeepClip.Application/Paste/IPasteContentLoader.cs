using KeepClip.Domain.History;

namespace KeepClip.Application.Paste;

public interface IPasteContentLoader
{
    Task<ClipContent?> LoadAsync(ClipEntry entry, CancellationToken ct = default);
}
