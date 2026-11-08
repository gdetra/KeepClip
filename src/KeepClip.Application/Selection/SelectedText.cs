using KeepClip.Domain.History;

namespace KeepClip.Application.Selection;

public sealed record SelectedText(
    string? Text,
    bool IsPassword = false,
    bool IsOwnApp = false,
    ClipSource? Source = null,
    bool? IsReadOnly = true);
