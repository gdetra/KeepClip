namespace KeepClip.Application.Appearance;

public sealed record ThemeColors(
    bool IsDark,
    uint Background,
    uint Surface,
    uint Header,
    uint Text,
    uint SecondaryText,
    uint HeaderText,
    uint Border,
    uint Accent,
    uint AccentText,
    uint AccentForeground,
    uint AccentHover,
    uint AccentPressed,
    uint Selection);
