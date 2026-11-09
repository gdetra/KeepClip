using KeepClip.Application;
using KeepClip.Application.Settings;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace KeepClip.App.Desktop;

internal sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeepClip";

    private static bool IsPackaged
    {
        get
        {
            try
            {
                _ = Package.Current.Id.Name;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<StartupState> GetAsync()
    {
        if (!IsPackaged)
        {
            try
            {
                var executable = Environment.ProcessPath ?? throw new IOException();
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return new(
                    StartupCommand.Matches(key?.GetValue(ValueName) as string, executable),
                    true,
                    "Avvio automatico per il tuo account Windows. Mantieni l'eseguibile in questa cartella.");
            }
            catch
            {
                return new(false, false, "Impossibile leggere l'avvio automatico per questo account Windows.");
            }
        }

        try
        {
            var task = await StartupTask.GetAsync("KeepClipStartup");
            return Result(task.State);
        }
        catch
        {
            return new(false, false, "Impossibile leggere lo stato di avvio del pacchetto.");
        }
    }

    public async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        if (!IsPackaged)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                if (enabled)
                {
                    key.SetValue(
                        ValueName,
                        StartupCommand.Build(Environment.ProcessPath ?? throw new IOException()),
                        RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }

                return await GetAsync();
            }
            catch
            {
                return new(false, false, "Impossibile modificare l'avvio automatico per questo account Windows.");
            }
        }

        try
        {
            var task = await StartupTask.GetAsync("KeepClipStartup");
            if (!enabled)
            {
                task.Disable();
                return Result(task.State);
            }

            return Result(await task.RequestEnableAsync());
        }
        catch
        {
            return new(false, false, "Impossibile modificare l'avvio automatico. Controlla le impostazioni di Windows.");
        }
    }

    private static StartupState Result(StartupTaskState state) => new(
        state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy,
        true,
        state is StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy ? "Avvio bloccato da Windows. Controlla Impostazioni → App → Avvio." : null);
}
