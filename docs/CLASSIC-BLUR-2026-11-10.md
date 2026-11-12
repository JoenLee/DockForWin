# 经典高斯模糊主题 — 2026-11-10

## 实现与边界

- 新增 ControlCenterMaterialMode（LiquidGlass 默认 / ClassicBlur），Copy、Normalize、SettingsService、草稿及 ApplyPreview 使用原链路。设置首页新增分段主题按钮；保存后现有正式控制中心也调用 ApplyPreview，无需重启切换主题。
- ClassicBlurBackdrop 是唯一共享实时背景模糊层：使用现有 WGC→CPU 合成→WriteableBitmap 源，24 DIP Gaussian、Quality，叠加浅/深灰材质。该层在文字与卡片下方，卡片只画半透明填充，保留原几何、布局、图标、状态反馈、导航和弹簧动画。
- DockSurface 新增默认关闭的内部 ClassicMaterial 分支；GlassButtonChrome 只在经典控制中心内绕过镜片/光照/边缘。经典主题关闭材质阴影、模板阴影、玻璃安全边缘辅助模糊与边缘淡化，避免平面卡片周围发白。切回玻璃恢复原效果与遮罩。
- 不新增采集服务、不新增帧队列、不降低采集频率、不冻结背景、不改 HLSL/.ps。继续即时上传最新完成的采样。窗口底板增加 14 DIP 横向、8 DIP 纵向背景安全留白，卡片位置和窗口尺寸不变。
- 这里“一次模糊”指仅一个可见 BlurEffect 实例。WPF 内部可能执行多个 GPU pass，未将实例数冒充 GPU pass 数或 GPU 测量。

## 同条件对照（诊断，不是实际 FPS）

同一隔离进程、同一窗口几何、同一渐变背景、180Hz、DPI 160.42%。执行相同 Wi-Fi 展开/返回和快速反向序列。顺序 Liquid→Classic→Classic→Liquid，单轮约 4.85 秒。玻璃组 intensity/distortion/frost/highlight/side/grain=1，opacity=1，highlightWidth=8、tint=.25，阴影启用，surface BlurEffect 强制 Quality；经典组固定完整分辨率共享 Gaussian 24、Quality。并非既有玻璃版本优化前后回滚对照，而是两种主题的成本对照。

| 主题 / 轮次 | 回调均值 ms | 回调 P95 ms | 回调 P99 ms | CPU 单核等效 | WS MiB | Private Bytes MiB |
|---|---:|---:|---:|---:|---:|---:|
| LiquidGlass / 1 | 40.50 | 69.79 | 76.29 | 203.85% | 282.03 | 562.75 |
| ClassicBlur / 1 | 9.18 | 13.83 | 16.77 | 155.29% | 278.38 | 495.50 |
| ClassicBlur / 2 | 9.21 | 13.52 | 16.65 | 149.89% | 287.14 | 500.42 |
| LiquidGlass / 2 | 37.81 | 68.92 | 70.45 | 152.50% | 286.26 | 535.52 |

回调来自 CompositionTarget.Rendering，仅反映 UI 调度与动画更新间隔，不能推断实际呈现 FPS、GPU frame time 或 DWM 队列延迟。CPU 为进程 CPU 时间/墙钟时间，100%=占满一个逻辑核，包含测试源窗口和测试逻辑，未按机器逻辑核数归一化。内存为每轮末尾瞬时样本，不是完整程序长期稳定值或峰值，也未强制 GC。Private Bytes 不能当作整机 Committed Memory；GPU 显存未测。

## 背景新鲜度与功能检查

- 七次红蓝背景变化均在上传位图和真实屏幕读回中观察到：静止、展开/返回、窗口移动，以及 125% 面板 / 115% 控件缩放。屏幕响应 54.73–92.24ms；此值包含测试源窗口呈现、WGC、UI 上传、模糊、DWM 和 5ms 轮询，不等于输入到光子，也不是零延迟。
- 16 次交替切换保持同一采集对象、同一共享模糊层；离开经典模式解除模糊源绑定；返回 Liquid Glass 恢复 Shader。检查滑块原实例及数值路径，测试屏蔽系统音量写入；真实硬件音量/亮度拖动未全面验收。
- --verify-classic-theme-ui 通过：分段切换只改草稿、实时预览复用窗口/采集、保存持久化并保持设置窗口打开、开机启动注册未改变。
- --verify-classic-blur 通过：旧配置默认与非法模式回退、持久化、平面卡片、唯一可见 BlurEffect、无可见光学 Shader/材质阴影、背景变化屏幕响应、物理坐标映射、缩放、快速导航和切换，以及 DockSurface 默认行为。
- tools/Build.ps1 -Configuration Release：0 警告 / 0 错误。专项测试构建保留既有 GuiChecks.cs:98 两条空值警告。未重复旧整套无关测试。

## 代码位置与回退

- Models/ControlCenterOptions.cs：MaterialMode、Normalize。
- Views/ClassicBlurBackdrop.cs：共享模糊层及物理像素映射。
- Views/ControlCenterWindow.Material.cs：主题应用、平面填充、遮罩和阴影恢复。
- Views/ControlCenterWindow.cs / Rendering.cs / Appearance.cs / Presentation.cs / Boundaries.cs：现有生命周期、Surface、采样分发、配色与预览接入。
- Views/DockSurface.cs / GlassButtonChrome.cs：默认不改变 Dock 的材质分支。
- Views/SettingsWindow.ControlCenter.cs / DockWindow.cs：主题选择、保存即时生效。
- tests/ClassicBlurChecks.cs / ClassicThemeUiChecks.cs / Program.cs：新定向检查。

实际工程无 .git，本轮原件和审计留在 C:/Users/HENG/Documents/ChatGPT/DockForWindows/classic-blur-change / before 与 change-audit.json。未覆盖或清理其他未提交修改，未修改正式用户 data。原 Shader、背景采集服务、图标与 ControlMotion 与此前保护清单哈希相同。

## 尚未完成的验收

实际呈现 FPS、GPU 耗时/占用/显存、DWM 队列和输入到光子尚未可靠测量，不能宣称达到 180FPS 或零延迟。长期稳定性、真实跨屏 DPI、持续动态壁纸和完整硬件功能仍需后续实机验收。本轮保留弹簧参数和动画时长，没有凭诊断回调值盲目修改曲线。

原始数据：[CLASSIC-BLUR-2026-11-10.json](CLASSIC-BLUR-2026-11-10.json)。截图在会话 classic-blur-change/verification（classic-blur.png、classic-dark.png、classic-scaled-light.png、theme-settings.png）。
