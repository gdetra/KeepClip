namespace KeepClip.Domain.History;

public sealed record ClipEntry(
    Guid Id,
    ClipKind Kind,
    string Hash,
    DateTimeOffset CopiedAt,
    bool IsPinned,
    string? Text,
    string? ImagePath,
    long SizeBytes,
    ClipSource? Source = null,
    IReadOnlyList<ClipFile>? Files = null);
