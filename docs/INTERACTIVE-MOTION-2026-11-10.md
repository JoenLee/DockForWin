# 控制中心交互弹簧、图标及边界弹性 · 2026-11-10

## 入口与交互

在原 ControlMotion 上增加可传入的初始速度。新的 Reveal 进度用 RenderTransform.Y 驱动96DIP下滑，保留原弹簧解析器和首尾C2收敛，不修改布局尺寸。零初速 speed=.82 / damping=.82，自然收敛约660ms；手势带速度时由实际弹簧状态决定。整个窗口透明度随进度平滑变化，实时背景模糊和内容一起淡入；不冻结背景、不调整Gaussian质量或Shader采样。

控制中心标题区域可拖动，上拉收起、向下恢复；松手按当前进度和速度投影选择目标，端部使用非线性阻尼。已有入口连续点击可在同一窗口反向，不提前取消后台采集或销毁资源。只有最终收起才释放服务。鼠标捕获、Escape/关闭及工具调用均考虑中断：截图等工具等待实际关闭，中途重新打开会取消待执行工具。

修复了两个在此验证中暴露的问题：未绑定的动画属性重新定向时，基值未同步可能瞬时闪回默认值；快速改换卡片时，旧NavigationInk未先Release导致临时父级冲突。前者只在无绑定属性写入本地基值，绑定属性沿用SetCurrentValue；后者仅增加先恢复原父级，不改变详情运动曲线和结构。

## 双层图标反馈

外层圆背景约147ms轻按到.975，恢复1；取消明显悬停放大及通用大幅图标压缩。内部SymbolEffect独立于背景，有限时动画、SnapshotAndReplace，读取当前值重新定向，无常驻新帧循环。

- Wi-Fi：复用原SVG三条路径，间隔60ms轻缩放/透明度波纹。
- 蓝牙：.90→1.07→1的360ms Symbol Pulse。
- 飞机：沿官方飞机朝向右移3DIP后缓出复位。
- 月亮：-8°轻倾后回正。
- 太阳：外围原路径依次增强，中心路径保持稳定。
- 音量：仅声波原路径依次变化，扬声器主体稳定；静音沿用状态交叉淡化。
- 铃铛：新增可选官方Filled图标，围绕顶部8°/−5°/2°衰减轻摆；其他图标回退为克制微动。

所有既有SVG文件和光学中心保持原样；新增airplane/radio/notifications来自官方Ionicons v8，原MIT许可继续适用。几何冻结缓存、分层变换复用，动画完成直接绘制原始整体Geometry。精确像素比较验证了Wi-Fi、蓝牙、月亮、太阳、音量、飞机、铃铛在连续触发后恢复原静态图像。图标适配原显示刷新参数，两种材质共用同一实现。

## 强弹性横向滑块

音量和亮度共用ControlCenterElasticSlider，真实指针捕获、连续double数值，不设整数吸附。0/100之外的视觉偏移为 sign(d) × 22 × (1 − exp(−abs(d)/55))，渐近22DIP；系统值严格Clamp。通过RenderTransform协调轨道、填充和现有表面轮廓，卡片Root位置、标题和图标不随整卡移动。松手 damping=.66弹簧回弹，重新抓取拇指保留当前显示的拉伸。原系统写入合并/异步链路保留。

## 新功能模块

追加枚举值不改变已有持久化编号，Normalize自动注册为未显示控件。可在“编辑控制 → 添加控件”或APP设置启用，不擅自覆盖当前布局。

- 夜间护眼：明确标注为Windows夜间模式入口，可进入原生色温/计划页面，不写未公开系统配置或虚构状态。
- 飞行模式：原生Windows飞行设置入口，不将Wi-Fi关闭当作飞行模式。已具备飞机专属微动效。
- 移动热点：NetworkOperatorTetheringManager读取真实能力/开关/设备数量，支持时异步Start/Stop；无连接、政策/硬件/权限受限时显示实际原因并提供原生设置入口。没有在验收中实际启停机器热点。

接口依据：[Windows设置URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)、[热点管理器创建与能力要求](https://learn.microsoft.com/en-us/uwp/api/windows.networking.networkoperators.networkoperatortetheringmanager.createfromconnectionprofile)。

## 验证与限制

Release构建0警告/0错误。--verify-interactive-motion 共42项通过：真实鼠标拖动/松手、越界与回弹中重抓、快速图标重播像素一致、两种材质反向、持续采集、工具取消与等待、三个新增详情页和注册。

受影响原卡片展开/返回及快速反向探针通过。原始回调统计（不是屏幕呈现FPS）：

frames=1403; callback FPS=92.33; mean=10.83ms; P95=17.44ms; P99=25.23ms; max=52.23ms; response mean=14.30ms; response P95=36.49ms; layout events=1183; UI allocations=124.15MB; captures=2524; WPF tier=2
PASS Wi-Fi repeated open/return and rapid reversals; callbacks are not GPU presentation FPS

此轮没有同场景优化前实际呈现基线，也未采GPU/ETW，因此不能据此宣称实际FPS提高、无GPU成本或零延迟。真实热点启停、跨屏DPI/触摸和长期内存仍待验收。少量原有测试项目GuiChecks.cs:98可空性警告保留；没有重复无关旧性能套件。

28个已有捕获/服务/Shader与DockSurface、ClassicBlurBackdrop文件哈希未变；仅新增MobileHotspotService，背景捕获、HLSL和材质管线未改。原件、日志、源码实施脚本、SVG来源哈希和汇总位于会话interactive-motion-change；未写用户data、未清理未提交文件或强制重启运行实例。
