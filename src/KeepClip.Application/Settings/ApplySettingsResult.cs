namespace KeepClip.Application.Settings;

public sealed record ApplySettingsResult
{
    private ApplySettingsResult(
        SettingsApplyStatus status,
        AppSettings effectiveSettings,
        SettingsApplyFailure failure,
        SettingsApplyWarnings warnings,
        StartupState? startup)
    {
        Status = status;
        EffectiveSettings = effectiveSettings;
        Failure = failure;
        Warnings = warnings;
        Startup = startup;
    }

    public SettingsApplyStatus Status { get; }
    public AppSettings EffectiveSettings { get; }
    public SettingsApplyFailure Failure { get; }
    public SettingsApplyWarnings Warnings { get; }
    public StartupState? Startup { get; }

    public static ApplySettingsResult Applied(
        AppSettings settings,
        StartupState startup,
        SettingsApplyWarnings warnings = SettingsApplyWarnings.None) => new(
            warnings == SettingsApplyWarnings.None ? SettingsApplyStatus.Applied : SettingsApplyStatus.AppliedWithWarnings,
            settings,
            SettingsApplyFailure.None,
            warnings,
            startup);
    public static ApplySettingsResult Rejected(
        AppSettings settings,
        SettingsApplyFailure failure,
        SettingsApplyWarnings warnings = SettingsApplyWarnings.None) => failure == SettingsApplyFailure.None ? throw new ArgumentException("A rejected settings operation requires a failure reason.", nameof(failure)) : new(SettingsApplyStatus.Rejected, settings, failure, warnings, null);
}
