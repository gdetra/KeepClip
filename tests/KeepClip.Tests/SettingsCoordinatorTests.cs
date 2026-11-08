using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.Tests;

public sealed class SettingsCoordinatorTests
{
    [Fact]
    public async Task SuccessfulSavePersistsBeforeApplyingRuntimeEffects()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        var requested = AppSettings.Default with
        {
            CopySound = true,
            AutoStart = true
        };
        var coordinator = fixture.Create();
        var result = await coordinator.ApplyAsync(requested);
        Assert.Equal(SettingsApplyStatus.Applied, result.Status);
        Assert.Equal(requested, fixture.Store.Saved);
        Assert.Equal(requested, result.EffectiveSettings);
        Assert.Equal(requested, coordinator.CurrentSettings);
        Assert.Equal(new[] { "save", "windows-v", "runtime" }, fixture.Effects);
    }

    [Fact]
    public async Task OccupiedShortcutRejectsWithoutChangingPersistenceOrStartup()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        fixture.Hotkeys.Available = false;
        var coordinator = fixture.Create();
        var result = await coordinator.ApplyAsync(AppSettings.Default with { Hotkey = new(3, 65), AutoStart = true });
        Assert.Equal(SettingsApplyStatus.Rejected, result.Status);
        Assert.Equal(SettingsApplyFailure.AlternativeHotkeyInUse, result.Failure);
        Assert.Null(fixture.Store.Saved);
        Assert.False(fixture.Startup.Enabled);
        Assert.Equal(AppSettings.Default, result.EffectiveSettings);
    }

    [Fact]
    public async Task DisablingWindowsVRetainsThePreviousAlternativeWhenRegistrationFails()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        fixture.Hotkeys.Available = false;
        var coordinator = fixture.Create();
        var result = await coordinator.ApplyAsync(AppSettings.Default with { Hotkey = new(3, 65), UseWindowsV = false });
        Assert.Equal(SettingsApplyStatus.AppliedWithWarnings, result.Status);
        Assert.True(result.Warnings.HasFlag(SettingsApplyWarnings.AlternativeHotkeyUnavailable));
        Assert.Equal(new Hotkey(), fixture.Store.Saved?.Hotkey);
        Assert.False(fixture.Hotkeys.WindowsVEnabled);
    }

    [Fact]
    public async Task PersistenceFailureRestoresStartupAndThePreviousHotkey()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        fixture.Store.Failure = new IOException("private path");
        var coordinator = fixture.Create();
        var result = await coordinator.ApplyAsync(AppSettings.Default with { Hotkey = new(3, 65), AutoStart = true });
        Assert.Equal(SettingsApplyStatus.Rejected, result.Status);
        Assert.Equal(SettingsApplyFailure.PersistenceFailed, result.Failure);
        Assert.Equal(AppSettings.Default, coordinator.CurrentSettings);
        Assert.Equal(new Hotkey(), fixture.Hotkeys.Current);
        Assert.False(fixture.Startup.Enabled);
        Assert.DoesNotContain("runtime", fixture.Effects);
    }

    [Fact]
    public async Task RollbackFailureDoesNotPreventOtherRollbackSteps()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        fixture.Store.Failure = new IOException("save failed");
        fixture.Hotkeys.RejectRestore = true;
        var coordinator = fixture.Create();
        var result = await coordinator.ApplyAsync(AppSettings.Default with { Hotkey = new(3, 65), AutoStart = true });
        Assert.True(result.Warnings.HasFlag(SettingsApplyWarnings.RollbackIncomplete));
        Assert.False(fixture.Startup.Enabled);
        Assert.Equal(AppSettings.Default, coordinator.CurrentSettings);
    }

    [Fact]
    public async Task UnexpectedSaveFailureIsRolledBackAndPropagates()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        fixture.Store.Failure = new InvalidOperationException("defect");
        var coordinator = fixture.Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ApplyAsync(AppSettings.Default with { Hotkey = new(3, 65), AutoStart = true }));
        Assert.Equal(new Hotkey(), fixture.Hotkeys.Current);
        Assert.False(fixture.Startup.Enabled);
        Assert.Equal(AppSettings.Default, coordinator.CurrentSettings);
    }

    [Fact]
    public async Task AllWarningsAreRetainedAndEffectiveStartupStateIsReturned()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        fixture.Startup.Available = false;
        fixture.Hotkeys.WindowsVAvailable = false;
        fixture.RuntimeWarnings = SettingsApplyWarnings.StorageUnavailable | SettingsApplyWarnings.SelectionCaptureUnavailable;
        var coordinator = fixture.Create();
        var result = await coordinator.ApplyAsync(AppSettings.Default with { AutoStart = true });
        Assert.Equal(SettingsApplyStatus.AppliedWithWarnings, result.Status);
        Assert.Equal(SettingsApplyWarnings.StartupUnavailable | SettingsApplyWarnings.WindowsVUnavailable | SettingsApplyWarnings.StorageUnavailable | SettingsApplyWarnings.SelectionCaptureUnavailable, result.Warnings);
        Assert.False(result.EffectiveSettings.AutoStart);
        Assert.False(result.Startup?.Available);
    }

    [Fact]
    public async Task ConcurrentPauseRequestsToggleTheLatestPersistedState()
    {
        SettingsCoordinatorFixture fixture = new SettingsCoordinatorFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = () => release.Task;
        var coordinator = fixture.Create();
        var first = coordinator.TogglePauseAsync();
        var second = coordinator.TogglePauseAsync();
        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.True((await first).EffectiveSettings.IsPaused);
        Assert.False((await second).EffectiveSettings.IsPaused);
        Assert.False(coordinator.CurrentSettings.IsPaused);
        Assert.Equal(2, fixture.Store.Saves);
    }
}
