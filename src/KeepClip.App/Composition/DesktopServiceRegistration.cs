using KeepClip.App.Clipboard;
using KeepClip.App.Desktop;
using KeepClip.App.Interop;
using KeepClip.App.Settings;
using KeepClip.App.Shelf;
using KeepClip.Application;
using KeepClip.Application.Paste;
using KeepClip.Application.Selection;
using KeepClip.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace KeepClip.App.Composition;

internal static class DesktopServiceRegistration
{
    public static IServiceCollection AddKeepClipDesktop(
        this IServiceCollection services,
        AppCallbacks callbacks)
    {
        services.AddSingleton<MessageWindow>();
        services.AddSingleton<ForegroundTracker>();
        services.AddSingleton<ClipboardSourceTracker>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IWindowTarget, WindowTarget>();
        services.AddSingleton<IPasteInput, PasteInput>();
        services.AddSingleton<Func<ClipboardSourceContext, Task<KeepClip.Domain.History.ClipSource?>>>(
            _ => context => SourceAppResolver.ResolveClipboardAsync(context.OwnerProcess, context.ForegroundProcess));
        // AsyncClipboardService owns its executor. Do not also register the executor for disposal.
        services.AddSingleton<IClipboardAdapter>(provider =>
        {
            var window = provider.GetRequiredService<MessageWindow>();
            var adapter = new ClipboardAdapter(provider.GetRequiredService<ClipboardSourceTracker>(), () => window.Handle);
            return new AsyncClipboardService(adapter, new StaClipboardExecutor(), NativeMethods.GetClipboardSequenceNumber);
        });
        services.AddSingleton<IHotkeySettingsService>(provider =>
        {
            var hotkey = new HotkeyService(provider.GetRequiredService<MessageWindow>());
            hotkey.Invoked += callbacks.OpenShelf;
            return hotkey;
        });
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ClipboardMonitor>();
        services.AddSingleton(provider => new SelectionCopyCoordinator(
            provider.GetRequiredService<IClipboardAdapter>(),
            provider.GetRequiredService<HistoryService>(),
            () => callbacks.ReadSettings().CopyOnSelection,
            NativeMethods.GetClipboardSequenceNumber,
            callbacks.Copied,
            callbacks.Notify));
        services.AddSingleton(provider => new SelectionCopyService(
            DispatcherQueue.GetForCurrentThread(),
            provider.GetRequiredService<HistoryService>(),
            provider.GetRequiredService<SelectionCopyCoordinator>(),
            callbacks.SelectionEnabled));
        services.AddSingleton<ShelfWindow>();
        services.AddTransient(provider => new SettingsWindow(provider.GetRequiredService<SettingsViewModel>()));
        return services;
    }
}
