using KeepClip.App.Composition;
using KeepClip.App.Settings;
using KeepClip.Application;
using KeepClip.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace KeepClip.Tests;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public async Task ReopeningSettingsUsesLatestSavedStateAndAnIndependentDraft()
    {
        var fixture = new SettingsCoordinatorFixture();
        var services = new ServiceCollection();
        services.AddSingleton<IStartupService>(fixture.Startup);
        services.AddSingleton<IHotkeySettingsService>(fixture.Hotkeys);
        services.AddKeepClipApplication("unused-data", AppSettings.Default, fixture.Store, Callbacks());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var first = provider.GetRequiredService<SettingsViewModel>();
        first.CopySound = true;
        await first.SaveCommand.ExecuteAsync(null);
        var second = provider.GetRequiredService<SettingsViewModel>();
        Assert.NotSame(first, second);
        Assert.True(second.CopySound);
        first.CopySound = false;
        Assert.True(second.CopySound);
        Assert.True(provider.GetRequiredService<SettingsCoordinator>().CurrentSettings.CopySound);
        Assert.Same(provider.GetRequiredService<SettingsCoordinator>(), provider.GetRequiredService<SettingsCoordinator>());
    }

    private static AppCallbacks Callbacks() => new(
        () => AppSettings.Default,
        () => false,
        () => { },
        _ => { },
        () => { },
        _ => SettingsApplyWarnings.None,
        () => Task.CompletedTask);
}
