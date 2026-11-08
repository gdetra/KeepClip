namespace KeepClip.Application.Appearance;

public static class ThemeCatalog
{
    public static IReadOnlyList<ThemePalette> Palettes { get; } = Array.AsReadOnly(new[] { new ThemePalette("coral", "Corallo", 0xFFFF6B5E), new ThemePalette("ocean", "Blu elettrico", 0xFF3B82F6), new ThemePalette("violet", "Viola", 0xFFA78BFA), new ThemePalette("fuchsia", "Fucsia neon", 0xFFFF2DAA), new ThemePalette("cyan", "Ciano", 0xFF00D9FF), new ThemePalette("lime", "Lime neon", 0xFFBEFF00), new ThemePalette("orange", "Arancio acceso", 0xFFFF8A00), new ThemePalette("red", "Rosso pop", 0xFFFF3B5C) });

    public static AppearanceSettings Normalize(AppearanceSettings? settings)
    {
        settings ??= new();
        return settings with
        {
            Mode = Enum.IsDefined(settings.Mode) ? settings.Mode : ThemeMode.System,
            Palette = Palettes.Any(x => x.Id == settings.Palette) ? settings.Palette : "coral"
        };
    }

    public static ThemeColors Resolve(AppearanceSettings? settings, bool systemDark)
    {
        settings = Normalize(settings);
        var dark = settings.Mode == ThemeMode.Dark || settings.Mode == ThemeMode.System && systemDark;
        var accent = Palettes.First(x => x.Id == settings.Palette).Accent;
        var background = dark ? 0xFF17181D : 0xFFF6F7FA;
        var surface = dark ? 0xFF25262E : 0xFFFFFFFF;
        if (settings.ColorSurfaces)
        {
            background = Mix(background, accent, dark ? 0.16 : 0.10);
            surface = Mix(surface, accent, dark ? 0.18 : 0.05);
        }

        var text = dark ? 0xFFF8F8FC : 0xFF17181D;
        var header = settings.ColorSurfaces ? accent : Mix(surface, accent, 0.08);
        var accentText = accent;
        // Keep small colored labels readable, including lime/cyan on a light card.
        while (Contrast(accentText, surface) < 4.5)
        {
            accentText = Mix(accentText, dark ? 0xFFFFFFFF : 0xFF000000, 0.12);
        }

        var accentForeground = Contrast(0xFF000000, accent) >= Contrast(0xFFFFFFFF, accent) ? 0xFF000000 : 0xFFFFFFFF;
        var awayFromText = accentForeground == 0xFF000000 ? 0xFFFFFFFF : 0xFF000000;
        return new(
            dark,
            background,
            surface,
            header,
            text,
            Mix(text, background, 0.25),
            Contrast(0xFF000000, header) >= Contrast(0xFFFFFFFF, header) ? 0xFF000000 : 0xFFFFFFFF,
            Mix(surface, text, 0.20),
            accent,
            accentText,
            accentForeground,
            Mix(accent, awayFromText, 0.10),
            Mix(accent, awayFromText, 0.18),
            Mix(surface, accent, 0.12));
    }

    private static uint Mix(uint first, uint second, double amount)
    {
        uint Channel(
            int shift) => (uint)Math.Round(((first >> shift) & 255) * (1 - amount) + ((second >> shift) & 255) * amount);
        return 0xFF000000 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    private static double Contrast(uint first, uint second)
    {
        static double Luminance(uint value)
        {
            static double Linear(uint channel)
            {
                var x = channel / 255d;
                return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Linear((value >> 16) & 255) + 0.7152 * Linear((value >> 8) & 255) + 0.0722 * Linear(value & 255);
        }

        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
