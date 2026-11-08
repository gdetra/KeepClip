namespace KeepClip.Application.Appearance;

public sealed record AppearanceSettings(
    ThemeMode Mode = ThemeMode.System,
    string Palette = "coral",
    bool ColorSurfaces = false);
