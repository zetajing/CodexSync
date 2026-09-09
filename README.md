# CodexSync

Windows 下用于多台电脑之间同步 Codex 本地会话记录的小工具，目标场景是：

- 两台电脑轮流使用 Codex，不同时运行
- 飞牛 fnOS / 群晖 / Nextcloud / 其它 WebDAV 作为中转
- Codex 始终读写本机 `%USERPROFILE%\.codex`
- 只有在 Codex 完全关闭时执行同步（冷同步）

## 当前 MVP

技术栈：.NET 8 + WPF，使用 `Microsoft.Extensions.Configuration` 读取本地配置。

## 本地配置

WebDAV 地址和登录信息保存在本地 `config.json`，该文件已加入 `.gitignore`，不会提交到 Git。首次使用时复制模板：

```powershell
Copy-Item .\config.example.json .\config.json
```

然后只在本机填写 WebDAV 地址、账号、密码、本地 `.codex` 路径和 NAS 目录。程序启动和每次同步时都会读取 `Localaddress` 和 `Remoteaddress`。不要把真实的 `config.json` 提交或分享；如果凭据曾经进入过 Git 历史，请立即更换密码。

配置示例：

```json
{
  "Url": "https://your-webdav-host.example/",
  "Username": "your-username",
  "Password": "your-password",
  "Remoteaddress": "CodexSync",
  "Localaddress": "%USERPROFILE%/.codex"
}
```

`Url` 是 WebDAV 服务地址，`Remoteaddress` 是 NAS 远程根目录，`Localaddress` 是本机 Codex 目录。路径支持 `%USERPROFILE%` 等环境变量。

已经实现：

- WebDAV 连接测试
- 扫描本机 `sessions` 数量
- 将会话状态打包为 ZIP 快照后上传 WebDAV
- 使用 `manifest.json` 指向 NAS 最新快照
- 拉取前自动备份本机当前状态
- 从 NAS 下载并恢复快照
- 同步前检测 `codex` 进程，运行中拒绝同步
- 默认不触碰 `auth.json` 和 `config.toml`

## 使用流程

1. 完全关闭 Codex。
2. 点击“测试 WebDAV”，确认连接正常。
3. 在拥有最新会话的一台电脑上点击“上传到 NAS”。
4. 在另一台电脑配置相同的 WebDAV 地址后，点击“从 NAS 拉取”。
5. 拉取前程序会自动备份本机当前同步数据。

普通上传和拉取是“单一最新版覆盖”模型，不是双机历史合并。如果两台电脑各自已经有独立历史记录，请不要直接互相覆盖。

当前同步白名单：

- `sessions/`
- `archived_sessions/`
- `session_index.jsonl`
- `state_5.sqlite`
- `thread_history_1.sqlite`
- `history.jsonl`
- `memories/`
- `skills/`

NAS 目录结构：

```text
CodexSync/
├─ manifest.json
└─ snapshots/
   ├─ 20260909_120000_PC-A.zip
   └─ 20260909_123000_PC-B.zip
```

## 非常重要：两台电脑都已有历史记录

当前版本的普通“上传 / 拉取”属于单一最新版模型。若 PC-A、PC-B 目前各自都有独立 session，先不要互相覆盖。

下一阶段会加入“首次合并”功能：

1. 两边先完整备份
2. 按 session/thread ID 合并
3. 相同内容去重
4. 一边是另一边完整延续时保留较新版本
5. 真正发生分叉的 session 交给用户选择
6. 合并后生成统一 Master，再进入日常上传/拉取模式

## 编译

Visual Studio 2022 打开 `CodexSync.sln`，或在 Windows 终端执行：

```powershell
dotnet build .\CodexSync.sln
```

运行：

```powershell
dotnet run --project .\CodexSync.csproj
```

## 项目结构

```text
CodexSync/
├─ MainWindow.xaml(.cs)       WPF 界面和操作入口
├─ Services/
│  ├─ WebDavService.cs        WebDAV 上传、下载和目录管理
│  ├─ SnapshotService.cs      快照、备份和恢复
│  ├─ SyncService.cs          上传/拉取流程
│  └─ CodexStateService.cs    Codex 进程和会话统计
├─ Models/SyncManifest.cs     远程快照清单模型
├─ config.example.json        无敏感信息的配置模板
└─ config.json                本机配置，不提交到 Git
```

本机备份默认保存到 `%LOCALAPPDATA%\CodexSync\Backups`。

## 当前限制

- 当前版本不支持首次双机历史合并。
- 拉取操作会覆盖同步白名单内的本机数据。
- 临时快照和历史备份目前没有自动清理策略。
- 快照暂未加入校验和或版本锁机制。

## 开发分支

当前 MVP 开发位于：

```text
feature/wpf-mvp
```
