using KeepClip.Domain.History;

namespace KeepClip.Storage;

public interface IImageCleanup
{
    void Clean(IReadOnlyList<ClipEntry> retained);
}
