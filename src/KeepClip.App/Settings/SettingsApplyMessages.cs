using KeepClip.Application.Settings;

namespace KeepClip.App.Settings;

internal static class SettingsApplyMessages
{
    public static string ForSave(ApplySettingsResult result)
    {
        var messages = new List<string>();
        if (result.Status == SettingsApplyStatus.Rejected)
        {
            messages.Add(result.Failure switch
            {
                SettingsApplyFailure.AlternativeHotkeyInUse => "Scorciatoia già in uso. La combinazione precedente resta attiva.",
                SettingsApplyFailure.PersistenceFailed => "Impossibile salvare le impostazioni.",
                _ => "Impossibile applicare le impostazioni di Windows.",
            });
        }
        else
        {
            messages.Add("Impostazioni salvate.");
        }

        AddWarning(SettingsApplyWarnings.SelectionCaptureUnavailable, "La copia alla selezione non è disponibile. Riprova dopo il riavvio.");
        AddWarning(SettingsApplyWarnings.WindowsVUnavailable, "Win+V non disponibile: usa la scorciatoia alternativa.");
        AddWarning(SettingsApplyWarnings.AlternativeHotkeyUnavailable, "Win+V restituito a Windows. Scorciatoia alternativa già in uso.");
        AddWarning(SettingsApplyWarnings.StorageUnavailable, "Cronologia non disponibile. Ripristinala prima di riprendere la cattura.");
        AddWarning(SettingsApplyWarnings.StartupUnavailable, "Impossibile verificare l'avvio automatico. Controlla le impostazioni di Windows.");
        AddWarning(SettingsApplyWarnings.StartupNotApplied, "Windows non ha applicato la scelta di avvio automatico.");
        AddWarning(SettingsApplyWarnings.RollbackIncomplete, "Ripristino delle impostazioni di Windows incompleto. Verifica l'avvio automatico e la scorciatoia.");
        return string.Join(Environment.NewLine, messages);
        void AddWarning(SettingsApplyWarnings warning, string message)
        {
            if (result.Warnings.HasFlag(warning))
            {
                messages.Add(message);
            }
        }
    }
}
