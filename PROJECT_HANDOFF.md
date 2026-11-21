# DockForWin 维护交接

当前发布版本：1.0.0。实际源码根目录为本仓库；不要依赖某台机器的固定盘符。

## 构建入口

- C# / WPF / .NET 10；SDK 版本见 global.json。
- tools/Build.ps1：编译着色器并构建应用。
- tools/Test.ps1：默认逻辑检查。定向检查参数见 tests/DockForWindows.Tests/Program.cs。
- packaging/Build-Installer.ps1：自带运行时的单文件应用与高压缩 Inno Setup 安装器。
- README.md 和 docs/USAGE.md：安装、运行、数据与功能边界。

## 运行与数据

安装版使用当前用户的 LOCALAPPDATA/DockForWindows。开发脚本显式使用仓库 data/。不自动导入其他目录，不在发行包或 Git 中放入个人配置。首次启动由 SettingsService.CreateDefaults 生成通用系统入口，不预置桌面小组件。

运行目录、源码目录和数据目录分别核对；编译成功不代表正在运行的进程已更新。重启前保留未保存草稿。

## 核心实现

- Models/DockModels.cs、WidgetOptions.cs、WidgetSystem.cs：配置、Copy/Normalize 和布局数据。
- Views/DockSurface.cs、LiquidGlassEffect.cs、ControlCenterTokens.cs、ControlMotion.cs：共享材质、轮廓和运动。
- Views/WidgetHost.cs、WidgetDesktopService.cs：宿主、桌面附着、DPI 和拖动布局。
- Models/FigmaWidgetCatalog.cs、Assets/FigmaWidgets、Views/FigmaWidgetView*.cs：40 款原始设计资源和原生布局。
- Services/WidgetRuntime*.cs、MediaControlService.cs、WidgetWeatherService*.cs：真实数据与命令。
- Views/WidgetContent.Renderer.cs 的 WidgetClock：每秒重读 Windows 时间并重置平滑动画，隐藏或卸载时停止计时。
- Views/WidgetContent.DocumentClocks.cs 的 DocumentClockDial：运行中也接受时间校正。

18 款退役组件不再注册；旧配置通过已有 Normalize 过滤对应实例。RetiredWidgetFixtures 只用于测试共享旧渲染/动画代码，不对应用注册，也不写进用户配置。

## 验证边界

定向检查 --verify-release-profile-clocks 验证干净默认配置、现有配置保留、各类时钟和前后跳变时间校正，不修改 Windows 系统时间。

保留真实状态、错误提示、材质质量及快速动画反向。系统闹钟没有通用读取接口；媒体跳转和硬件控制仅在提供方支持时启用。测试替身、编译、屏幕截图、真实系统 API 与 GPU 呈现应分别报告。
