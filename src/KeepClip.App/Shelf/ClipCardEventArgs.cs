using KeepClip.Domain.History;

namespace KeepClip.App.Shelf;

public sealed class ClipCardEventArgs(ClipEntry entry) : EventArgs
{
    public ClipEntry Entry { get; } = entry;
}
