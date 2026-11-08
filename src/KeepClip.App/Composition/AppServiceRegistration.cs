using System.Text.Json;
using KeepClip.Application;
using KeepClip.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace KeepClip.App.Composition;

internal static class AppServiceRegistration
{
    public static async Task<ServiceProvider> CreateAsync(string dataPath, AppCallbacks callbacks)
    {
        var store = new SettingsStore(dataPath);
        var settings = AppSettings.Default;
        string? errorMessage = null;
        try
        {
            settings = await store.LoadAsync();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            errorMessage = "Impostazioni non leggibili. Sono stati usati i valori predefiniti.";
        }

        var services = new ServiceCollection();
        services.AddSingleton(new StartupSettings(settings, errorMessage));
        services.AddKeepClipApplication(dataPath, settings, store, callbacks);
        services.AddKeepClipDesktop(callbacks);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
