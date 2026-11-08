using KeepClip.Application;

namespace KeepClip.App.Composition;

internal sealed record StartupSettings(AppSettings Settings, string? Error);
