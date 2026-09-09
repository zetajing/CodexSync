using CodexSync.Services;
using Microsoft.Extensions.Configuration;
using System.Windows;

namespace CodexSync;

public partial class MainWindow : Window
{
    private const string DefaultRemoteRoot = "CodexSync";
    private readonly CodexStateService _codexStateService = new();
    private readonly SnapshotService _snapshotService = new();
    private readonly MyConfig _config;

    public MainWindow()
    {
        InitializeComponent();

        _config = LoadConfig();
        RefreshLocalStatus();
        AppendLog($"已加载配置。本机目录：{GetCodexHome(_config.Localaddress)}；NAS 目录：{GetRemoteRoot(_config.Remoteaddress)}。");
        AppendLog("程序已启动。首次双机都有历史记录时，请先不要互相覆盖；下一步将加入首次合并功能。");
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            AppendLog("正在测试 WebDAV 连接...");
            using var webDav = CreateWebDav();
            await webDav.TestConnectionAsync();
            AppendLog("WebDAV 连接成功。");
        });
    }

    private async void PushButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "上传会把本机当前 Codex 会话作为 NAS 最新版本。\n\n如果另一台电脑还有尚未合并的独立历史，请先取消。",
            "确认上传",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var webDav = CreateWebDav();
            await webDav.TestConnectionAsync();
            var sync = new SyncService(_codexStateService, _snapshotService);
            var progress = new Progress<string>(AppendLog);
            var manifest = await sync.PushAsync(
                webDav,
                GetCodexHome(_config.Localaddress),
                GetRemoteRoot(_config.Remoteaddress),
                progress);

            AppendLog($"NAS 最新版本：{manifest.DeviceName} / {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss} / {manifest.SessionCount} 个 session");
            RefreshLocalStatus();
        });
    }

    private async void PullButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "拉取会先备份本机，然后替换本机的会话同步数据。auth.json 和 config.toml 不会被修改。\n\n如果两台电脑目前各自都有独立历史，请先取消，等待使用“首次合并”功能。",
            "确认拉取",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var webDav = CreateWebDav();
            await webDav.TestConnectionAsync();
            var sync = new SyncService(_codexStateService, _snapshotService);
            var progress = new Progress<string>(AppendLog);
            var manifest = await sync.PullAsync(
                webDav,
                GetCodexHome(_config.Localaddress),
                GetRemoteRoot(_config.Remoteaddress),
                progress);

            AppendLog($"已恢复 NAS 版本：{manifest.DeviceName} / {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            RefreshLocalStatus();
        });
    }

    private WebDavService CreateWebDav() => new(_config.Url, _config.Username, _config.Password);

    private static MyConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "找不到 config.json。请复制 config.example.json 为 config.json，并填写本机配置。",
                configPath);
        }

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("config.json", optional: false, reloadOnChange: false)
            .Build()
            .Get<MyConfig>()
            ?? throw new InvalidOperationException("config.json 配置为空或格式不正确。");
    }

    private static string GetCodexHome(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CodexStateService.DefaultCodexHome;
        }

        return Environment.ExpandEnvironmentVariables(value.Trim());
    }

    private static string GetRemoteRoot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultRemoteRoot;
        }

        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        return string.IsNullOrWhiteSpace(normalized) ? DefaultRemoteRoot : normalized;
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog($"错误：{ex.Message}");
            MessageBox.Show(ex.Message, "CodexSync", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        TestButton.IsEnabled = !busy;
        PushButton.IsEnabled = !busy;
        PullButton.IsEnabled = !busy;
    }

    private void RefreshLocalStatus()
    {
        var home = GetCodexHome(_config.Localaddress);
        var exists = Directory.Exists(home);
        var count = exists ? _codexStateService.CountSessions(home) : 0;
        LocalStatusTextBlock.Text = $"设备：{Environment.MachineName} · Codex目录：{(exists ? "已找到" : "未找到")} · Sessions：{count}";
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            var followTail = LogTextBox.VerticalOffset + LogTextBox.ViewportHeight
                >= LogTextBox.ExtentHeight - 2;

            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            if (followTail)
            {
                LogTextBox.UpdateLayout();
                LogTextBox.CaretIndex = LogTextBox.Text.Length;
                LogTextBox.ScrollToEnd();
            }
        });
    }
}
