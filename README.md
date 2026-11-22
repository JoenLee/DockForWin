# DockForWin 1.0.0

Windows 原生程序坞、液态玻璃控制中心和桌面小组件。使用 C# / WPF / .NET 10。

[下载安装包](https://github.com/JoenLee/DockForWin/releases/tag/v1.0.0) · [详细使用说明](docs/USAGE.md) · [更新记录](CHANGELOG.md)

## 安装与首次运行

1. 从 **Releases → v1.0.0** 下载 `DockForWin-Setup-1.0.0-x64.exe`。
2. 双击安装。新安装默认位置为 `%LOCALAPPDATA%\Programs\DockForWin`，不需要管理员权限；升级沿用已有安装目录。
3. 从开始菜单打开 **DockForWin**。左键点击托盘图标进入设置，右键可退出。

安装包已包含 .NET 10 桌面运行环境，无需另装 .NET。面向 x64 Windows，主要验证环境为 Windows 11；声明的 API 下限为 Windows 10 2004，但 Windows 10 的完整兼容性尚未验证。

**新用户首次启动使用干净的默认配置：**

- 桌面没有预先放置的小组件。
- Dock 只固定文件资源管理器、可用时的记事本、下载和回收站；入口在使用者电脑上动态解析。
- 不包含作者的软件固定项、账号、播放记录、城市坐标或显示器路径，也不会自动读取开发目录。
- 已有用户配置会继续保留；升级不会重置你的固定项与桌面布局。

安装器采用 LZMA2 高压缩，保留完整 WPF / COM 运行所需功能。为了兼容 WPF，未采用可能破坏资源与反射调用的裁剪。

## 怎么使用

| 操作 | 使用方式 |
| --- | --- |
| 固定应用、文件或文件夹 | 拖入 Dock；也可通过设置管理固定项 |
| 启动 / 切换 / 收起窗口 | 点击 Dock 图标；多窗口应用可悬停查看预览 |
| 打开设置 | 左键点击托盘图标，或右键 Dock → 设置 |
| 打开控制中心 | 默认 **Ctrl+Win+A**；也可使用托盘或任务栏入口 |
| 放置桌面小组件 | 设置 → 小组件，选择类型、样式和尺寸，点击“添加到桌面” |
| 移动 / 删除小组件 | 拖动非交互区域；右键组件管理或删除 |
| 调整小组件外观 | 在组件设置中修改配色、字号、材质和圆角 |
| 时钟与世界时间 | 读取 Windows 系统时间；普通时钟默认本机时区，城市 / 世界时钟按所选时区换算 |
| 音乐播放器 | 在支持 Windows 系统媒体接口的播放器中播放音乐，再使用组件的播放、暂停、切歌等按钮 |
| 天气 | 选择城市，或授权使用位置；天气、日出与日落来自 Open-Meteo |

指针表盘会持续按系统时间校准；关闭动画时仍更新时间。小组件库保留 40 款 Figma 设计样式，其中包含 18 款天气、12 款时钟、5 款 iPod、1 款唱机和 4 款 iOS 播放器。已按作者选择移除 18 款旧组件。

完整操作、数据备份和能力限制见 [使用说明](docs/USAGE.md)。

## 配置与隐私

默认用户数据：`%LOCALAPPDATA%\DockForWindows`。

- `settings.json`：偏好、固定项、小组件与布局。
- `widget-media`：本机播放封面缓存；`widget-account`：可选的 Microsoft 数据连接。
- `dock.log`：诊断日志。

卸载保留用户数据。发行包和 Git 源码不包含 `data/`。开发启动使用仓库内 `data/`，和普通安装默认数据目录分别管理。

天气查询会访问 Open-Meteo；Microsoft To Do / OneNote 仅在你主动连接账号并配置来源后使用。系统媒体信息取自 Windows 当前媒体会话，应用收藏与最近播放记录属于本应用，不会伪造播放器的完整队列或音质。

## 从源码构建

需要 Windows、`global.json` 指定的 .NET SDK，以及联网恢复 NuGet 依赖。SDK 和缓存不随源码仓库发布。

```powershell
# 在仓库根目录安装本地 SDK，不修改系统 PATH
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\dotnet-install.ps1 -Version 10.0.403 -InstallDir .\tools\dotnet

# 构建、默认逻辑检查
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test.ps1

# 验证干净默认配置和全部时钟；使用隔离数据，不改 Windows 系统时间
.\tools\dotnet\dotnet.exe run --project .\tests\DockForWindows.Tests\DockForWindows.Tests.csproj -- --verify-release-profile-clocks .\tests\artifacts\release-clocks
```

`Run-Dev.cmd` 构建并使用仓库 `data/` 启动。`Open-Settings.cmd` 唤出设置。GUI 定向检查可能打开置顶窗口，执行前请阅读对应检查代码。

生成安装包需要 **Inno Setup**：将 `ISCC.exe` 加到 PATH，或将工具放在 `tools\installer\inno\`，然后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\Build-Installer.ps1
```

输出到 `dist/`，包括安装 EXE、SHA-256 校验文件和临时 `payload/`。安装 EXE 完成验证后可以删除 `payload/`。构建脚本只收集发布程序和许可文件，不收集个人配置。

## 说明

- 亮度、无线网络、蓝牙及部分系统操作取决于硬件、Windows 版本与权限。不可用时显示实际反馈，或打开 Windows 原生设置。
- 音乐进度拖动、随机与循环取决于播放器向 Windows 暴露的能力；不支持时禁用。
- Windows 没有通用的系统闹钟读取接口；闹钟组件提供 Windows 时钟入口，不显示虚构的下一次闹钟。
- 可选的任务栏图标隐藏依赖用户自行配置的 Windhawk；本发行包不携带该工具，不会为此自动下载或接管它。
- 本首发安装包未做商业代码签名。

第三方字体与图标许可随安装包放在 `Licenses/`。架构说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)；维护规范见 [AGENTS.md](AGENTS.md)。
