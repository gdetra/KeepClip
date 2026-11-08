using KeepClip.App.Settings;
using KeepClip.Application;
using KeepClip.Application.Appearance;
using KeepClip.Application.Settings;

namespace KeepClip.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public async Task AppliedWarningsRefreshEffectiveHotkeyAndStartupState()
    {
        var effective = AppSettings.Default with
        {
            UseWindowsV = false
        };
        var model = Create(apply: _ => Task.FromResult(ApplySettingsResult.Applied(
            effective,
            new StartupState(false, false, "Unavailable"),
            SettingsApplyWarnings.AlternativeHotkeyUnavailable)));
        model.Letter = "A";
        model.ModifierIndex = 1;
        model.AutoStart = true;
        await model.SaveCommand.ExecuteAsync(null);
        Assert.Equal("V", model.Letter);
        Assert.Equal(0, model.ModifierIndex);
        Assert.False(model.AutoStart);
        Assert.False(model.StartupAvailable);
        Assert.Equal("Unavailable", model.StartupHint);
        Assert.True(model.IsStatusOpen);
    }

    [Fact]
    public async Task SaveCompletionPreservesNewerEdits()
    {
        var completion = new TaskCompletionSource<ApplySettingsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = Create(apply: _ => completion.Task);
        var save = model.SaveCommand.ExecuteAsync(null);
        model.Letter = "B";
        model.ModifierIndex = 2;
        model.AutoStart = true;
        completion.SetResult(Applied(AppSettings.Default));
        await save;
        Assert.Equal("B", model.Letter);
        Assert.Equal(2, model.ModifierIndex);
        Assert.True(model.AutoStart);
    }

    [Fact]
    public async Task RejectedSaveRetainsTheDraftForCorrection()
    {
        var model = Create(apply: _ => Task.FromResult(ApplySettingsResult.Rejected(AppSettings.Default, SettingsApplyFailure.AlternativeHotkeyInUse)));
        model.Letter = "A";
        await model.SaveCommand.ExecuteAsync(null);
        Assert.Equal("A", model.Letter);
        Assert.True(model.IsStatusOpen);
        Assert.True(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SqliteRecoveryFailurePreservesDataAndAllowsRetry()
    {
        var directory = Directory.CreateTempSubdirectory("keepclip-recovery-").FullName;
        try
        {
            var database = Path.Combine(directory, "history.db");
            await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
            var model = Create(recover: () => new KeepClip.Storage.SqliteHistoryRepository(directory).SaveAsync([]));
            await model.RecoverCommand.ExecuteAsync(null);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(database));
            Assert.True(model.IsStatusOpen);
            Assert.True(model.SaveCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("AB")]
    public async Task InvalidShortcutDoesNotReachPersistence(string letter)
    {
        var saved = false;
        var model = Create(apply: _ =>
        {
            saved = true;
            return Task.FromResult(Applied(AppSettings.Default));
        });
        model.Letter = letter;
        await model.SaveCommand.ExecuteAsync(null);
        Assert.False(saved);
        Assert.True(model.IsStatusOpen);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task SaveMapsEditedSettingsAndNormalizesTheShortcut()
    {
        AppSettings? saved = null;
        var model = Create(apply: settings =>
        {
            saved = settings;
            return Task.FromResult(Applied(AppSettings.Default));
        });
        model.Letter = " a ";
        model.ModifierIndex = 2;
        model.UseWindowsV = false;
        model.CaptureEnabled = false;
        model.AutoStart = true;
        model.CopyOnSelection = true;
        model.CopySound = true;
        model.SoundVolume = 73.4;
        model.ThemeIndex = 2;
        model.SelectedPalette = "ocean";
        model.ColorSurfaces = true;
        await model.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new AppSettings(new Hotkey(5, 65), true, true, true, true, 73, new AppearanceSettings(ThemeMode.Dark, "ocean", true)) { UseWindowsV = false }, saved);
        Assert.False(model.IsBusy);
        Assert.True(model.IsStatusOpen);
    }

    [Fact]
    public async Task SaveAndRecoveryCannotOverlap()
    {
        var completion = new TaskCompletionSource<ApplySettingsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var recoveries = 0;
        var saves = 0;
        var model = Create(apply: _ =>
        {
            saves++;
            return completion.Task;
        }, recover: () =>
        {
            recoveries++;
            return Task.CompletedTask;
        });
        var save = model.SaveCommand.ExecuteAsync(null);
        Assert.True(model.IsBusy);
        Assert.False(model.RecoverCommand.CanExecute(null));
        await model.RecoverCommand.ExecuteAsync(null);
        await model.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, saves);
        Assert.Equal(0, recoveries);
        completion.SetResult(Applied(AppSettings.Default));
        await save;
        Assert.True(model.RecoverCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExpectedSaveFailureAllowsRetry()
    {
        var model = Create(apply: _ => throw new IOException("private payload"));
        await model.SaveCommand.ExecuteAsync(null);
        Assert.DoesNotContain("private payload", model.StatusMessage);
        Assert.True(model.IsStatusOpen);
        Assert.True(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnexpectedSaveFailurePropagates()
    {
        var model = Create(apply: _ => throw new InvalidOperationException("programming defect"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.SaveCommand.ExecuteAsync(null));
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task StartupRefreshReflectsWindowsAvailabilityAndActualState()
    {
        var model = Create(startup: () => Task.FromResult(new StartupState(false, false, "Disabled by Windows")));
        model.AutoStart = true;
        await model.RefreshStartupCommand.ExecuteAsync(null);
        Assert.False(model.AutoStart);
        Assert.False(model.StartupAvailable);
        Assert.Equal("Disabled by Windows", model.StartupHint);
    }

    [Fact]
    public async Task RecoveryFailureReleasesTheBusyState()
    {
        var model = Create(recover: () => throw new UnauthorizedAccessException("private path"));
        await model.RecoverCommand.ExecuteAsync(null);
        Assert.True(model.IsStatusOpen);
        Assert.True(model.SaveCommand.CanExecute(null));
        Assert.DoesNotContain("private path", model.StatusMessage);
    }

    [Fact]
    public void ResetThemeRestoresDefaultsWithoutChangingOtherSettings()
    {
        var model = Create();
        model.CopySound = true;
        model.ThemeIndex = 2;
        model.SelectedPalette = "ocean";
        model.ColorSurfaces = true;
        model.ResetThemeCommand.Execute(null);
        Assert.Equal(new AppearanceSettings(), model.Appearance);
        Assert.True(model.CopySound);
    }

    private static ApplySettingsResult Applied(
        AppSettings settings) => ApplySettingsResult.Applied(settings, new StartupState(settings.AutoStart, true, null));

    private static SettingsViewModel Create(
        Func<AppSettings, Task<ApplySettingsResult>>? apply = null,
        Func<Task<StartupState>>? startup = null,
        Func<Task>? recover = null) => new(
            AppSettings.Default,
            "data",
            apply ?? (settings => Task.FromResult(Applied(settings))),
            startup ?? (() => Task.FromResult(new StartupState(true, true, null))),
            recover ?? (() => Task.CompletedTask));
}
