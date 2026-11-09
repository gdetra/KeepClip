using KeepClip.Domain.History;

namespace KeepClip.Application;

public sealed record ClipboardSnapshot(
    ClipContent Content,
    uint Sequence,
    bool IsOwnWrite = false,
    long? CaptureGeneration = null,
    ClipSource? Source = null,
    ClipboardSourceContext? SourceContext = null,
    Func<ClipContent>? PrepareContent = null);
