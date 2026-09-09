using System.IO.Compression;

namespace CodexSync.Services;

public sealed class SnapshotService
{
    private static readonly string[] SyncItems =
    [
        "sessions",
        "archived_sessions",
        "session_index.jsonl",
        "state_5.sqlite",
        "thread_history_1.sqlite",
        "history.jsonl",
        "memories",
        "skills"
    ];

    public async Task<string> CreateSnapshotAsync(string codexHome, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(codexHome))
        {
            throw new DirectoryNotFoundException($"Codex 目录不存在：{codexHome}");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "CodexSync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, "codex-state.zip");

        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            foreach (var item in SyncItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = Path.Combine(codexHome, item);

                if (File.Exists(source))
                {
                    archive.CreateEntryFromFile(source, item, CompressionLevel.Fastest);
                    continue;
                }

                if (!Directory.Exists(source))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(codexHome, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, relative, CompressionLevel.Fastest);
                }
            }
        }, cancellationToken);

        return zipPath;
    }

    public async Task<string> BackupCurrentAsync(string codexHome, CancellationToken cancellationToken = default)
    {
        var snapshot = await CreateSnapshotAsync(codexHome, cancellationToken);
        var backupRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexSync",
            "Backups");

        Directory.CreateDirectory(backupRoot);
        var backupFile = Path.Combine(backupRoot, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.MachineName}.zip");
        File.Copy(snapshot, backupFile, true);
        return backupFile;
    }

    public async Task RestoreSnapshotAsync(string zipPath, string codexHome, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("同步快照不存在。", zipPath);
        }

        await Task.Run(() =>
        {
            Directory.CreateDirectory(codexHome);

            foreach (var item in SyncItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.Combine(codexHome, item);
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
                else if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }
            }

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.GetFullPath(Path.Combine(codexHome, entry.FullName));
                var root = Path.GetFullPath(codexHome) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("快照包含非法路径。已停止恢复。 ");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
            }
        }, cancellationToken);
    }
}
