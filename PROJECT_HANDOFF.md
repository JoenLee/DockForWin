# Windows Liquid Glass 控制中心项目交接

> 最新视觉修正：按用户图二改为小连续圆角，轮播内滚轮不再滚动页面，卡片边缘统一约22 DIP间距。71项定向检查通过；详见 docs/APP-WIDGET-CORNERS-2026-11-11.md。该说明优先于下文完整超椭圆及边界滚轮冒泡的旧设计。

> APP / 小组件原型后续更新（2026-11-11）：见 `docs/APP-WIDGET-PROTOTYPE-2026-11-11.md`。新版 APP 外壳、天气和日历两类已有源码与隔离预览；正式 PID 10604 未重启。实际 FPS、GPU 和长时资源验收仍待完成，暂不扩展其他类别。

> 最新接手入口（2026-11-11）：先读 `HANDOFF-2026-11-11.md`。下文早期正文为历史记录；控制中心阴影、会话目录内容与运行状态以最新指南/源码为准。

核对日期：2026-11-09。依据当前磁盘源码整理；本次仅创建文档，未修改业务代码、启动应用、重新构建或运行测试。

## 1. 新对话先读

- **实际工程根目录：`D:\DockForWindows`。** 下文相对路径均以此为基准。
- 当前 Codex 会话目录 `C:\Users\HENG\Documents\ChatGPT\DockForWindows` 只有空 Git 仓库及本次交接文档，不是源码副本。先确认工作目录，再修改工程；不要在此空仓库重新搭建应用。
- 两个目录各存放一份本文件及 `AGENTS.md`。以后以实际工程的源码和交接文档为准；文档副本不会自动同步。
- 当前项目版本字段为 `0.6.0`。D 盘根目录没有 `.git`，不能从当前空仓库的 Git 状态推断业务代码改动、部署版本或提交记录。
- **“已实现”表示源码存在调用链；“能力受限”表示仅在 API/硬件支持范围内工作；“待验证”表示本次没有运行确认。** 旧测试报告、二进制时间戳、历史聊天都不能代替当前运行验证。

建议阅读顺序：`Program.cs` → `Models/ControlCenterOptions.cs` → `Services/ControlCenterLayout.cs` → `Views/ControlCenterWindow*.cs` → 玻璃与系统服务。这些文件均位于 `src/DockForWindows/`。

## 2. 技术栈与依赖

| 层 | 当前实现 |
| --- | --- |
| 桌面框架 | C# / WPF，主要通过 C# 构建控件树；局部 MVVM（`DockViewModel`），控制中心为多个 partial 文件组成的 `ControlCenterWindow` |
| 平台目标 | `net10.0-windows10.0.26100.0`，`SupportedOSPlatformVersion=10.0.19041.0`；这是声明的 API 下限，不代表已完成 Windows 10 兼容验收 |
| SDK | 根目录 `global.json` 指定 .NET SDK `10.0.403`、`rollForward=latestPatch`；脚本使用 `tools/dotnet/dotnet.exe` |
| 系统交互 | Win32 P/Invoke、COM、Windows Runtime；Windows Forms 用于托盘、显示器和部分系统对话框 |
| 玻璃绘制 | WPF `ShaderEffect` / HLSL `ps_3_0`、Gaussian `BlurEffect`、独立阴影层；D3D11/DXGI + Windows Graphics Capture 采集窗口 |
| 动画 | WPF 合成动画 + `CompositionTarget.Rendering`；共享 `ControlMotion` 解析弹簧，位置/尺寸部分按真实 delta time 推进 |
| 显式 NuGet 依赖 | `System.Management` `10.0.0`（WMI）；`NuGet.Config` 仅配置 nuget.org v3 |
| 辅助工具 | `tools/ControlCenterDesigner/index.html` 为离线几何配置设计器，**不是桌面 UI 运行时**；打包使用 Inno Setup，任务栏隐藏使用随工程提供的 Windhawk 辅助方案 |

`app.manifest` 使用 `asInvoker`、PerMonitorV2、`longPathAware`；入口也设置 DPI awareness。开发构建 `UseAppHost=false`，通常通过 `dotnet.exe <DLL>` 启动。不要照搬 Web/Flexbox 代码重写现有 WPF 布局。

## 3. 目录与模块职责

```text
D:\DockForWindows\
  src/DockForWindows/       应用源码、Assets、Shaders、app.manifest
  tests/DockForWindows.Tests/  自定义可执行测试与 GUI/性能诊断
  tests/artifacts/          测试截图、JSON 等输出
  tools/                   本地 SDK、构建/运行/测试/Shader 脚本、设计器及辅助工具
  packaging/               安装器构建/验证脚本和 Inno Setup 配置
  data/                    开发运行配置、日志、托管快捷方式（用户数据）
  docs/                    历史架构/验证记录、第三方许可、旧版本备份
  global.json / NuGet.Config / Run-Dev.cmd / Open-Settings.cmd
```

### 核心依赖链

```text
Program
  SettingsService → DockSettings / ControlCenterOptions
  WindowTracker → DockViewModel → DockWindow → DockStrip / DockTile / DockSurface
  托盘、热键或任务栏入口 → ControlCenterWindow
    ControlCenterLayout + ControlCenterTokens + ControlCenterCatalog
    ConnectivityService / SystemControlService / MediaControlService / 其他系统服务
    GlassBackdropService → WindowFrameCapture → 共享背景帧
    DockSurface → LiquidGlassEffect / LiquidGlass.hlsl
    ControlMotion → 主界面 / Navigation / 编辑重排
```

| 文件/类 | 职责及主要入口 |
| --- | --- |
| `Program.cs` | STA 入口、单实例、启动参数、托盘、服务生命周期、退出保存与异常日志 |
| `Models/DockModels.cs`：`DockSettings` | Dock、动画、启动项和控制中心全局配置 |
| `Models/ControlCenterOptions.cs` | 模块枚举、布局/页面、尺寸、圆角、材质覆盖、`Normalize` / `Copy` / `Material` |
| `Models/ThemeAppearance.cs`、`AppearancePolicy.cs` | 材质与可用性规则；玻璃专属参数、全透明时的倒影条件 |
| `Services/SettingsService.cs`、`StartupRegistration.cs` | 配置读写/迁移、日志；当前用户 Run 注册表启动命令 |
| `Services/ControlCenterLayout.cs` | `Cells` / `Size` / `Arrange` / `Snap` / `MoveBefore` / `FitPanel`，四列网格、固定规格、换位、适屏 |
| `Views/ControlCenterWindow.cs` | 窗口构建/释放、`AnimateOpen`、系统事件订阅；同文件的 `ControlCenterCatalog` 提供模块名称/图标 |
| `ControlCenterWindow.Cards.cs` | 卡片绑定、热区、编辑、鼠标捕获、拖拽/尺寸吸附及 `LayoutCards` |
| `ControlCenterWindow.Interactions.cs` | `ControlPage`、`OperationPhase`、`Execute`、`BackDetails`、蓝牙设备详情、忙碌/错误状态 |
| `ControlCenterWindow.System.cs`、`SystemFeatures.cs` | `PollSystem`、`PrimaryAction`、`OpenModule`、`ShowDetails` / `HideDetails`、系统功能页面 |
| `ControlCenterWindow.Navigation.cs` | `StartNavigationGlass` / `StepNavigationGlass` / `FinishNavigationGlass`，共享玻璃轮廓和内容形变 |
| `ControlCenterWindow.Rendering.cs` | `CaptureLoop` / `CaptureFrame` / `Sample` / `ApplySample` / `OnRendering` / `MapGlassFrame`，采样、上传与屏幕坐标映射 |
| `ControlCenterWindow.Appearance.cs`、`Presentation.cs` | 材质继承、自适应前景/衬色、预览、空白点击、滑块反馈 |
| `ControlCenterWindow.Boundaries.cs`、`Scrollbars.cs`、`ScrollEdgeFade.cs` | 外扩安全区、左右/上下渐隐、滚动条、滚动交互边界 |
| `ControlCenterWindow.Management.cs` | 控件库、增删隐藏、名称/图标、页面、分组及配置保存 |
| `ControlCenterWindow.Media.cs`、`MediaCards.cs`、`MediaMarquee.cs` | 播放器四种规格/二级页、时间线、封面、长标题滚动 |
| `Views/ControlCenterTokens.cs`、`ControlCenterIcon.cs`、`ControlCenterToggle.cs` | 间距、内部轮廓、图标容器、媒体滑块、开关等通用规范 |
| `Views/ControlMotion.cs`、`ControlCenterPerformance.cs` | 统一弹簧及 `FramePerformance`（刷新率检测、呈现回调时间统计） |
| `Views/DockSurface.cs`、`LiquidGlassEffect.cs`、`GlassButtonChrome.cs` | Dock 和控制中心共用的玻璃、Shader 参数、小按钮玻璃 |
| `Views/ControlPlateShadow.cs`、`AdaptiveGlassAppearance.cs` | 独立阴影轮廓；背景亮度采样和衬色/前景对比度 |
| `Views/SettingsWindow.cs`、`SettingsWindow.ControlCenter.cs` | 三栏设置、参数可用性、草稿、实时预览、提交 |
| `Services/LaunchService.cs`、`IconService.cs`、`WindowTracker.cs` | 启动/快捷方式、图标提取、实际运行窗口跟踪 |

以上 `ControlCenterWindow.*` 相对路径前缀均为 `src/DockForWindows/Views/`。当前是枚举/目录表 + partial 窗口 + 系统服务的架构，已有 `IControlCenterConnectivity` 可注入测试替身；**尚非所有模块均实现独立统一组件接口**，不要在交接时称已完成全面组件化重构。

## 4. 功能现状与真实能力

以下均有源码实现，具体硬件/驱动/系统版本行为需本机验收。

| 功能 | 实现位置 / 系统接入 | 限制或待验证 |
| --- | --- | --- |
| Dock 固定、文件/文件夹拖入、运行/最近项目、回收站 | `DockViewModel`、`LaunchService`、`WindowTracker`、`DockWindow` | 不要求项目保留在桌面；固定项目与机器路径是用户数据 |
| Dock 波浪放大、点击弹跳、换位、自动适屏居中、边缘停靠/隐藏 | `DockStrip` / `DockTile` / `DockWindow` / `DockMath` | 多屏和 DPI、最大化/全屏识别仍需现场验证 |
| 图标统一外框、倒影、四套设置界面配色 | `IconPresentation`、`InterfaceStyles`、`IosStyles`、`AppearancePolicy` | 倒影仅允许完全透明、无底板条件；设置主题与 Dock 材质独立 |
| Wi-Fi | `ConnectivityService.Initialize` / `SetRadio`：WinRT Radio；`Networks` / `Connect` / `ConnectNew` / `Disconnect`：Native WLAN | 支持扫描、配置文件、个人网络密码；企业认证转原生流程；扫描可能需要定位授权 |
| 蓝牙 | `ConnectivityService`：Radio、DeviceWatcher、`PairBluetooth` / `ForgetBluetooth` | 能读适配器/设备状态、配对/取消配对；**已配对设备的连接/断开仍转 Windows 管理**，不以取消配对冒充断开 |
| 音量、静音、麦克风音量/静音 | `SystemControlService`：Core Audio；`PollSystem` 同步读回 | 设备切换、禁用或移除时的行为待硬件验证 |
| 输入/输出设备切换 | `AudioDeviceService.Outputs` / `Inputs` / `SetDefault` | PolicyConfig COM 设置默认设备，依赖未公开 ABI，Windows 兼容性敏感 |
| 亮度、电池 | `SystemControlService`：WMI 亮度、`GetSystemPowerStatus` | 亮度只在 WMI 支持的显示设备可用；未实现外接屏 DDC/CI |
| 刷新率 | `DisplayControlService.RefreshRates` / `TryRefreshRate`、`RateTrial` | 枚举当前分辨率/色深下支持的模式；试用 15 秒，未确认则恢复 |
| HDR | `DisplayControlService`：DisplayConfig 读写高级颜色状态 | 根据设备支持和策略判定；无能力时应禁用并说明 |
| 性能/节能 | `SystemControlService`：powrprof effective overlay | “省电”采用能效优先电源模式，**不是 Windows Battery Saver 开关** |
| 投影/多显示器 | `ProjectionService.ReadTopology` / `SetTopology` | 主屏/复制/扩展/副屏；无线发现仍调用 Windows 原生面板 |
| 保持唤醒 | `AwakeService`：PowerCreateRequest / PowerSetRequest / PowerClearRequest | 15/30/60 分钟或至退出，可包含保持屏幕亮起；退出须释放 |
| 媒体 | `MediaControlService`：GlobalSystemMediaTransportControlsSessionManager | 实际标题/歌手/封面、上一首/暂停/下一首/Seek，能力由媒体提供方决定 |
| 专注 | `MediaControlService.ReadFocus`：FocusSessionManager；设置入口 | 可读可用专注状态；**尚无可靠直接开关 Windows 勿扰的完整实现** |
| 截图 | `ScreenshotService.Monitor`、窗口 WGC、`SystemToolService.OpenScreenshot` | 全屏/窗口截图及剪贴板；区域截图调用原生工具 |
| 录屏、语音输入 | `SystemToolService.OpenRecording` / `OpenDictation` | 版本检查 + 原生快捷键/系统工具；不是自研录屏器/转写器，不能据启动动作报告录制/识别已成功 |
| 系统深色 | `SystemControlService` 写 AppsUseLightTheme 并广播设置变化 | 不同时修改 SystemUsesLightTheme；不等同完整切换所有系统配色 |
| 控件管理 | `Management` / `Cards` / `ControlCenterLayout` | 添加、删除/隐藏、固定规格切换、排序、分组、页面、自定义名称/图标及持久化已有链路 |
| 设置/实时预览 | `SettingsWindow`、`ApplyPreview`、`DockViewModel` | 保存不关闭设置窗口；取消重新载入已提交配置；退出设置会停止预览 |
| 开机自启 | `StartupRegistration` + `DockWindow` 提交流程 | 当前用户 HKCU Run；命令包含数据目录；`BuildCommand` 仍有 260 字符保护，长路径可能被拒绝 |
| 热键/任务栏接管 | `ControlCenterEntryService`，默认 Ctrl+Win+A | RegisterHotKey、UIA 边界缓存/鼠标钩子；Explorer 版本/本地化会影响接管，Ctrl 点击保留原生入口 |
| 隐藏任务栏应用图标 | `TaskbarVisibilityService`、`tools/Watch-Taskbar.ps1`、Windhawk | 辅助工具缺失或另有 Windhawk 时拒绝接管；不能描述为纯 WPF 内置能力 |

### 当前点击与导航逻辑

- `Cards` / `Interactions` 实际把图标、标题、状态以及卡片空白交给 `PrimaryAction`；Wi-Fi/蓝牙等执行快捷操作，**右侧箭头**调用 `OpenModule`。部分旧注释与“名称区域展开”的早期需求不同，以绑定为准；若后续修改热区，需明确验收行为。
- `Execute` 维护 Idle/Busy/Error，防重复操作、捕获异常，结束后 `PollSystem`、同步开关、刷新详情。不能添加只改变本地布尔值的假成功逻辑。
- Wi-Fi/蓝牙进入详情会自动刷新一次，另有手动刷新按钮；设备事件仍会更新状态，不等于界面永久停止更新。
- `BackDetails` 从密码、蓝牙设备、刷新率确认返回所属模块，否则回一级。二级卡片外点击返回，一级空白点击退出；Esc 也参与返回。二级页已不使用 X/圆形关闭底板。

## 5. 几何与布局约束

- `ControlCenterLayout` 是四列占位网格，按页面/组、可见性自动填空。拖拽由 `MoveBefore` 重排；尺寸拖拽由 `Snap` 在规格间吸附，**不是自由改变长宽比**。
- 规格：圆按钮 1×1，胶囊 2×1，正方形 2×2，普通长条 4×1；播放器为 Strip 4×1、Square 2×2、Rectangle 4×2、Large **4×7**。大号按严格 4×7，不改成四个中号叠加的 4×8。
- `ControlCenterGeometry`：PanelWidth 300–480 DIP，PanelScale .65–1.5，ComponentScale .75–1.5，Gap 6–24。组件放缩、面板放缩与自动适屏是不同层次，不应混乘。
- Capsule/Media/Rectangle/SquareRoundness 单独设置，均 0–100；详情绘制通过 `DetailPhysicalRadius` 限制 DetailRoundness 至 **35%**。旧 LayoutVersion 迁移仍需验证保存后数值范围。
- 连续圆角使用 `DockSurface.CreateGeometry`、`CapsuleBorder`、`SquircleBorder` 和 `ControlCenterTokens`；Lamé 曲线应理解为对称的 `|x|³+|y|³=1`，不能把整张长卡拉成椭圆。胶囊直线/圆弧衔接有专用连续曲线分支。
- 封面与内部底板须遵循外轮廓的等距内缩；不能只靠随意 margin 或把半径写成外半径减 padding。封面保持正方形，长文本约束/省略，播放按钮固定占位，时间数字稳定，专辑封面悬停不抖动。
- `SideGutter=64 DIP` 扩展安全区；外溢在安全区渐隐。约 2cm 的短滚动条实现为最大 76 DIP，不是所有 DPI/屏幕下的真实物理厘米。

## 6. Liquid Glass 实现

### 背景采样与坐标

1. `GlassBackdropService.RenderCore` 在目标窗口背后按 Z 序组合桌面和应用窗口，不只读取壁纸。过滤不适合采集的工具/HUD 等窗口，采样区外扩约 `ceil(24*dpi)`，屏幕边缘补像素。
2. `WindowFrameCapture` 使用 D3D11/DXGI、WGC free-threaded frame pool；保留最新帧，窗口尺寸变化重建相关资源，闲置采集对象会清理。
3. `ReadWindow` 优先 WGC，短期缓存防止采集空帧退回壁纸，再尝试有超时约束的 PrintWindow。受保护内容、驱动或系统限制仍可能采不到。
4. 控制中心 `CaptureLoop` 串行异步采样，`CaptureFrame` 用 `Task.Run(Sample)` 避免阻塞 UI；仅保留 `_pendingSample` 最新结果，`OnRendering` 每个呈现回调最多上传一次。
5. 所有卡片复用 `_wholeFrame`（全分辨率 `WriteableBitmap`）；`MapGlassFrame`、`PointToScreen`、`BackdropScreenBounds`、`CapturePixelScale` 按屏幕坐标和实际变换取样，不能随卡片大小随意拉伸背景。

**当前并非 GPU 零拷贝：** D3D staging 读回、CPU 位图/字节数组及 WPF `WritePixels` 上传仍存在；这是高刷新率性能的重要成本。

### 材质与光学

- `DockSurface.ConfigureBackdrop` 的顺序是背景图像折射 Shader，再对该背景宿主作 Gaussian Blur，独立叠加表面衬色/高光。磨砂半径约 `24 * GlassFrost^1.6`，调磨砂不应放大背景或移除折射。
- `LiquidGlassEffect` 向嵌入的 `Shaders/LiquidGlass.ps` 传递参数；源文件为 `LiquidGlass.hlsl`。完整 Shader 路径依赖 WPF 渲染 Tier ≥ 2，低能力设备不能假定有相同效果。
- HLSL 分离外轮廓距离场/边缘照明与透镜折射。折射集中在边缘带，强度与 Distortion 控制位移，存在轻微 RGB 分离；中间尽量保持原背景采样比例。高光不是折射强度。
- 高光宽度/亮度、侧边暗线、淡黑衬色和透明度分别控制。`AdaptiveGlassAppearance` 根据局部背景亮度平滑调整衬色/文字；运行指示点也有对比度处理。
- `ConstrainFrostToOutline` 在控制中心用于模糊后限制外轮廓，避免涂层溢出；Dock 路径与控制中心存在差异，改共享代码必须兼查两处。
- `ControlPlateShadow` 把阴影放在独立兄弟层，通过外区减卡片区域的 EvenOdd 遮罩避免覆盖玻璃；不要把 DropShadow/Blur 直接加到整个玻璃或父容器，历史上会吞掉左边缘。
- `GlassButtonChrome` 复用相同 Shader；优先使用 `IGlassBackdropHost` 的帧，无宿主时才渲染祖先背景。按钮的黑/白/语义色仍独立保留。
- `ControlCenterOptions.Material(global)` 默认继承 Dock 材质，只有 `OverrideMaterial` 才应用独立参数；不要另建一套材质引擎。
- **占位参数：** 当前 HLSL 的 `Texture.x`（Grain）、`ClearBackground`、`WorldOrigin`、`Material.y` 没有参与最终输出。界面有 Grain 参数不代表已经实现可见的程序噪声磨砂。

## 7. 动画、编辑与媒体更新

### 统一运动参数

`ControlMotion`：Stiffness=230，DampingRatio=.90，Mass=1；ExitSpeed=1.12，EntranceSpeed=1.22，PageSpeed=1.30，PageDamping=.82，LayoutSpeed=1.65。

- `SpringPath.Value` / `Velocity` 为解析弹簧，Duration 按衰减包络自然收敛；`To` 对 WPF DoubleAnimation 设置物理 easing，并从上一段动画继承速度；`Step` 按高精度真实 dt 更新编辑布局。
- `FramePerformance.Detect` 查询窗口所在显示器刷新率，给 WPF Timeline 设置 DesiredFrameRate；采集循环按 30–360Hz 请求频率调度。**请求频率不等于实际完成/显示帧率，WPF/DWM/GPU 仍有实际限制。**
- `OnRendering` 初始 `1/60` 仅是首帧 dt 兜底，不是固定 60FPS 限制。Dock 自己的玻璃更新仍有独立 16ms DispatcherTimer，不能把控制中心优化结果直接套给 Dock。
- `To` 完成时先解除动画时钟，再提交基值，顺序不要颠倒；反向/快速重入须保留速度和最终状态。

### 弹出与共享元素形变

- `AnimateOpen` 等首个有效采样到达后仅触发一次；窗口此前透明。根层约 .94 缩放/Y24，卡片约 .90/Y18，按行错开 .025 秒、最大 .14 秒，保持逐层扫开结构。
- `NavigationLayer` 使用一块共享 `DockSurface` + 独立阴影；`StartNavigationGlass` 保存来源/目标矩形与圆角，`StepNavigationGlass` 同步插值位置、尺寸、曲率和内容比例，返回沿原来源逆向进行。
- `NavigationInk` 临时把**实际源卡内容和详情内容/标题**移入固定宿主，裁在共享轮廓内，按缩放/透明度交接；`Release` 放回原父级。当前不是从右侧塞入，也不是整页截图动画。快速返回、关闭和重建时须确保只释放一次、恢复原父级。
- 不要重新加入已放弃的内容截图/裁切纹理或额外动画时钟方案。宽高变化、部分轮廓更新仍需要布局，不能称完全不触发 Measure/Arrange。

### 拖拽、让位与滑动

- `Cards` 约 3 DIP 后进入拖动，捕获鼠标；`TrackDragPointer` 按呈现帧直接跟踪，拖动轻微放大（约 1.045）。其他卡片 `LayoutCards` 实时让位，弹簧位移带受限避让弧线。
- 调尺寸基于屏幕坐标/DPI/面板缩放换算，`Snap` 吸附固定规格；拖动期间避免反复重新定位整个窗口，结束才适屏和持久化。
- 滑块按压、拖动端点反馈、滚动阻尼/回弹已有实现；目前仍须核对降低动态效果设置是否覆盖所有直接 `ControlMotion.To` 调用。
- 阴影、安全区和滚动渐隐是局部边界处理；用户已撤回整个控制中心底层大块模糊，勿重新添加整块模糊底板。

### 媒体

- `MediaControlService` 订阅播放、元数据和时间线变化；`Read` 保存时间戳/播放速率，`PositionAt` 在有效媒体播放时外推位置。
- `Media.cs` 的 500ms 媒体读回是独立低频同步；`MediaCards.UpdateSmoothMediaTimeline` 按呈现帧更新进度，拖动期间不覆盖用户值，Seek 后读回校正。
- 提供方不给时间线/时长时，禁用拖动并显示未知时间，不能伪造 0:00/-0:00 或假进度。不同播放器能力需要分别验证。
- `MediaMarquee` 在固定宽度裁切区滚动标题；不应撑大 Grid 列。进度使用小圆形滑块；Strip/Square 不显示完整进度是规格设计，不等于丢失媒体状态。

## 8. 已知风险、未完成与待验证

| 优先级/状态 | 源码证据与影响 |
| --- | --- |
| 高：待确认的缓冲区竞争 | `GlassBackdropService.ReadPixels` 返回复用 `_pixelBuffer`；`SampleResult.Pixels` 直接引用，下一次后台采样可能先于 UI 的 `ApplySample` 改写数组。存在撕裂/闪烁风险，尚未复现归因；优先明确帧所有权/双缓冲，而非降低玻璃质量掩盖 |
| 高：高刷新率尚未验收 | 存在 CPU 合成/读回、整帧比较和上传、Blur/阴影以及形变尺寸布局开销。`FramePerformance` 测的是 CompositionTarget.Rendering **回调间隔**，不是 GPU 实际 present FPS；不能宣称已达到 180Hz，也不能沿用旧聊天数字作当前测量 |
| 中：可能保留过期窗口图像 | `ReadWindow` 最后的同位置缓存兜底未限制年龄；当新帧和 PrintWindow 持续失败时可能长期显示旧内容。需兼顾防闪与时效 |
| 中：WGC 初始化失败恢复 | `_captureUnavailable` 在初始化异常后阻止当前服务实例再次初始化；设备/驱动恢复路径待完善 |
| 中：动画生命周期 | `NavigationInk` 移动真实控件父级；快速反向、关闭、ApplyPreview、刷新详情与重建并发组合需检查，防双父级异常/旧引用/玻璃映射错位 |
| 中：能力未全量直控 | 蓝牙已配对设备连接、专注/DND、录屏和听写等仍有原生系统入口；硬件不支持时应明确提示。未建立所有模块统一独立组件接口 |
| 中：配置迁移/权限 | `Normalize` 有 LayoutVersion 迁移；旧角度转换与最终 35% 限制需核对。设置写入权限错误需要路径、ACL、占用等现场证据；本次不宣称旧 Access denied 问题已修复 |
| 中：视觉待验收 | 高/混合 DPI、长 SSID/歌曲名、播放器内外等距圆角、极端圆角、详情字号/留白、滚动条与分隔线距离、透明/深浅背景都需实际截图检查 |
| 低：参数与注释漂移 | Grain 等 Shader 参数未实际生效；部分热区注释不符绑定；减少动态效果对滑块拉伸等分支覆盖不足 |

这是一份源码审计风险清单，不是本次已复现的 Bug 列表。`README.md` 中“没有开机自启/没有打包”等部分描述已落后于代码；`docs/VERIFICATION-*` 仅是各自时点记录。尚未完成安装包的本次版本验收，也没有重新验证当前运行进程是否加载最新 DLL。

## 9. 配置、持久化和容易踩坑的细节

- 数据目录优先级：显式 `--data-directory` → `DOCKFORWINDOWS_DATA` → `%LOCALAPPDATA%\DockForWindows`。开发 `Run.ps1` 同时明确传入工程 `data`，因此通常读写 `D:\DockForWindows\data`。
- `Program` 在没有显式参数/环境目录时，会尝试 `SettingsService.TryImportFrom(@"D:\DockForWindows\data")`。**新安装可能导入这台机器的旧开发配置**；首次安装默认项目方案仍应单独设计，不要顺手修改用户数据。
- `SettingsService.Save`：Normalize → 写 `settings.json.tmp` → `File.Replace` 到主配置并保留 `settings.json.bak`；不存在时 Move。读取损坏配置会尝试备份 `.invalid-*`、给出 RecoveryNotice 并用默认值，**不会自动把 .bak 读作恢复配置**。
- 应用配置 SchemaVersion 当前为 1；控制中心 LayoutVersion 已迁移至 3。禁止跳过 Copy/Normalize、丢掉旧模块或直接覆盖用户机器路径。
- `DockWindow` / `DockViewModel` 负责草稿提交与保存失败回滚；设置保存不关窗。实时预览不等于已提交，正常关闭会结束预览。
- `PersistLayout` 在正常控制中心回调中进入 `DockViewModel.Save`，设置预览中的回调只更新草稿；改回调时不要让预览意外覆盖用户正式布局。
- 单实例使用 `Local\DockForWindows.Development.Instance`，命名事件包括 Show、Settings、Exit、ControlCenter。启动另一份相同应用会通知旧实例，**换数据目录也不会天然隔离 GUI 实例**。
- `--shutdown` 请求正常退出；不要直接结束进程来绕过未保存草稿。运行中 DLL 可能锁定 Debug 输出，构建 Release 成功不代表 Debug 进程已更新。
- 日志为当前数据目录 `dock.log`（约 2MB 滚动至 `.previous`）。UI 未处理异常会记录并退出，不能把“窗口消失”当操作成功。
- `StartupRegistration.BuildCommand` 包含进程、开发 DLL 和 profile 路径；长路径保护与 manifest 的 longPathAware 是不同概念。
- WPF DIP、屏幕物理像素、DPI 和 PanelScale/ComponentScale 必须分别换算；采样原点还包含外扩 padding，不能随意从任意父容器读 ActualWidth 替代屏幕坐标。
- 第三方来源/许可见 `docs/third-party/glassdesk/`、`android-liquid-glass/` 和 `docs/third-party/windhawk/NOTICE.md`，保留随附通知。
- 导入设计使用 `Services/ControlCenterDesign.Apply`，JSON schema v1/v2、大小限制 128KB，仅几何和模块配置；不要自动编译用户附件里的 C# 或假定可导入整套材质。

## 10. 启动、构建、测试和调试

以下命令从**实际工程目录**运行；本次没有执行这些命令。

```powershell
Set-Location 'D:\DockForWindows'
# 编译应用（脚本先处理 Shader）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Release
# 开发运行 / 打开设置；Run 会先构建 Debug
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Run.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Run.ps1 -Settings
# 自定义测试入口，按本次改动决定是否需要全量执行
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Test.ps1
```

根目录 `Run-Dev.cmd` / `Open-Settings.cmd` 对应开发入口。已构建 DLL 可传 `--settings`、`--control-center`、`--data-directory <path>`、`--shutdown`；先核对单实例与现有设置窗口状态。调试器附加实际 `dotnet.exe` 宿主或自包含进程，别附加到编译工具进程。

### Shader 与输出

- `tools/Compile-Shaders.ps1` 调系统 `d3dcompiler_47.dll`，入口 `main`、目标 `ps_3_0`，生成 `Shaders/LiquidGlass.ps`；按时间戳跳过。改 HLSL 后确认二进制同步，不能只看直接 `dotnet build` 成功。
- 应用输出固定在 `src/DockForWindows/bin/<Debug|Release>/net10.0-windows/`，不追加完整 TFM。直接构建 csproj 只嵌入现有 `.ps`，不会替代 Shader 编译脚本。
- 脚本设置 `DOTNET_CLI_HOME=tools/.cli`、`NUGET_PACKAGES=tools/.nuget`。离线缓存缺失时仍需 restore，不要把首次还原失败当代码错误。

### 定向诊断

`tests/DockForWindows.Tests/Program.cs` 是自定义可执行检查分发器，**不是 xUnit，也不能用一句 dotnet test 全绿替代**。例如：

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD 'tools\.cli'
$env:NUGET_PACKAGES = Join-Path $PWD 'tools\.nuget'
& .\tools\dotnet\dotnet.exe build .\tests\DockForWindows.Tests\DockForWindows.Tests.csproj -c Release
& .\tools\dotnet\dotnet.exe .\tests\DockForWindows.Tests\bin\Release\net10.0-windows\DockForWindows.Tests.dll --verify-control-sweep .\tests\artifacts\control-sweep
```

| 参数 | 用途 |
| --- | --- |
| `--verify-control-motion <file>`、`--verify-frame-pacing <file>` | 运动/帧时间诊断，记录回调均值、P95/P99、布局/分配等；非真实 GPU FPS |
| `--verify-control-center-interactions`、`--verify-control-services` | 功能交互和服务检查，检查代码中测试替身/硬件边界 |
| `--verify-glass-alignment <dir>`、`--verify-scaled-backdrop` | 玻璃映射、缩放对齐 |
| `--verify-control-design <dir>`、`--verify-control-center-scale <dir>` | 设计与组件/面板缩放 |
| `--verify-settings-preview <dir>` | 设置提交/预览相关 |
| `--verify-control-center-rendering` / `--verify-control-center-rendering-short`、`--capture-lifetime` | 背景捕获与生命周期 |
| `--probe-media` | 媒体会话/能力诊断，不以缺失提供方时间线伪造功能成功 |
| `--verify-reorder-animation` | **DockStrip** 的排序检查，不是控制中心拖拽完整验收 |

GUI 检查可能打开置顶测试窗口、操作鼠标、访问实际系统状态；先读相应 Checks，再运行必要的部分。有的测试（如 `ControlMotionChecks`）硬编码读取 `D:\DockForWindows\data`，迁移机器时不能以为设置了环境变量就完全隔离。Wi-Fi/蓝牙优先使用 `IControlCenterConnectivity` 测试替身，真实硬件操作另行记录结果。

性能对比应固定屏幕/刷新率/DPI/材质/布局/交互场景，先测 Wi-Fi 展开/返回，再推广。仅做文档不重跑 GUI；改动后做必要定向检查，一次通过后没有新变化不要反复跑旧测试。

### 打包（仅说明现有入口）

`packaging/Build-Installer.ps1`、`DockForWin.iss`、`Verify-Installer.ps1` 已存在。构建脚本默认输出 `D:\DockForWin`，发布 win-x64 自包含单文件、保留非裁剪，使用本地 Inno Setup，并会生成图标。脚本/旧安装包存在不代表当前安装包已验收；只有明确要求打包时才执行，不携带开发 `data/settings.json` 和用户应用清单。

## 11. 当前承接任务与下一步

**本次交付范围止于交接文档和长期规范。** 下面是应继续承接的开发方向，不表示本次已修改或正在后台运行。

1. 先确认启动路径、活动进程、实际加载 DLL 与用户数据目录。已有源码包含新导航、拖拽、媒体进度和标题滚动链路，避免从历史聊天重新实现一遍。
2. 优先解决背景帧所有权和采样/上传稳定性，定位闪烁、延迟与玻璃涂层偏移；保留当前质量与坐标映射，不用降分辨率、删材质或限屏幕刷新率掩盖问题。
3. 定向核对 Wi-Fi 原位展开/返回、逐行弹出、连续反向、尺寸吸附/避让的平滑度。先保留路径与共享元素结构，只优化帧开销与统一弹簧参数。
4. 核对媒体真实时间线、长标题滚动、四个固定规格以及内外等距轮廓；缺失媒体 API 信息时明确能力限制。验证二级页留白、圆角≤35%、无 X、来源返回和不重建导致抖动。
5. 以后再按需补齐平台能力和独立组件接口；保持现有 UI，不为“架构理想”重构共享引擎。首次安装默认项目/配置隔离方案参见 `docs/IDEA-21-deferred.md`，不要在性能任务里顺带改变用户配置。

长期约束见根目录 `AGENTS.md`。用户明确要求：保留现有 UI、玻璃效果、功能与动画结构；改动以性能、真实系统接入和交互稳定性为主。完成必要检查后直接更新实际工程，报告真实改动与未验证项，避免反复讲旧测试或保证未经测量的帧率。

## 12. 2026-11-09 本轮研发结果

本节优先于上文最初交接时的状态描述；完整数据见 `docs/PERFORMANCE-2026-11-09.md`。

- 已合入局部弹簧首尾 C2 修正、背景帧池化快照与明确所有权、材质资源和样式复用、相同 Shader 常量去重、形变关闭父级恢复，以及解析移动路径中的冗余计算清理。
- 原缓冲区竞争风险已修复。最高画质性能目标、200MB 挑战目标仍未完全实现，不能宣称接近 180Hz。
- HLSL 早退实际增加纹理读取；局部纹理、非阻塞上传、遮挡剔除及原生位图接管等实验未通过性能或严格画面验收，均未合入。HLSL/.ps 保持原版。
- Release 应用构建通过，定向曲线/帧所有权/四主题/Dock 设置/控制中心扫查/排序与关闭生命周期检查通过。最高画质连续运行 420 秒完成，采样 22,142 次；不等于多小时或真实硬件全面验收。
- WPF 回调统计不等于真实 GPU FPS。ETW/PresentMon 未提供可可靠归因的应用 GPU 帧时间与输入到显示时延，相关指标明确未测。
- 当前运行实例已正常切换为实际工程 Release，数据仍使用工程 data；没有打包、手工改显示或电源设置，也未通过强制 GC/工作集修剪制造低内存数字。
- 用户授权同类只读诊断和管理员性能采集直接继续，无需逐次再次询问；操作系统 UAC 安全桌面仍可能需要人工点击。
- 后续只对新改动或未解决问题运行相应必要检查；不要重复整套已经通过且源码未变的检查。
## 13. 2026-11-09 玻璃响应回归专项（优先于第 12 节）

- 用户取消 200MB 目标；700MB 为软目标，实时玻璃/动画/交互优先，禁止降级画质或复用过期背景换帧率。
- 已核对实际源码与上一轮已应用哈希。D 工程没有 .git；对照来自上一轮保留的原始源码，不能宣称完整 Git 历史归因。
- 最小修复：采样完成后在 UI continuation 立即上传，删除等待 OnRendering 的 pending 快照与关闭清理；保留独立帧所有权、Shader/画质、局部首尾曲线及全部布局和功能。
- 隔离最高画质对照：快照就绪到上传均值 4.93→0.44ms，P99 26.47→5.88ms。回调间隔 P95 89.57→86.82ms、P99 105.71→102.33ms；回调频率 29.37→31.54/s，不是真实 FPS。
- 最高画质实际屏幕背景变色检测：实际源码修复后静止均值 227.13ms、展开返回 255.46ms，仍有明显延迟；优化前快照也存在较长延迟，全部新增回归的归因尚未证明。
- 新 --verify-glass-response 检查静止背景、展开返回和窗口移动共12次屏幕变化通过。受影响生命周期检查已同步删除已移除字段的反射访问；快速反向、形变中关闭、父级恢复、帧释放和关闭停更通过。未修改材质与 Shader。
- Build.ps1 Release 成功。当前通过正常退出从 Debug PID9020 切至新 Release PID14200，同原 data 路径；切换前仅 Dock 主窗口，没有设置/控制中心草稿窗口。以后仍需现场核对 PID。
- 完整记录 docs/GLASS-REGRESSION-2026-11-09.md。真实呈现 FPS、应用 GPU 帧时/队列、滑块真实拖动同步、源 WGC 帧准确新鲜度及严格逐帧画质一致性仍未完成；未达到高刷新率验收，不进入后续内存大改。

## 14. 2026-11-09 WPF动画/重复采样与清理待确认

- 仅合入 WindowFrameCapture.Read / GlassBackdropService.ReadWindow 的精确同WGC帧、同窗口位置尺寸、同ROI读回去重；新帧与区域变化仍立即读取，无有效时间戳则原样读取。UI立即上传、Shader/玻璃画质、动画时长曲线、布局、Ionicons和点击逻辑均未改。
- 两轮反序最高画质对照：采样均值15.09→7.32ms；回调P95 94.85→90.69ms，P99 181.13→177.68ms（两轮各自统计值算术平均，非合并分位数）；单轮P99有升有降，不能宣称长尾已稳定解决。回调频率32.71→33.94次/s，不能冒充真实FPS。静态背景GPU复制3550/3565→40/40次；动态场景收益不保证相同比例。
- 实际Release构建、同帧/ROI/窗口移动/新帧检查、12次最高画质玻璃屏幕响应通过。隔离版本真实鼠标滑块拖动通过，测试阻止系统音量写入并恢复光标。真实FPS、GPU帧时间/DWM队列仍未可靠取得，700MB软目标未达到。
- 轮廓共享和Shader固定通道试验没有可靠帧时间收益，未合入；试验中发现WPF增量资源残留，失效结果已剔除。最终候选在全新animation-lab/P构建，嵌入原Shader哈希核对通过；实际工程HLSL/.ps未改。回退原件及哈希保存在会话animation-change目录。
- 新测试入口 --verify-capture-stamp / --verify-slider-animation <output.json>。不重复旧整套检查；仅对新变化运行必要验证。
- 当前PID5212仍为Debug，因用户设置窗口已打开，为保留草稿未退出/重启；新Release已构建，运行实例尚未加载本轮改动。下次操作须现场核对窗口和PID。
- docs/ANIMATION-PERFORMANCE-2026-11-09.md记录完整数据和限制。docs/CLEANUP-PROPOSAL-2026-11-09.md列出26个可重建缓存目录，共414.48MiB；尚未删除，必须等用户明确确认，确认后重新验证路径/数据/占用再清理并构建。旧ETW 2526.38MiB、正式二进制、所有源码快照和用户数据保留。

## 15. 2026-11-09 已确认的26个缓存目录清理完成

- 用户明确确认清理上一节清单中的26个可重建缓存目录；已按原绝对路径执行，删除前校验边界、祖先/内部重解析点、数据目录、意外源文件与占用。未扩大范围。
- 共删除492个文件，逻辑大小414.48MiB。随后tools/Build.ps1 -Configuration Release成功，0警告/0错误；重新生成8.75MiB中间文件，按目录文件长度净减少405.73MiB，不将该数字冒称磁盘簇精确释放量。
- 已接受源码哈希不变，新嵌入Shader与原.ps哈希一致；6个旧ETW文件大小不变，正式Debug/Release、测试Release、源码、数据、配置、SDK/NuGet依赖均保留。没有关闭程序或设置窗口，也没有重复旧功能测试。
- 实际工程src/DockForWindows/obj由构建重新生成；其他25个清单目录未重建。完整原路径与执行状态见docs/CLEANUP-PROPOSAL-2026-11-09.md，机器记录在会话animation-lab/cleanup-preflight.json和cleanup-result.json。
- 本次只执行已批准清理与构建验证，没有新的性能或画质验收结论；此前实际FPS/GPU帧耗时、P99和运行实例更新的限制仍有效。

## 16. 2026-11-09 APP设置双栏UI样板（待设计确认）

- 用户本轮只重设计APP/设置窗口，明确不动悬浮控制中心、玻璃Shader、背景捕获、系统功能与既有性能成果。已读用户Word中的开关/分段CSS参考及macOS设置截图，作为设计参考，未执行文档代码。
- 新增独立SettingsShellWindow双栏样板：固定220DIP侧栏、8分类、关键词搜索/空结果、移动高亮、返回前进、3个二级页、51×31开关及可变选项数分段选择器。页切换统一200ms/8DIP和270ms/20DIP，使用Opacity/RenderTransform；最多2活动页、11缓存页、64条历史。快速切换中修复透明度逐次衰减问题。
- 默认原设置窗口暂未替换，原全部设置与功能完整保留。原设置底部新增“新版界面预览”；模态预览用当前草稿深拷贝，检查后交回原页面保存。只附带修正SettingsWindow传入非空草稿时不再覆盖StartWithWindows字段；系统API/启动注册和提交代码未改。
- 新增局部SettingsShellStyles.xaml、SettingsSwitchMotion、SettingsSegmentedControl、SettingsPageHost、SettingsShellWindow及Pages partial、SettingsWindow.ShellPreview partial；新增专用测试和独立启动器。全局IosStyles、ControlMotion及34个受保护渲染/捕获相关文件哈希均不变。
- Release构建通过，22项新定向检查通过（含80次快速切页无空白/双页上限、键盘方向键、最小化/最大化/还原、滚动恢复、草稿与三个二级页）。本机160.42%DPI和980×740/780×570DIP截图已检查；未重复旧整套玻璃/性能测试。跨屏真实DPI、高对比、完整读屏和长期内存仍未全面验收。
- 双击Open-Settings-UI-Preview.cmd可独立打开样板（本地SDK/NuGet、默认测试草稿、不写正式data、不启动第二Dock）；“完成预览”仅写docs/settings-shell-preview/preview-draft.json。现场已打开预览PID46224，原应用PID9236为Debug、未退出/重启，PID后续需重新核对。
- docs/SETTINGS-SHELL-PROTOTYPE-2026-11-09.md记录页面盘点、实际接入范围、保存链路与限制。样板自身固定浅色，侧栏是低成本渐变层次，没有实时侧栏采样。待用户确认设计后再全面迁移/替换默认设置，不能将样板说成全部页面迁移完成。
- 回退原件/保护哈希/已应用哈希/截图与测试日志保存在会话settings-shell-change目录；不要作为垃圾自动删除，也不要覆盖用户后续修改。

## 17. 2026-11-10 全部设置迁移与图标/焦点调整

- 用户已确认双栏样板方向，正式 SettingsWindow 已接入同一外壳。原八分类、44滑块、3颜色编辑器、全部模块开关与功能回调保留；主功能组直接显示，其余组放入缓存二级页。原第三栏预览改为底部“预览”入口，按来源返回。保存/取消/实时预览仍使用原链路。
- 四配色与浅深外观均保留。主题或模块重建保留二级页、滚动、窗口尺寸与草稿；SettingsPageHost 修复重复路由历史的精确索引恢复。新 SettingsWindow.ShellLayout / SettingsShellWindow.Palette partial 管理设置专用布局和资源，没有改浮层渲染架构。
- 用户追加要求移除所有设置控件蓝色焦点框；已去掉局部模板焦点描边并禁用默认焦点装饰，键盘操作和选中高亮保留。侧栏八图标换为官方 Ionicons v8.0.0 Filled，原SVG、MIT许可和冻结WPF DrawingImage 位于 Assets/Ionicons，未改悬浮控制中心图标。
- 完整设置31项、实际主题重建8项、原控制中心预览7项、受影响外壳22项、原设置/Dock适配23项、去焦点框7项、图标9项均通过。Release构建通过。49个服务/Shader/受保护渲染文件哈希不变；没有重复旧整套玻璃性能测试，没有新的真实FPS/GPU帧时结论。
- 独立完整设置测试末尾瞬时Working Set约225.12MiB、Private Bytes约213.06MiB，非正式完整进程稳定内存验收；没有强制GC。真实跨屏DPI、高对比、完整读屏、长期内存和全部硬件操作仍未全面验收。
- 现场正式Debug PID9236仍有设置窗口，为保留草稿未关闭或重启；独立预览PID48008曾锁定测试输出，因此焦点/图标测试改用会话settings-full-change/focus-build独立输出。正常重开后才加载新源码。Open-Settings-UI-Preview.cmd 已改为完整设置隔离预览，开机启动开关在此预览禁用。
- 完整记录 docs/SETTINGS-FULL-MIGRATION-2026-11-09.md；原件、哈希、截图及必要测试日志在会话settings-full-change，不能作为垃圾自动删除。实际D工程没有.git，比较基于本轮修改前副本。
## 18. 2026-11-10 黑白窗口、右侧控制键与分隔线

- 用户本轮要求只保留黑白窗口主题。InterfaceStyles只解析Mono浅深配色；保留旧枚举用于配置读取，Normalize迁移到Mono，不改Dock材质。删除彩色配色选择，侧栏图标底色改参考图橙/蓝/绿/灰，Ionicons Filled不变。
- 用户已明确确认移除图1全部：快捷键字段、打开控制中心、导入网页设计参数和该组说明。两个入口开关及其余功能保留，后台快捷键/入口服务未改。
- 窗口控制移到右上，绿/黄/红，16DIP圆圈、矢量居中符号、120ms轻微悬停放大，无外围框；保留原生窗口动作。开关变为47×22黑轨、27×17白滑块、15DIP位移、250ms及CSS对应KeySpline。
- 新DockWindow.Separator partial在现有背景帧上传后局部采样，沿用小圆点平滑/滞回/颜色过渡，实现分隔线随亮暗背景变色，原几何和0.35透明度保留。没有增加采样频率或修改Shader、捕获服务及控制中心动画。
- Release构建通过；新增20项定向检查、受影响8项重建检查通过；49个服务/Shader/受保护浮层文件哈希不变。测试是WPF实机及合成背景，未新增真实FPS/GPU时延结论，不重复旧性能整套测试。
- 当前用户Debug PID27272仍有设置窗口，保留草稿未重启；Release已构建。报告docs/SETTINGS-REFINEMENT-2026-11-10.md；会话settings-refinement留有原件、截图、日志和哈希。真实动态壁纸、跨屏DPI和长期内存未全面验收。
## 19. 2026-11-10 开关内缩与单层配色框修正

- 用户指出上一版胶囊边缘挤压，且明确关闭为灰色、开启才为黑色。设置开关调整为54×24DIP轨道、32×18DIP滑块，四周最小3DIP内缩、16DIP位移；移除滑块重阴影和按压拉伸，保留250ms曲线及真实CheckBox状态。浅色关闭#D1D1D6、深色关闭#5B5B61、开启#080808。
- 控制中心配色设置保留外层圆角组框，去掉ComboBox内层描边。下拉模板改用TemplateBinding BorderThickness、默认仍为1，仅此行设为0；新增cc-color-mode定位ID。其余下拉外观不变。
- 新--verify-switch-shape六项通过（灰/黑状态、四边留白、垂直一致、干净轮廓、24次快速反向不越界、终点）；--verify-settings-color-row三项通过（单层边框、打开下拉、只改草稿）。Release构建0警告/0错误。没有重跑旧整套测试、没有写正式配置或关闭用户窗口。
- 修改文件：SettingsShellStyles.xaml、SettingsSwitchMotion.cs、SettingsShellWindow.Palette.cs、SettingsWindow.ControlCenter.cs。相应旧测试尺寸/位移断言同步更新，新增SwitchShapeChecks并增加专用测试入口。证据和本轮原件位于会话switch-shape-change；截图verification/switch-shape.png。当前已运行实例是否加载新版本仍须现场核对。
## 20. 2026-11-10 独立经典高斯模糊主题

- 用户明确批准新增独立 Classic Blur 主题，Liquid Glass 默认和原分支保留。设置→控制中心→控制中心主题使用分段选择器，原草稿/保存/实时预览链路持久化 MaterialMode；保存即时更新现有正式控制中心。
- 新 ClassicBlurBackdrop 使用唯一共享实时背景层（24DIP Gaussian、Quality），卡片仅平面半透明填充，关闭本模式折射、扭曲、光照 Shader、边缘/阴影与玻璃辅助淡化。继续现有同一 WGC 采样和即时上传，不冻结背景、不改 Shader/采集服务、布局、图标或弹簧参数。底板背景留白不改变窗口及卡片尺寸。
- Release 0警告/0错误；新主题专项和实际设置切换/保存专项均通过。七次背景变色经过真实屏幕读回，包含窗口移动及125%面板/115%控件缩放；快速切换复用同一采集服务、唯一共享模糊层，离开模式解除源绑定，玻璃效果恢复。
- 180Hz / 160.42% DPI 两轮反序最高玻璃画质与经典主题对照见 docs/CLASSIC-BLUR-2026-11-10.md 和 .json。记录的是回调间隔/进程CPU时间/瞬时WS和Private Bytes，不能冒充真实FPS；真实呈现、GPU/DWM和长期内存验收仍未完成，屏幕背景响应并非零延迟。
- 本轮原件和审计在会话 classic-blur-change。用户 data 未改，未强制退出或重启旧实例；下次运行仍须核对实际二进制。不要重复无新变化的旧测试。

## 21. 2026-11-10 无底板背景模糊、正圆及 Filled 图标

- 用户最后明确“没有底板，只给背景加一层模糊”，取代上一节早期整块填色底板方案。ClassicBlurBackdrop 已移除填色与圆角底板，唯一 Gaussian24/Quality 图像层位于窗口内容之后，外围72DIP渐隐到清晰桌面，绝对遮罩防止外扩采样泄漏，不增加捕获或模糊实例。深色卡片保留；卡片白字、外围标题使用独立背景亮度适配。
- 修复胶囊内部按钮随外框内缩裁剪而缺角：内部使用独立等宽高 EllipseGeometry，外框0/35/99%均正圆。ControlCenterIcon 使用官方Ionicons v8 Filled SVG（29语义/28资源），统一视口及离线光学中心，缓存冻结几何；补上点击缩放、容器按压、轻摆及实际状态交叉淡化。APP侧栏原SVG未改，主动画和系统API不改。
- Release 0警告/0错误；新refinement专项及受影响classic-blur回归通过，包括屏幕背景响应、移动/缩放、快速反向与模式恢复。HLSL/.ps/WGC服务/ControlMotion哈希未变。实际FPS/GPU和长期稳定性仍未验收，不宣称零延迟。
- 详见 docs/CLASSIC-REFINEMENT-2026-11-10.md/.json；会话classic-refinement-change保留原件、无底板澄清前快照、官方来源哈希和截图。用户配置未改、旧实例未强制关闭。

## 22. 2026-11-10 控制中心入口轻滑出

- 用户要求弹出减少闪亮和阻力。AnimateOpen 保留24/18DIP位移，取消卡片独立淡入，只留整体淡入；初始缩放改为窗口.985/卡片.99，逐行延迟10ms/上限60ms。采用现有PageDamping .82并补偿速度，解析收敛423.07→406.71ms。
- ControlMotion.To 新增可选 damping（默认仍.90），既有速度继承与数学求解不变；详情、关闭、布局、Shader/WGC、ClassicBlur和图标未改。
- Release主程序0警告/0错误；新--verify-entrance-motion定向检查两种材质、单层淡入、轻缩放、收敛及中途关闭通过。只记录属性曲线，不冒充真实FPS或GPU性能结论；实际观感仍需验收。
- 报告docs/ENTRANCE-MOTION-2026-11-10.md；会话entrance-motion-change保留原件及verification/entrance-traces.json。未改用户配置、未强制关闭旧实例。

## 23. 2026-11-10 APP Dashboard与青绿导航

- APP本体改为白色/浅灰Dashboard、240/68DIP可折叠侧栏、七主分类与底部关于；选中#E5F0F0/#00575B，中性Filled图标。默认设置首页section8；旧0..7业务索引保留，通过显式映射与新导航分开，重建/预览调用使用CurrentSettingsSection。
- 原设置功能与草稿保存链路保留。图标/倒影从外观进入；应用、位置、启动、窗口主题归通用。新增Overview partial，六卡读取草稿与真实内存快照；小组件、GPU、FPS等无数据源均明确未接入。
- 开关55×33/25DIP正圆、四边4DIP、22DIP行程、200ms，关灰开黑。公共表面和配色资源统一，现有200/270ms PageHost导航不另建系统。
- Release0警告/0错误；定向及受影响设置检查通过，67个非设置源码哈希不变。未改用户data、未强制重启；不宣称FPS/GPU或长期验收。报告docs/APP-DASHBOARD-2026-11-10.md/.json，原件与截图在会话dashboard-redesign。

## 24. 2026-11-10 设置开关缩小

- 用户反馈开关过大；由55×33缩为44×26 DIP，滑块25缩为18 DIP正圆，四边仍为4 DIP，行程22改18 DIP。关灰开黑、200ms缓动及点击行高度保留。
- Release构建0警告/0错误；原开关专项6项通过，包括连续反向不越界。只修改设置开关与对应尺寸断言，未重复整套测试；原件、日志和截图在会话switch-compact-change。未强制重启旧实例。

## 25. 2026-11-10 交互式下滑、Symbol Effects、滑块强弹性及新模块

- 标题区域可拖动的96DIP Reveal进度，.82速度/.82阻尼约660ms；位置/速度投影吸附，同窗连续入口可反向，透明度带动两种材质同步过渡。收起完成后才取消采集与释放服务；外部截图等等待真正关闭，反向会取消工具。
- 图标与背景分离：背景.975/约147ms；原SVG分层Wi-Fi、声波、太阳及独立蓝牙/月亮/飞机/铃铛效果。原图形与光学中心不改，新官方资源和离线中心已加入。
- 音量/亮度共用ControlCenterElasticSlider，连续值、系统Clamp0..100、渐近22DIP橡皮筋及.66阻尼回弹，可重新抓取。布局位置与Shader/采集不改。
- NightLight/Airplane/MobileHotspot追加枚举，旧配置自动注册为隐藏项，从编辑控制添加。夜间/飞行为明确标注的Windows原生设置入口；热点真实WinRT状态/异步开关，失败给出原因与原生入口，硬件启停尚未验收。
- 修复To无绑定基值闪回和NavigationInk跨卡片父级未释放。Release0警告/0错误；专项42项及受影响原卡片反向探针通过。28个既有渲染/采集等文件哈希不变；没有新增实际FPS/GPU或长期验收结论。报告docs/INTERACTIVE-MOTION-2026-11-10.md/.json，会话interactive-motion-change有原件和日志。未强制重启旧实例或修改用户data。

## 26. 2026-11-11 控制中心交互与网络旁附件

- 保留交互下滑，卡片逐行34DIP/42ms，最高错开240ms；空白返回消费按下/抬起，修复透明区域命中与一级按钮穿透。圆圈快捷操作、其他区域详情，移除卡片三角，蓝色横轨；编辑左上减号、右下弯角，保留原布局保存。
- 控制中心向右靠，约2cm外围渐隐并按工作区裁切。经典卡片提亮，深色模式真实开启时圆圈变白，经典胶囊活动衬色统一。
- 入口最终为网络旁32→28DIP的非激活任务栏附件，不是托盘或中间应用槽；复用既有UIA刷新、任务栏适配的40DIP留白。普通任务栏模式保留Windows默认控件可见性。独立入口旧试验已取代，以当前源码为准。
- tools/Run.ps1正式启动Release。平衡计划+最佳能效下Debug/Release同条件回调约93.45/93.82，不能冒充实际FPS；最终实际FPS/GPU对齐尚未验收。报告docs/CONTROL-POLISH-2026-11-11.md，原件/日志在会话control-polish-change。

## 27. 2026-11-11 卡片磨砂修正

- 用户纠正增强的是卡片而非外围背景：ClassicBlurBackdrop默认恢复24DIP/Quality；新增同类共享卡片层40DIP、83%混合强度，通过真实轮廓/变换/透明度遮罩限定于卡片，前景文字图标不模糊。两层共用同一实时位图和捕获服务，模式切换复用，关闭释放，禁止按卡片复制采集。
- 外围约76DIP渐隐空间保留；网络旁图标20→16DIP、附件宽28DIP。Explorer/UIA核对45×44物理像素、网络左侧间隔10像素。
- Release0警告/0错误，新--verify-card-frost九项通过。旧测试材质层数断言同步为两个共享层，未重跑无关检查；新增层的真实FPS/GPU成本未验收。报告docs/CARD-FROST-CORRECTION-2026-11-11.md；会话card-frost-correction留有原件和证据。正式实例已正常更新，未强制结束或丢弃草稿。

## 28. 2026-11-11 玻璃与磨砂共享外围模糊

- 两种材质共用 ClassicBlurBackdrop 与原实时位图；基底半径 22 DIP，磨砂卡片仍为 40 DIP/83%。玻璃原 Shader 不变。
- 遮罩按变换后的控制中心边界外扩 57 DIP，88 DIP 平滑渐隐、左上较强向右下散开；亮背景最多 7% 黑色压暗，暗背景不额外压暗，首帧前透明。
- 专项 15 项通过（含实际遮罩绘制范围/方向、切换复用和关闭释放）；Release 0 警告/0 错误，已正常更新正式实例。实际呈现 FPS/GPU 与完整观感未验收。报告 docs/SHARED-BACKDROP-2026-11-11.md，会话 backdrop-scatter-change 保留原件和日志。

## 29. 2026-11-11 双材质帧率诊断，目标未完成

- 新MaterialPerfChecks用WGC窗口帧SystemRelativeTime观察合成更新，不能当屏幕扫描FPS；180Hz/平衡计划/最高玻璃，同布局Blur-Glass-Glass-Blur两轮。快速反向基线Blur53.82/s、Glass8.42/s，P95约22.22/200.01ms。完整报告docs/MATERIAL-FPS-IN-PROGRESS-2026-11-11.md与会话fps-parity-change/baseline/results.json。
- 局部纹理原型通过24组GPU截图一致性检查但整体收益不足；亮度批量/原始帧优化通过32项计算一致性检查且微基准更快，但同条件raw-perf仍未追平，部分滑块/尾部指标变差。GPU、CPU实验均撤下正式业务源码，保留会话实验副本、截图和数据供分析，禁止宣称已提高FPS。
- AdaptiveGlassAppearance、ControlCenterWindow.Rendering/Presentation、DockSurface、ControlCenterWindow.cs五个生产文件及Shader均核对恢复本轮前哈希；保留上一轮共享实时模糊修复。正式Release0警告0错误、正常运行核验PID33752。只有测试工具MaterialPerfChecks及Program入口新增；诊断工程构建成功（GuiChecks.cs:98既存两项nullable警告）。
- UAC请求最终返回“操作已被用户取消”，没有管理员ETW记录/后台会话，未重复自动请求。手动入口会话fps-parity-change/Start-Admin-Profile.cmd，指向恢复后diagnostic-build；会保护设置草稿、暂停正式实例、采集并恢复应用。工具不能替代Windows安全桌面的本机实际点击。
- 同用户native-pc.json线程模块采样见WPF/D3D9、NVIDIA驱动、GDI及JIT，命中包括等待状态，不能当GPU耗时或具体函数瓶颈证据。下一步需管理员ETW调用栈/GPU队列诊断及可靠呈现数据，再针对主要瓶颈优化，不能重复盲调弹簧或降低画质。
## 30. 2026-11-11 控制中心阴影彻底移除与重复映射合并

- 先在隔离源码快照计数，正式产品无RenderAudit：单次Rendering订阅、单主采集泵，无同内容重复WritePixels。确认ApplySample与Rendering重复映射；改为同纹理/坐标的上传仍立即WritePixels，常规映射并入既有Rendering；首帧/纹理重建/坐标变化仍即时映射。16场景中映射8965→4526、采样额外映射4803→0，保留必要每卡折射和22/40DIP不同模糊。
- 删除ControlPlateShadow.cs、_plateShadows、阴影兄弟层、轮廓缓存/UpdateShadow/阴影恢复附加属性，控制中心滑块/开关模板不再创建玻璃投影。删除ControlCenterOptions的ShadowOpacity/ShadowBlur/SideDarkness及cc-shadow-opacity/cc-shadow-blur/cc-side设置，旧JSON由原SettingsService忽略且保存后不再写出；Dock全局阴影保留。
- 共用LiquidGlass.hlsl编译LiquidGlass.ControlCenterLighting.ps：固定LightingOnly并编译排除黑色侧边阴影，默认Dock Shader二进制哈希完全不变。共享GlassButtonChrome/IosStyles按控制中心选择无玻璃投影，其他窗口默认阴影保留。稳定主界面持有DropShadowEffect21→0，LiquidGlassEffect40与BlurEffect23不变。
- 旧-新-新-旧交错、同机180Hz/平衡计划/最高质量：WGC窗口合成更新（不是屏幕FPS）展开返回11.98→15.67/s、P95 155.56→136.11ms；快速反向8.81→10.84/s；滑块14.99→25.09/s。实际呈现FPS/GPU帧耗时/呈现队列仍无可靠测量；时变GPU频率及部分任务栏可见性限制短时结论。色变到屏幕玻璃更新均值547.87→293.05ms，含源WPF呈现及读回/轮询开销，不能叫零延迟或光子延迟。
- 25项阴影/配置/即时上传专项、15项磨砂、42项交互回归通过；真实硬件写操作/长期稳定性未验收。Release0警告0错误，实屏独立截图无明显黑边锯齿。当前PID33908有“程序坞设置”打开，保留可能未保存草稿，未强制重启；不能把构建成功当作该实例已加载新版。关闭设置后正常重启生效。
- 详见docs/CONTROL-CENTER-RENDER-SHADOW-2026-11-11.md。会话shadow-render-change包含before、baseline-source/shadow-only-source/final-source隔离计数快照、comparison.json、paired-*、GPU CSV、背景延迟与资源计数、visual-final.png。旧fps-parity-change冻结诊断构建不代表本轮版本。

## 31. 2026-11-11 Dock窗口交互、堆叠预览与图标修正

- 新增WindowPreviewStack原生DWM实时缩略预览，悬停多窗口应用350ms显示最多3张圆角堆叠卡片，滚轮前后切换、点击激活、离开280ms关闭；不新增WGC/截图/帧计时器，关闭释放DWM句柄。DockWindow.Interactions统一菜单、外部点击、Shell注意信号生命周期，外部低级鼠标监听仅菜单/预览开启时存在，不吞桌面点击。
- 已确认新窗口命令的Chrome/Edge/Brave/Firefox、VS Code、Windows Terminal菜单增加新建窗口。点击当前前台应用收起其可见窗口组，再点恢复；异步还原延迟核对，避免误报激活失败。通知点橙色呼吸仅对应Shell任务栏attention，不是所有Toast/未读计数。
- 普通Dock图标透明呈现并按可见最长边88%归一化，GPT保留旧底板，源图自带背景不破坏性删除；最近应用/回收站增加独立明暗自适应分隔线。
- 官方Ionicons v8新增eye/scan/videocam/color-filter；修复ControlCenterIcon对SVG line/CSS描边的解析，电池轮廓/极柱不再丢失，HDR与电池区分；重新生成Metrics。APP窗口符号使用WindowControlGlyph居中绘制，hover1.17。未修改玻璃/采集/系统逻辑。
- --verify-dock-interactions 24项通过，截图checks/preview-stack.png。交互旧回归前7项通过后真实鼠标捕获失败，完整42项未通过；本轮未改滑块。独立Release0警告0错误，测试构建有GuiChecks.cs:98两项既存nullable警告；无实际屏幕FPS测量。
- 当前PID25116打开程序坞设置，正常Build.ps1因运行DLL占用复制失败，未强制结束/丢草稿。会话dock-interaction-change/release-ready为已验证独立Release；保存关闭设置后运行该目录Apply-Dock-Changes.cmd会再次检查草稿、正常退出、正式构建并启动，当前旧实例尚未加载改动。
- 报告docs/DOCK-INTERACTIONS-2026-11-11.md，before原件与checks.log/build.log/release-staged.log位于会话dock-interaction-change；实际源码当前无.git，未清理配置数据。


## 32. 2026-11-11 本阶段结束与新会话交接

- 新增 HANDOFF-2026-11-11.md，集中记录当前架构、必须保留的设计决策、最新Dock功能、测试失败/性能未完成项、证据位置与接手步骤；NEW-SESSION-PROMPT.txt为可直接粘贴的接手指令。源码和会话目录均有副本。
- 本次只读复核进程为PID10604，正式Release DLL和独立release-ready DLL的SHA256一致（0AF42E5FC61DB0292B1448790975D28A5A246C3CD540487737274679C942BCD2）；替代旧PID25116/磁盘DLL未更新的历史状态，运行功能未重新验收。
- 本次仅写文档，未修改业务源码、运行测试、构建、重启、搬迁工程或清理数据。实际呈现FPS/玻璃追平磨砂及最新真实鼠标捕获失败仍需后续处理。

