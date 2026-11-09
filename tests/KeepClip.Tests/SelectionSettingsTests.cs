using System.Text.Json;
using KeepClip.Application;
using KeepClip.Storage;

namespace KeepClip.Tests;

public class SelectionSettingsTests
{
    [Fact]
    public void OlderSettingsKeepNewFeaturesDisabled()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"Hotkey\":{\"Modifiers\":6,\"Key\":86},\"IsPaused\":false,\"AutoStart\":true}")!;
        Assert.False(settings.CopyOnSelection);
        Assert.False(settings.CopySound);
        Assert.True(settings.AutoStart);
    }

    [Fact]
    public async Task NewFeaturesRoundtripWithoutChangingOtherPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var original = new AppSettings(new(3, 65), true, true, true, true);
            var store = new SettingsStore(directory);
            await store.SaveAsync(original);
            Assert.Equal(original, await store.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
