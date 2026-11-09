namespace KeepClip.Application.Settings;

[Flags]
public enum SettingsApplyWarnings
{
    None = 0,
    AlternativeHotkeyUnavailable = 1,
    StartupUnavailable = 2,
    StartupNotApplied = 4,
    WindowsVUnavailable = 8,
    SelectionCaptureUnavailable = 16,
    StorageUnavailable = 32,
    RollbackIncomplete = 64,
}
