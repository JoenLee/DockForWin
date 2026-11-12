# 控制中心重复渲染诊断与阴影移除 · 2026-11-11

代码与Release构建已完成。当前正式PID33908仍打开“程序坞设置”，为保护可能未保存的草稿，没有强制关闭或重启，因此不能声称该实例已经加载新构建。关闭设置并正常重启应用后生效。独立新版窗口已完成下面的测试和画面检查。

## 诊断依据与最小方案

实际源码D:\DockForWindows；该目录不是Git仓库，未做reset、clean或覆盖用户配置。修改前副本在会话shadow-render-change/before；调用计数只注入隔离源码快照，未给正式产品加入计数、计时循环或新依赖。

Dock：DockWindow.UpdateGlassAsync → 独立的GlassBackdropService → 一个主DockSurface，采集有_busy保护。控制中心：单个CaptureLoop → Sample/ApplySample → 一张共享WriteableBitmap → 各卡片按自己的形状折射，另有Rendering动画循环与两个不同半径的经典模糊层。不同形状卡片的折射、高光，以及22DIP外围模糊/40DIP卡片磨砂并不相同，不能因为代码相似就删除或合并。

确认的问题：

- 控制中心每次采样提交都MapGlassFrame，同时Rendering又MapGlassFrame；即使像素未变化、背景纹理与坐标没变化，也遍历卡片。完整16场景记录区间累计，映射8965次，其中采样触发4803次。真实上传仅1118次，说明采样次数不能当成上传次数。原有像素比较已经避免相同内容再次WritePixels，没有据此改动采集或传输链。
- Rendering订阅1次、主CaptureLoop启动1次，没有发现重复注册或重复主泵。记录中WGC设备2个，包含测量工具专门观察控制中心窗口的1个，产品背景采集只有1个；模式切换没有新增背景采集设备。每个不同来源窗口有自己的WGC会话，是现有多窗口背景合成所需。
- 侧边SafetyFade“只接触、没有实际面积”计数为0，未证实该假设，不改其视觉逻辑。
- 原玻璃投影有专用ControlPlateShadow节点与轮廓缓存；控制按钮模板还有投影，Shader有独立黑色侧边阴影。

实施：像素仍在ApplySample中立即WritePixels；同一位图同一采样坐标的常规映射只标记“内容变化”，由既有Rendering更新。首帧、新位图和采样坐标变化仍立即映射，没有推迟上传、缓存旧帧或增加刷新节流。没有改动画轨迹、时长、弹簧、卡片尺寸或图标。

## 阴影移除与配置兼容

- 删除ControlPlateShadow.cs、_plateShadows字典、外投影兄弟节点、UpdateShadow/ApplySurfaceShadow及阴影恢复附加属性；导航层取消阴影孩子，保留原玻璃和内容父级恢复逻辑。
- 删除控制中心滑块及开关模板的玻璃投影片段。共享IosStyles新增可选无玻璃投影片段，默认模板保持原内容；GlassButtonChrome在控制中心不创建或附加投影Effect和投影层，其他窗口保留原参数。
- 同一LiquidGlass.hlsl编译控制中心专用高光版本，LightingOnly编译时固定，并排除只服务于黑色侧边阴影的计算。折射仍用原Shader；没有复制两套玻璃源码。专用高光字节码静态指令槽247→156，不能把此比例当GPU耗时改善比例。
- 删除控制中心ShadowOpacity、ShadowBlur、SideDarkness三个配置字段以及cc-shadow-opacity、cc-shadow-blur、cc-side三项设置；保留Dock全局阴影选项。旧JSON未知字段由现有SettingsService安全忽略，保存时自然不再写出。专项验证没有触发配置恢复、没有丢失热键或Dock阴影，未重写正式用户data。
- 背景衬色、亮背景轻微压暗、透明度、折射、Gaussian模糊、边缘白色高光及按钮状态反馈保留；它们不属于移除的投影。

## 调用计数与Effect资源

计数为两轮、16个场景的实际记录区间合计，包含经典磨砂和玻璃；基线记录66.92秒，最终记录65.87秒。场景时长和调度略有波动，具体每行原始值见comparison.json。

|项目|修改前|修改后|
|---|---:|---:|
|有效Rendering动画回调|4162|4526|
|材质映射遍历|8965|4526|
|采样提交额外映射|4803|0|
|映射中的卡片访问|62257|25358|
|映射CPU侧墙钟耗时合计，ms|773.72|431.55|
|阴影Update调用|63947|0|
|阴影轮廓重建|321|0|
|经典卡片遮罩更新|5208|2979|

没有承诺降低所有调用：ConfigureBackdrop仍处理布局和材质变化，累计10336→10846；帧数更多时计数可能更高。没有无依据地删掉必要布局或高光更新。GPU/绘制耗时不能用上表CPU墙钟时间替代。

首次打开稳定主界面、未进入详情时，按引用去重统计实际持有的Effect（包括隐藏/未附着但仍持有的字段），18个DockSurface在两版一致：

|Effect|修改前|修改后|
|---|---:|---:|
|DropShadowEffect|21|0|
|LiquidGlassEffect|40|40|
|BlurEffect|23|23|

没有用删掉玻璃效果实例换帧率。导航新增的必要表面首次创建后复用，不把不同页面状态的实例数直接混为一谈。

## 性能对比

条件：同机RTX5060 Laptop GPU、2560×1600/180Hz、160.42%DPI、相同用户卡片布局与窗口尺寸；平衡电源方案，未启用高性能、未锁帧/关闭VSync。独立测试进程含背景测试窗口、控制中心和WGC观察器，最高玻璃参数、Quality Gaussian。滑块用连续数值驱动且禁止硬件写入，非真实鼠标拖动全链路延迟测量。

实际面板呈现FPS、GPU Frame Time、Present队列延迟没有可靠ETW样本，本报告不填估算值。下面是WGC提供的控制中心窗口合成更新率与更新间隔，既不是Rendering回调频率，也不保证等于物理屏幕显示FPS。观察器会增加开销，但所有对照使用同一方法。

追加旧—新—新—旧紧邻对照，每版2次；表中为每轮指标算术均值，P95/P99为各轮分位数的均值，不是合并全部帧后的分位数。来源窗口和矩形已记录captureSources，任务栏在部分时段可见性改变，GPU频率也由平衡模式自动调整，所以短时数值不代表固定FPS保证。

|玻璃场景|合成更新/秒，前→后|平均间隔ms|P95 ms|P99 ms|
|---|---:|---:|---:|---:|
|静止界面＋动态背景|20.06 → 27.01|49.97 → 37.24|113.89 → 80.56|130.56 → 97.22|
|展开／返回|11.98 → 15.67|83.85 → 63.82|155.56 → 136.11|194.45 → 166.67|
|连续快速反向|8.81 → 10.84|113.65 → 92.24|194.45 → 136.12|208.34 → 152.78|
|滑块变化|14.99 → 25.09|68.48 → 39.90|125.00 → 83.34|186.12 → 94.45|

|玻璃场景|测试进程CPU占全机%，前→后|整卡GPU利用率%，前→后|Working Set MiB|Private Bytes MiB|整卡已用显存MiB|
|---|---:|---:|---:|---:|---:|
|静止界面＋动态背景|8.86 → 9.41|19.44 → 23.75|295.94 → 294.68|889.76 → 872.07|2255.88 → 2265.19|
|展开／返回|8.86 → 8.39|15.13 → 16.47|304.66 → 301.30|757.31 → 683.67|2158.38 → 2094.47|
|连续快速反向|7.65 → 7.55|16.45 → 15.94|305.12 → 300.65|948.27 → 945.56|2168.00 → 2247.75|
|滑块变化|7.08 → 7.36|18.56 → 23.47|298.94 → 299.56|885.01 → 931.69|2061.89 → 2215.18|

GPU数据来自nvidia-smi，是整个适配器而非单个进程；更高更新吞吐可伴随更高GPU利用率。Private Bytes不是Working Set；整卡显存也不是应用独占显存。未测系统全部Committed Memory或宣称满足700MB目标，没有用强制GC或EmptyWorkingSet修饰读数。

另有统一Blur—Glass—Glass—Blur两轮记录，便于核查磨砂没有通过画质降级换取结果；阶段间来源窗口可见性差异使其主要作为辅助对照：

|场景|合成更新/秒，前→后|P95 ms，前→后|
|---|---:|---:|
|ClassicBlur / 静止界面＋动态背景|49.12 → 51.68|36.11 → 22.22|
|ClassicBlur / 展开／返回|42.22 → 44.20|41.67 → 38.89|
|ClassicBlur / 连续快速反向|51.15 → 52.68|22.22 → 22.22|
|ClassicBlur / 滑块变化|42.00 → 45.15|38.89 → 38.89|
|LiquidGlass / 静止界面＋动态背景|25.54 → 33.09|52.78 → 50.00|
|LiquidGlass / 展开／返回|11.10 → 17.07|166.67 → 133.34|
|LiquidGlass / 连续快速反向|9.08 → 11.44|191.67 → 133.34|
|LiquidGlass / 滑块变化|13.07 → 25.21|130.56 → 91.67|

## 背景同步与可见延迟

两版各8次纯色背景切换，在同一最高玻璃设置下，分别观察共享位图像素和屏幕亮度卡片空白区域：

|指标|前，ms|后，ms|
|---|---:|---:|
|触发背景切色→位图更新|325.38|158.43|
|触发背景切色→屏幕玻璃可见更新|547.87|293.05|
|位图更新→可见更新差值|222.49|134.62|

该量包含背景源自身的WPF呈现、采集、Glass合成、屏幕读回及轮询开销，样本小，不是精确背景帧年龄、GPU呈现队列延迟或Input-to-Photon。未观察到本轮映射合并使该测试延迟变差，但当前延迟仍明显，不称零延迟。

## 验证与尚存限制

- 25项ShadowRenderChecks通过：旧配置、阴影类/缓存删除、主卡片与Wi-Fi/蓝牙/音量/亮度/麦克风详情无玻璃投影、模式切换、位图即时更新、反向返回、关闭释放、Dock设置和默认GlassButtonChrome阴影保留。
- 15项CardFrostChecks、42项InteractiveMotionChecks通过，共82项相关检查；没有重复跑无关全套测试。真实Wi-Fi连接、蓝牙配对、热点启停等硬件写操作未执行，不当作硬件验收。
- Release 0警告/0错误；测试项目有GuiChecks.cs:98既存2项nullable警告。独立实屏截图visual-final.png检查轮廓、高光和透明度，无明显锯齿或异常黑边；纯蓝测试背景不是新底板配色。
- DockWindow.cs哈希65a480924bb448e9067a1d44aac3fcfa1c7edc2df0ca4f6df9400d18823ee240，与修改前一致；默认LiquidGlass.ps哈希37497cb8323942064837f6f387774a5f0c092d6fd67efbc70276632012634d42，与修改前完全相同。共享默认阴影模板保留并通过专项检查；没有改Dock主渲染调用链。
- 玻璃仍未接近180Hz，快速反向的长尾仍高；下一步需GPU/合成队列与WPF原生渲染调用栈分析，不能继续把所有卡片的必要独立折射称为重复。长期稳定性、完整人工视觉、实际屏幕呈现FPS/GPU帧耗时尚未验收。

原始计数/场景指标/来源日志/温度频率记录/截图/备份均在C:\Users\HENG\Documents\ChatGPT\DockForWindows\shadow-render-change。正式源码不含RenderAudit。当前设置窗口的草稿已保留，PID33908未被强制重启，用户正常重启后加载新版。

## 主要文件与核心位置

|范围|文件与函数|
|---|---|
|重复映射合并|Views/ControlCenterWindow.Rendering.cs：OnRendering、ApplySample|
|专属阴影删除|删除Views/ControlPlateShadow.cs；清理ControlCenterWindow.Appearance.cs、Cards.cs、Navigation.cs、Material.cs、Presentation.cs、System.cs和ControlCenterWindow.cs的创建、更新与父级引用|
|控件模板投影|Views/GlassButtonChrome.cs：Refresh及阴影延迟创建条件；IosStyles.Create可选无投影片段；ControlCenterWindow.Polish.cs滑块模板|
|专用无阴影高光|Views/DockSurface.cs：UseControlCenterLighting；LiquidGlassEffect.cs：SetControlCenterLighting；Shaders/LiquidGlass.hlsl及新增LiquidGlass.ControlCenterLighting.ps；tools/Compile-Shaders.ps1和csproj资源声明|
|设置与旧配置|Models/ControlCenterOptions.cs、Views/SettingsWindow.ControlCenter.cs|
|必要检查|tests/DockForWindows.Tests/ShadowRenderChecks.cs、BackdropLatencyChecks.cs、Program.cs；清理ControlMotionChecks旧no-shadow诊断分支|

正式目录没有引入RenderAudit.cs、统计定时器或性能日志高频写盘；计数器均属于会话内隔离快照。没有清理用户缓存目录、发布包或未提交文件。
