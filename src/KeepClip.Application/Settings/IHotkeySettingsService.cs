namespace KeepClip.Application.Settings;

public interface IHotkeySettingsService
{
    bool TryRegister(Hotkey hotkey);
    bool SetWindowsV(bool enabled);
}
