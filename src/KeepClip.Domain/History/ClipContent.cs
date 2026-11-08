namespace KeepClip.Domain.History;

public sealed record ClipContent(
    ClipKind Kind,
    string? Text = null,
    byte[]? Png = null,
    IReadOnlyList<ClipFile>? Files = null);
