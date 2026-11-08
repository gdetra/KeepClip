using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;

namespace KeepClip.Application.Settings;

public sealed class SettingsCoordinator
{
    private readonly ISettingsStore _store;
    private readonly IStartupService _startup;
    private readonly IHotkeySettingsService _hotkeys;
    private readonly Func<AppSettings, SettingsApplyWarnings> _applyRuntime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _currentSettings;

    public SettingsCoordinator(
        AppSettings initialSettings,
        ISettingsStore store,
        IStartupService startup,
        IHotkeySettingsService hotkeys,
        Func<AppSettings, SettingsApplyWarnings> applyRuntime)
    {
        ArgumentNullException.ThrowIfNull(initialSettings);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(applyRuntime);
        _currentSettings = initialSettings;
        _store = store;
        _startup = startup;
        _hotkeys = hotkeys;
        _applyRuntime = applyRuntime;
    }

    public AppSettings CurrentSettings => Volatile.Read(ref _currentSettings);

    public async Task<ApplySettingsResult> ApplyAsync(
        AppSettings requested,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ApplyCoreAsync(requested, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ApplySettingsResult> TogglePauseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ApplyCoreAsync(CurrentSettings with { IsPaused = !CurrentSettings.IsPaused }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ApplySettingsResult> ApplyCoreAsync(AppSettings requested, CancellationToken cancellationToken)
    {
        var previous = CurrentSettings;
        var effective = requested;
        var warnings = SettingsApplyWarnings.None;
        var failure = SettingsApplyFailure.PlatformUpdateFailed;
        StartupState? originalStartup = null;
        StartupState? actualStartup = null;
        var startupChangeAttempted = false;
        // Desktop adapters and the runtime callback retain the caller's UI context.
        // Only the pre-commit section is transactional: a later UI failure must not undo durable settings.
        try
        {
            var alternativeAvailable = _hotkeys.TryRegister(requested.Hotkey);
            if (!alternativeAvailable)
            {
                if (!previous.UseWindowsV || requested.UseWindowsV)
                {
                    return ApplySettingsResult.Rejected(previous, SettingsApplyFailure.AlternativeHotkeyInUse);
                }

                effective = effective with
                {
                    Hotkey = previous.Hotkey
                };
                warnings |= SettingsApplyWarnings.AlternativeHotkeyUnavailable;
            }

            originalStartup = await _startup.GetAsync();
            actualStartup = originalStartup;
            if (originalStartup.Available && originalStartup.Enabled != requested.AutoStart)
            {
                startupChangeAttempted = true;
                actualStartup = await _startup.SetEnabledAsync(requested.AutoStart);
            }

            effective = effective with
            {
                AutoStart = actualStartup.Enabled
            };
            if (!actualStartup.Available)
            {
                warnings |= SettingsApplyWarnings.StartupUnavailable;
            }
            else if (actualStartup.Enabled != requested.AutoStart)
            {
                warnings |= SettingsApplyWarnings.StartupNotApplied;
            }

            failure = SettingsApplyFailure.PersistenceFailed;
            await _store.SaveAsync(effective, cancellationToken);
        }
        catch (Exception error)
        {
            var rollback = await RollbackAsync(previous, originalStartup, startupChangeAttempted);
            if (error is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (!IsOperationalFailure(error))
            {
                throw;
            }

            return ApplySettingsResult.Rejected(previous, failure, rollback);
        }

        Volatile.Write(ref _currentSettings, effective);
        if (!_hotkeys.SetWindowsV(effective.UseWindowsV))
        {
            warnings |= SettingsApplyWarnings.WindowsVUnavailable;
        }

        warnings |= _applyRuntime(effective);
        return ApplySettingsResult.Applied(effective, actualStartup!, warnings);
    }

    private async Task<SettingsApplyWarnings> RollbackAsync(
        AppSettings previous,
        StartupState? originalStartup,
        bool startupChangeAttempted)
    {
        var warnings = SettingsApplyWarnings.None;
        try
        {
            if (!_hotkeys.TryRegister(previous.Hotkey))
            {
                warnings |= SettingsApplyWarnings.RollbackIncomplete;
            }
        }
        catch (Exception)
        {
            warnings |= SettingsApplyWarnings.RollbackIncomplete;
        }

        if (startupChangeAttempted && originalStartup is not null)
        {
            try
            {
                var restored = await _startup.SetEnabledAsync(originalStartup.Enabled);
                if (!restored.Available || restored.Enabled != originalStartup.Enabled)
                {
                    warnings |= SettingsApplyWarnings.RollbackIncomplete;
                }
            }
            catch (Exception)
            {
                warnings |= SettingsApplyWarnings.RollbackIncomplete;
            }
        }

        return warnings;
    }

    private static bool IsOperationalFailure(
        Exception error) => error is IOException or UnauthorizedAccessException or COMException or JsonException or SecurityException;
}
