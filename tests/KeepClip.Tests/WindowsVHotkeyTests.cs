using System.Text.Json;
using KeepClip.App.Desktop;
using KeepClip.Application;
using KeepClip.Storage;

namespace KeepClip.Tests;

public class WindowsVHotkeyTests
{
    [Fact]
    public async Task WindowsVIsEnabledByDefaultAndAnExplicitOptOutSurvivesReload()
    {
        Assert.True(AppSettings.Default.UseWindowsV);
        var legacy = JsonSerializer.Deserialize<AppSettings>("""{"Hotkey":{"Modifiers":6,"Key":86}}""");
        Assert.True(legacy!.UseWindowsV);
        using var directory = new TemporarySettingsDirectory();
        var store = new SettingsStore(directory.Path);
        await store.SaveAsync(AppSettings.Default with { UseWindowsV = false });
        Assert.False((await store.LoadAsync()).UseWindowsV);
    }

    [Fact]
    public void AcceptedPressSuppressesRepeatsAndReleaseButInvokesOnlyOnce()
    {
        var gesture = new WindowsVGesture();
        var calls = 0;
        bool Invoke()
        {
            calls++;
            return true;
        }

        Assert.True(gesture.Handle(down: true, windowsOnly: true, Invoke));
        Assert.True(gesture.Handle(down: true, windowsOnly: true, Invoke));
        Assert.True(gesture.Handle(down: true, windowsOnly: false, Invoke));
        Assert.True(gesture.Handle(down: false, windowsOnly: false, Invoke));
        Assert.False(gesture.Handle(down: true, windowsOnly: false, Invoke));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AKeyAlreadyHeldAndARejectedInvocationBothPassThrough()
    {
        var gesture = new WindowsVGesture();
        gesture.Reset(vAlreadyDown: true);
        Assert.False(gesture.Handle(down: true, windowsOnly: true, () => throw new InvalidOperationException()));
        Assert.False(gesture.Handle(down: false, windowsOnly: true, () => throw new InvalidOperationException()));
        Assert.False(gesture.Handle(down: true, windowsOnly: true, () => false));
        Assert.False(gesture.Handle(down: true, windowsOnly: true, () => throw new InvalidOperationException()));
        Assert.False(gesture.Handle(down: false, windowsOnly: true, () => throw new InvalidOperationException()));
        Assert.True(gesture.Handle(down: true, windowsOnly: true, () => true));
    }

    private sealed class TemporarySettingsDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("keepclip-win-v-").FullName;

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Path))
            {
                File.Delete(file);
            }

            Directory.Delete(Path);
        }
    }
}
