using System.Text;
using System.Text.Json;
using KeepClip.Domain.History;

namespace KeepClip.Application.History;

public static class FileReferences
{
    public const int MaxItems = 1000;

    public static bool Valid(IReadOnlyList<ClipFile>? files)
    {
        if (files is not { Count: > 0 and <= MaxItems })
        {
            return false;
        }

        try
        {
            return files.All(file => !string.IsNullOrWhiteSpace(file.Path) && Path.IsPathFullyQualified(file.Path) && Path.GetFullPath(file.Path).Length <= 32767);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    public static byte[] Identity(IReadOnlyList<ClipFile> files)
    {
        var canonical = files.Select(file => new ClipFile(Path.GetFullPath(file.Path), file.IsDirectory)).Distinct().OrderBy(file => file.Path, StringComparer.Ordinal).ThenBy(file => file.IsDirectory).ToArray();
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical));
    }
}
