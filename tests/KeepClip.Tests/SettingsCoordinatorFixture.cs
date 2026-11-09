using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.Tests;

internal sealed class SettingsCoordinatorFixture
{
    public List<string> Effects { get; } = [];
    public SettingsStoreStub Store { get; }
    public StartupServiceStub Startup { get; } = new();
    public HotkeySettingsStub Hotkeys { get; }
    public SettingsApplyWarnings RuntimeWarnings { get; set; }

    public SettingsCoordinatorFixture()
    {
        Store = new(Effects);
        Hotkeys = new(Effects);
    }

    public SettingsCoordinator Create() => new(
        AppSettings.Default,
        Store,
        Startup,
        Hotkeys,
        ApplyRuntime);

    private SettingsApplyWarnings ApplyRuntime(AppSettings settings)
    {
        Effects.Add("runtime");
        return RuntimeWarnings;
    }

}
