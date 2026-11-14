# DockForWindows · 开发预览 0.6.0

Windows 上的 macOS 风格程序坞，带液态玻璃控制中心和桌面小组件。C# / WPF / .NET 10，开发预览 0.6.0，最低支持 Windows 10 2004。

## 首次克隆后的准备

仓库不包含 SDK、NuGet 缓存、个人数据和第三方工具。在仓库根目录执行一次：

```powershell
# 把 global.json 指定的 SDK 装到 tools\dotnet，不改系统 PATH
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\dotnet-install.ps1 -Version 10.0.403 -InstallDir .\tools\dotnet
```

NuGet 包在第一次编译时下载到 `tools\.nuget`（需要联网）。

任务栏适配（隐藏任务栏应用图标、在网络图标旁给控制中心入口留位置）依赖便携版 Windhawk 和 Windows 11 Taskbar Styler 模组，放在 `tools\windhawk\`，不在仓库里。没有这个目录时程序照常运行，任务栏保持原样。

## 运行

- `Run-Dev.cmd`：编译 Release 后启动，配置目录用仓库里的 `data\`。
- `Open-Settings.cmd`：打开设置；程序已在运行时直接唤出设置窗口。
- 退出：右键托盘图标 →“退出”。

再次启动只会唤出已运行的实例。命令行参数：`--settings`、`--control-center`、`--data-directory <目录>`、`--shutdown`。

## 编译与测试

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test.ps1
```

`Test.ps1` 跑默认的逻辑检查，不改系统设置。其余 `--verify-*` 定向检查列在 `tests/DockForWindows.Tests/Program.cs` 里；GUI 检查会移动鼠标、打开置顶窗口，运行前先看对应的检查代码。改了 `Shaders/LiquidGlass.hlsl` 要重新生成 `.ps`，`Build.ps1` 会自动调用 `tools\Compile-Shaders.ps1`。

## 配置与数据

- 开发运行：仓库根目录的 `data\`（已被 `.gitignore` 排除，不会提交）。
- 安装版：`%LOCALAPPDATA%\DockForWindows`。
- `settings.json` 是配置，`dock.log` 是日志，超过 2 MB 换新文件。

## 会动到系统的地方

| 功能 | 做了什么 | 怎么关 |
| --- | --- | --- |
| 接管托盘图标 | 点任务栏右侧的网络、音量、电池图标时打开程序坞的控制中心 | 按住 Ctrl 点击仍打开系统面板；设置里关闭“接管右侧快捷设置入口” |
| 全局快捷键 | 默认 Ctrl+Win+A 打开控制中心 | 在控制中心设置里更换 |
| 任务栏适配 | 找到 `tools\windhawk` 时启动便携版 Windhawk 修改资源管理器的任务栏；程序退出或崩溃后由守护脚本让它退出，任务栏恢复 | 删除或改名 `tools\windhawk` |
| 开机自启 | 只有在设置里打开并保存时才写 `HKCU\...\Run` | 在设置里关闭 |
| Wi-Fi 列表 | Windows 11 24H2 起列出附近网络需要开启位置服务；未开启时不去读取，界面提示并给出设置入口 | — |
| 联网 | 只有天气小组件访问 Open-Meteo，发送的是所选城市名或坐标 | 不添加天气组件 |

## 主要功能

- **程序坞**：固定项在左，运行中和最近使用的应用在分隔线右侧，回收站在最右。拖入 exe、快捷方式、文件或文件夹即可固定；快捷方式复制到 `data\shortcuts`，原快捷方式删掉也能启动。点击启动或切换窗口，再点收起；多窗口应用悬停显示窗口预览，滚轮切换。前台全屏时自动退下。
- **液态玻璃**：用 Windows Graphics Capture 取程序坞后方窗口的画面，在 GPU 上做边缘折射、高斯磨砂、高光和衬色。后方画面不变时复用上一帧，不重复上传。
- **控制中心**：Wi-Fi、蓝牙、音量、亮度、媒体、深色模式、节能、投影等模块，可拖动排列、调整尺寸；系统操作以 Windows 实际返回的状态为准，做不到的直接打开系统设置。
- **桌面小组件**：天气、日历、时钟等，可放到桌面。

## 打包

`packaging\Build-Installer.ps1` 生成自带运行时的单文件 exe 和 Inno Setup 安装包（需要 `tools\installer\inno`）。给内测用户发安装包，不要发整个开发目录。

## 文档

- `AGENTS.md`：开发规范，人和 AI 编码助手都按它来。
- `HANDOFF-2026-11-11.md`、`PROJECT_HANDOFF.md`：原作者的交接记录，含历史实现和验证细节；里面的 `D:\DockForWindows` 等路径是原作者机器上的。
- `docs/`：各轮设计、性能和验证记录，按日期命名，是历史资料。
