using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.Tests;

internal sealed class HotkeySettingsStub(List<string> effects) : IHotkeySettingsService
{
    public bool Available { get; set; } = true;
    public bool WindowsVAvailable { get; set; } = true;
    public bool WindowsVEnabled { get; private set; } = true;
    public bool RejectRestore { get; set; }
    public Hotkey Current { get; private set; } = new();

    public bool TryRegister(Hotkey hotkey)
    {
        if (!Available || (RejectRestore && hotkey == new Hotkey()))
        {
            return false;
        }

        Current = hotkey;
        return true;
    }

    public bool SetWindowsV(bool enabled)
    {
        effects.Add("windows-v");
        WindowsVEnabled = enabled;
        return WindowsVAvailable;
    }
}
