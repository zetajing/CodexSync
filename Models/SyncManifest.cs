using System.Text.Json.Serialization;

namespace CodexSync.Models;

public sealed class SyncManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string DeviceName { get; init; } = Environment.MachineName;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public string SnapshotFile { get; init; } = string.Empty;
    public int SessionCount { get; init; }
}
