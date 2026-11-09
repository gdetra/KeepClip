using System.Text.Json;
using KeepClip.Application;
using KeepClip.Application.Appearance;
using KeepClip.Storage;

namespace KeepClip.Tests;

public class ThemeTests
{
    [Theory]
    [InlineData(ThemeMode.System, false, false)]
    [InlineData(ThemeMode.System, true, true)]
    [InlineData(ThemeMode.Light, true, false)]
    [InlineData(ThemeMode.Dark, false, true)]
    public void ExplicitThemeOverridesSystemAndSystemModeFollowsIt(
        ThemeMode mode,
        bool systemDark,
        bool expectedDark) => Assert.Equal(expectedDark, ThemeCatalog.Resolve(new(mode), systemDark).IsDark);

    [Fact]
    public void AllPalettesKeepTextReadableInBothModesAndOnVividSurfaces()
    {
        foreach (var palette in ThemeCatalog.Palettes)
        {
            foreach (var dark in new[]
            {
                false,
                true
            }

            )
            {
                foreach (var tinted in new[]
                {
                    false,
                    true
                }

                )
                {
                    var colors = ThemeCatalog.Resolve(new(ThemeMode.System, palette.Id, tinted), dark);
                    Assert.True(Contrast(colors.Text, colors.Background) >= 4.5, palette.Id + " page text");
                    Assert.True(Contrast(colors.Text, colors.Surface) >= 4.5, palette.Id + " card text");
                    Assert.True(Contrast(colors.SecondaryText, colors.Surface) >= 4.5, palette.Id + " secondary text");
                    Assert.True(Contrast(colors.HeaderText, colors.Header) >= 4.5, palette.Id + " header text");
                    Assert.True(Contrast(colors.AccentText, colors.Surface) >= 4.5, palette.Id + " accent text");
                    foreach (var buttonBackground in new[]
                    {
                        colors.Accent,
                        colors.AccentHover,
                        colors.AccentPressed
                    }

                    )
                    {
                        Assert.True(Contrast(colors.AccentForeground, buttonBackground) >= 4.5, palette.Id + " button text");
                    }
                }
            }
        }
    }

    [Fact]
    public void InvalidPreferencesFallBackToUsableTheme()
    {
        Assert.Equal(new AppearanceSettings(), ThemeCatalog.Normalize(null));
        Assert.Equal(new AppearanceSettings(), ThemeCatalog.Normalize(new((ThemeMode)99, "missing")));
        Assert.Equal(new AppearanceSettings(ThemeMode.Dark, "lime", true), ThemeCatalog.Normalize(new(ThemeMode.Dark, "lime", true)));
    }

    [Fact]
    public async Task AppearanceSurvivesSaveAndReloadWithExistingPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>("{\"Hotkey\":{\"Modifiers\":3,\"Key\":65},\"CopySound\":true,\"CopySoundVolume\":73,\"Appearance\":{\"Mode\":2,\"Palette\":\"lime\",\"ColorSurfaces\":true}}")!;
            var store = new SettingsStore(directory);
            await store.SaveAsync(settings);
            var reloaded = await store.LoadAsync();
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(reloaded));
            Assert.True(document.RootElement.TryGetProperty("Appearance", out var appearance));
            Assert.Equal("lime", appearance.GetProperty("Palette").GetString());
            Assert.Equal(2, appearance.GetProperty("Mode").GetInt32());
            Assert.True(appearance.GetProperty("ColorSurfaces").GetBoolean());
            Assert.Equal(new Hotkey(3, 65), reloaded.Hotkey);
            Assert.True(reloaded.CopySound);
            Assert.Equal(73, reloaded.CopySoundVolume);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
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
