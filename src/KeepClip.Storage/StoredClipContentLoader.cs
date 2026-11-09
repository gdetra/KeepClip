using KeepClip.Application.History;
using KeepClip.Application.Paste;
using KeepClip.Domain.History;

namespace KeepClip.Storage;

public sealed class StoredClipContentLoader : IPasteContentLoader
{
    public async Task<ClipContent?> LoadAsync(ClipEntry entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (entry.Kind == ClipKind.Text)
        {
            return new(ClipKind.Text, entry.Text);
        }

        if (entry.Kind == ClipKind.Files)
        {
            if (!FileReferences.Valid(entry.Files))
            {
                throw new IOException("Riferimenti ai file non validi.");
            }

            if (!await Task.Run(() => FilesAvailable(entry.Files!), ct))
            {
                return null;
            }

            return new(ClipKind.Files, Files: entry.Files);
        }

        return new(ClipKind.Image, Png: await File.ReadAllBytesAsync(entry.ImagePath ?? throw new IOException(), ct));
    }

    public static bool FilesAvailable(
        IReadOnlyList<ClipFile> files) => files.All(file => file.IsDirectory ? Directory.Exists(file.Path) : File.Exists(file.Path));
}
