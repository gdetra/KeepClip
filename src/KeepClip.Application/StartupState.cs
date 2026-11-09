namespace KeepClip.Application;

public sealed record StartupState(bool Enabled, bool Available, string? Message);
