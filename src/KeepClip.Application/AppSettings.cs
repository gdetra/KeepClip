using KeepClip.Application.Appearance;

namespace KeepClip.Application;

public sealed record AppSettings(
    Hotkey Hotkey,
    bool IsPaused = false,
    bool AutoStart = false,
    bool CopyOnSelection = false,
    bool CopySound = false,
    int CopySoundVolume = 50,
    AppearanceSettings? Appearance = null)
{
    public bool UseWindowsV { get; init; } = true;
    public static AppSettings Default => new(new Hotkey());
}
