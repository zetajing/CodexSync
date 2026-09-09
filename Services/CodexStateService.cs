using System.Diagnostics;

namespace CodexSync.Services;

public sealed class CodexStateService
{
    public static string DefaultCodexHome => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

    public bool IsCodexRunning()
    {
        try
        {
            return Process.GetProcessesByName("codex").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public int CountSessions(string codexHome)
    {
        var sessionsPath = Path.Combine(codexHome, "sessions");
        if (!Directory.Exists(sessionsPath))
        {
            return 0;
        }

        return Directory.EnumerateFiles(sessionsPath, "*.jsonl", SearchOption.AllDirectories).Count();
    }
}
