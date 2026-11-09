using System.Text.Json;
using KeepClip.Application;
using KeepClip.Application.Settings;

namespace KeepClip.Storage;

public sealed class SettingsStore(string directory) : ISettingsStore
{
    private string FilePath => Path.Combine(directory, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return AppSettings.Default;
        }

        var value = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(FilePath, cancellationToken));
        return value is not null && value.Hotkey is not null ? value : AppSettings.Default;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        await File.WriteAllTextAsync(
            temp,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temp, FilePath, true);
    }
}
