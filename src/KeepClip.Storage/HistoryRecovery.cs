namespace KeepClip.Storage;

public sealed class HistoryRecovery(string directory)
{
    public async Task PrepareResetAsync()
    {
        await BackupAsync();
        foreach (var name in new[]
        {
            "history.db",
            "history.db-wal",
            "history.db-shm",
            "history.db-journal"
        }

        )
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public Task<string> BackupAsync() => Task.Run(() =>
    {
        var backup = Path.Combine(directory, "backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(backup);
        foreach (var name in new[]
        {
            "history.db",
            "history.db-wal",
            "history.db-shm",
            "history.db-journal"
        }

        )
        {
            var source = Path.Combine(directory, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(backup, name));
            }
        }

        var images = Path.Combine(directory, "images");
        if (Directory.Exists(images))
        {
            var imageBackup = Path.Combine(backup, "images");
            Directory.CreateDirectory(imageBackup);
            foreach (var file in Directory.EnumerateFiles(images))
            {
                File.Copy(file, Path.Combine(imageBackup, Path.GetFileName(file)));
            }
        }

        // No original file is changed until the caller has a complete backup.
        return backup;
    });
}
