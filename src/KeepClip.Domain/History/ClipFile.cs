namespace KeepClip.Domain.History;

public sealed record ClipFile(string Path, bool IsDirectory = false);
