namespace KeepClip.Application.Settings;

public interface IStartupService
{
    Task<StartupState> GetAsync();
    Task<StartupState> SetEnabledAsync(bool enabled);
}
