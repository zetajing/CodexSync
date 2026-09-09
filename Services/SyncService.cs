using System.Text.Json;
using CodexSync.Models;

namespace CodexSync.Services;

public sealed class SyncService
{
    private readonly CodexStateService _codexStateService;
    private readonly SnapshotService _snapshotService;

    public SyncService(CodexStateService codexStateService, SnapshotService snapshotService)
    {
        _codexStateService = codexStateService;
        _snapshotService = snapshotService;
    }

    public async Task<SyncManifest> PushAsync(
        WebDavService webDav,
        string codexHome,
        string remoteRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCodexStopped();
        progress?.Report("正在创建本机快照...");

        var snapshot = await _snapshotService.CreateSnapshotAsync(codexHome, cancellationToken);
        var fileName = $"{DateTimeOffset.Now:yyyyMMdd_HHmmss}_{Sanitize(Environment.MachineName)}.zip";
        var snapshotRemote = Combine(remoteRoot, "snapshots", fileName);

        progress?.Report("正在上传快照到 NAS...");
        await webDav.UploadFileAsync(snapshotRemote, snapshot, cancellationToken);

        var manifest = new SyncManifest
        {
            DeviceName = Environment.MachineName,
            CreatedAt = DateTimeOffset.Now,
            SnapshotFile = snapshotRemote,
            SessionCount = _codexStateService.CountSessions(codexHome)
        };

        progress?.Report("正在更新远端清单...");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await webDav.UploadTextAsync(Combine(remoteRoot, "manifest.json"), json, cancellationToken);
        progress?.Report("上传完成。");
        return manifest;
    }

    public async Task<SyncManifest> PullAsync(
        WebDavService webDav,
        string codexHome,
        string remoteRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCodexStopped();
        progress?.Report("正在读取 NAS 清单...");

        var json = await webDav.DownloadTextAsync(Combine(remoteRoot, "manifest.json"), cancellationToken);
        var manifest = JsonSerializer.Deserialize<SyncManifest>(json)
            ?? throw new InvalidDataException("NAS manifest.json 无法解析。");

        if (string.IsNullOrWhiteSpace(manifest.SnapshotFile))
        {
            throw new InvalidDataException("NAS manifest.json 没有快照路径。");
        }

        progress?.Report("正在备份当前本机 Codex 记录...");
        await _snapshotService.BackupCurrentAsync(codexHome, cancellationToken);

        var tempDir = Path.Combine(Path.GetTempPath(), "CodexSync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "remote-state.zip");

        progress?.Report($"正在下载 {manifest.DeviceName} 的快照...");
        await webDav.DownloadFileAsync(manifest.SnapshotFile, zipPath, cancellationToken);

        progress?.Report("正在恢复 Codex 会话记录...");
        await _snapshotService.RestoreSnapshotAsync(zipPath, codexHome, cancellationToken);
        progress?.Report("拉取完成。");
        return manifest;
    }

    private void EnsureCodexStopped()
    {
        if (_codexStateService.IsCodexRunning())
        {
            throw new InvalidOperationException("检测到 Codex 正在运行。请先完全关闭 Codex，再执行同步。 ");
        }
    }

    private static string Combine(params string[] parts) =>
        string.Join('/', parts.Select(p => p.Replace('\\', '/').Trim('/')).Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }
}
