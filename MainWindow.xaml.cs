using System.Windows;
using CodexSync.Services;

namespace CodexSync;

public partial class MainWindow : Window
{
    private readonly CodexStateService _codexStateService = new();
    private readonly SnapshotService _snapshotService = new();

    public MainWindow()
    {
        InitializeComponent();
        CodexHomeTextBox.Text = CodexStateService.DefaultCodexHome;
        RefreshLocalStatus();
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
                CodexHomeTextBox.Text.Trim(),
                RemoteRootTextBox.Text.Trim(),
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
                CodexHomeTextBox.Text.Trim(),
                RemoteRootTextBox.Text.Trim(),
                progress);

            AppendLog($"已恢复 NAS 版本：{manifest.DeviceName} / {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            RefreshLocalStatus();
        });
    }

    private WebDavService CreateWebDav() => new(
        WebDavUrlTextBox.Text.Trim(),
        UsernameTextBox.Text.Trim(),
        PasswordBox.Password);

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
        var home = CodexHomeTextBox.Text.Trim();
        var exists = Directory.Exists(home);
        var count = exists ? _codexStateService.CountSessions(home) : 0;
        LocalStatusTextBlock.Text = $"设备：{Environment.MachineName}\nCodex目录：{(exists ? "已找到" : "未找到")}\nSessions：{count}";
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }
}
