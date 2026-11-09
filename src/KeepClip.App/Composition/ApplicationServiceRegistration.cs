using KeepClip.App.Settings;
using KeepClip.App.Shelf;
using KeepClip.Application;
using KeepClip.Application.Paste;
using KeepClip.Application.Settings;
using KeepClip.Domain.History;
using KeepClip.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace KeepClip.App.Composition;

internal static class ApplicationServiceRegistration
{
    public static IServiceCollection AddKeepClipApplication(
        this IServiceCollection services,
        string dataPath,
        AppSettings initialSettings,
        ISettingsStore settingsStore,
        AppCallbacks callbacks)
    {
        services.AddSingleton(settingsStore);
        services.AddSingleton<IHistoryRepository>(_ => new SqliteHistoryRepository(dataPath));
        services.AddSingleton(_ => new ImageStore(dataPath));
        services.AddSingleton(_ => new HistoryRecovery(dataPath));
        services.AddSingleton(provider => new HistoryService(
            provider.GetRequiredService<IHistoryRepository>(),
            provider.GetRequiredService<ImageStore>().SaveAsync,
            provider.GetRequiredService<Func<ClipboardSourceContext, Task<ClipSource?>>>()));
        services.AddSingleton(provider => new SettingsCoordinator(
            initialSettings,
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<IStartupService>(),
            provider.GetRequiredService<IHotkeySettingsService>(),
            callbacks.ApplyRuntime));
        services.AddSingleton<IPasteContentLoader, StoredClipContentLoader>();
        services.AddSingleton<PasteClipFactory>();
        services.AddSingleton<ShelfViewModel>();
        services.AddTransient(provider =>
        {
            var coordinator = provider.GetRequiredService<SettingsCoordinator>();
            return new SettingsViewModel(
                coordinator.CurrentSettings,
                dataPath,
                settings => coordinator.ApplyAsync(settings),
                provider.GetRequiredService<IStartupService>().GetAsync,
                callbacks.Recover);
        });
        return services;
    }
}
