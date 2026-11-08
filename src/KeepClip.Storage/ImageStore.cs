namespace KeepClip.Storage;

public sealed class ImageStore(string directory)
{
    public async Task<string> SaveAsync(Guid id, byte[] png, CancellationToken ct = default)
    {
        var folder = Path.Combine(directory, "images");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, id.ToString("N") + ".png");
        var temp = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, png, ct);
            File.Move(temp, path, true);
            return path;
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
