using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.App.Composition;

internal sealed record AppCallbacks(
    Func<AppSettings> ReadSettings,
    Func<bool> SelectionEnabled,
    Action Copied,
    Action<string> Notify,
    Action OpenShelf,
    Func<AppSettings, SettingsApplyWarnings> ApplyRuntime,
    Func<Task> Recover);
