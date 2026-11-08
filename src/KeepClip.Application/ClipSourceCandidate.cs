using KeepClip.Domain.History;

namespace KeepClip.Application;

public sealed record ClipSourceCandidate(string ProcessName, ClipSource? Source);
