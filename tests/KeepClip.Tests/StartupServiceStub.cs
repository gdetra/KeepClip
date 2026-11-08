using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.Tests;

internal sealed class StartupServiceStub : IStartupService
{
    public bool Enabled { get; private set; }
    public bool Available { get; set; } = true;

    public Task<StartupState> GetAsync() => Task.FromResult(new StartupState(Enabled, Available, null));
    public Task<StartupState> SetEnabledAsync(bool enabled)
    {
        Enabled = enabled;
        return GetAsync();
    }
}
