using System.Globalization;
using System.Text.Json;
using KeepClip.Application;
using KeepClip.Domain.History;
using Microsoft.Data.Sqlite;

namespace KeepClip.Storage;

public sealed class SqliteHistoryRepository(string directory, IImageCleanup? imageCleanup = null) : IHistoryRepository
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string DatabasePath => Path.Combine(directory, "history.db");

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        try
        {
            connection.Open();
            using var schema = connection.CreateCommand();
            schema.CommandText = "CREATE TABLE IF NOT EXISTS clips (id TEXT PRIMARY KEY, kind INTEGER NOT NULL, hash TEXT NOT NULL, copied TEXT NOT NULL, pinned INTEGER NOT NULL, text TEXT, image TEXT, size INTEGER NOT NULL)";
            schema.ExecuteNonQuery();
            var columns = new HashSet<string>();
            using (var info = connection.CreateCommand())
            {
                info.CommandText = "PRAGMA table_info(clips)";
                using var reader = info.ExecuteReader();
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
            }

            using var migration = connection.BeginTransaction();
            foreach (var column in new[]
            {
                ("source_name", "TEXT"),
                ("source_icon", "BLOB"),
                ("files", "TEXT")
            }

            )
            {
                if (!columns.Contains(column.Item1))
                {
                    using var alter = connection.CreateCommand();
                    alter.Transaction = migration;
                    alter.CommandText = $"ALTER TABLE clips ADD COLUMN {column.Item1} {column.Item2}";
                    alter.ExecuteNonQuery();
                }
            }

            migration.Commit();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public async Task<IReadOnlyList<ClipEntry>> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run<IReadOnlyList<ClipEntry>>(() =>
            {
                using var db = Open();
                var entries = new List<ClipEntry>();
                var missing = new List<string>();
                using (var command = db.CreateCommand())
                {
                    command.CommandText = "SELECT id,kind,hash,copied,pinned,text,image,size,source_name,source_icon,files FROM clips ORDER BY copied DESC";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ct.ThrowIfCancellationRequested();
                        var files = reader.IsDBNull(10) ? null : JsonSerializer.Deserialize<ClipFile[]>(reader.GetString(10));
                        var kind = (ClipKind)reader.GetInt32(1);
                        var path = reader.IsDBNull(6) ? null : reader.GetString(6);
                        if (kind == ClipKind.Image && (path is null || !IsOwnImage(path) || !File.Exists(path)))
                        {
                            missing.Add(reader.GetString(0));
                            continue;
                        }

                        entries.Add(new(
                            Guid.Parse(reader.GetString(0)),
                            kind,
                            reader.GetString(2),
                            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                            reader.GetBoolean(4),
                            reader.IsDBNull(5) ? null : reader.GetString(5),
                            path,
                            reader.GetInt64(7),
                            reader.IsDBNull(8) ? null : new ClipSource(reader.GetString(8), reader.IsDBNull(9) ? null : (byte[])reader.GetValue(9)),
                            files));
                    }
                }

                foreach (var id in missing)
                {
                    using var remove = db.CreateCommand();
                    remove.CommandText = "DELETE FROM clips WHERE id=$id";
                    remove.Parameters.AddWithValue("$id", id);
                    remove.ExecuteNonQuery();
                }

                CleanImages(entries);
                return entries;
            }, ct);
        }
        catch (SqliteException ex)
        {
            throw new HistoryStorageException(DatabasePath, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await Task.Run(() =>
            {
                using var db = Open();
                using var transaction = db.BeginTransaction();
                using (var clear = db.CreateCommand())
                {
                    clear.Transaction = transaction;
                    clear.CommandText = "DELETE FROM clips";
                    clear.ExecuteNonQuery();
                }

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    using var insert = db.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO clips (id,kind,hash,copied,pinned,text,image,size,source_name,source_icon,files) VALUES ($id,$kind,$hash,$copied,$pinned,$text,$image,$size,$source_name,$source_icon,$files)";
                    insert.Parameters.AddWithValue("$id", entry.Id.ToString());
                    insert.Parameters.AddWithValue("$kind", (int)entry.Kind);
                    insert.Parameters.AddWithValue("$hash", entry.Hash);
                    insert.Parameters.AddWithValue("$copied", entry.CopiedAt.ToUniversalTime().ToString("O"));
                    insert.Parameters.AddWithValue("$pinned", entry.IsPinned);
                    insert.Parameters.AddWithValue("$text", (object?)entry.Text ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$image", (object?)entry.ImagePath ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$size", entry.SizeBytes);
                    insert.Parameters.AddWithValue("$source_name", (object?)entry.Source?.Name ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$source_icon", (object?)entry.Source?.IconPng ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$files", entry.Files is null ? DBNull.Value : (object)JsonSerializer.Serialize(entry.Files));
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
                // Cleanup is best effort after commit: failure must not misreport the durable transaction.
                CleanImages(entries);
            }, ct);
        }
        catch (SqliteException error)
        {
            throw new HistoryStorageException(DatabasePath, error);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsOwnImage(
        string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(Path.Combine(directory, "images")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private void CleanImages(IReadOnlyList<ClipEntry> entries)
    {
        try
        {
            if (imageCleanup is not null)
            {
                imageCleanup.Clean(entries);
                return;
            }

            var folder = Path.Combine(directory, "images");
            if (!Directory.Exists(folder))
            {
                return;
            }

            var retained = entries.Where(x => x.ImagePath is not null).Select(x => Path.GetFullPath(x.ImagePath!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                if (!retained.Contains(Path.GetFullPath(path)))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
