using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.Tests;

internal sealed class SettingsStoreStub(List<string> effects) : ISettingsStore
{
    public AppSettings? Saved { get; private set; }
    public Exception? Failure { get; set; }
    public Func<Task>? BeforeSave { get; set; }
    public int Saves { get; private set; }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved ?? AppSettings.Default);
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (BeforeSave is not null)
        {
            await BeforeSave();
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            throw Failure;
        }

        Saved = settings;
        Saves++;
        effects.Add("save");
    }
}
