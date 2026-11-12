# 经典背景模糊、正圆和图标动效修正 — 2026-11-10

用户最后澄清：没有整块底板，只给背景加一层模糊。此要求取代本轮早期“透明黑底板”和窄边羽化方案。

## 最终外观

- 移除 ClassicBlurBackdrop 的填色 Border 与圆角底板。只保留一张实时背景 Image + Gaussian BlurEffect（24 DIP、Quality）。该层位于窗口内容后面，不在卡片父级内部，不模糊任何文字或图标。
- 模糊区外围使用 72 DIP 宽的 smootherstep 透明渐变，并处理四角。外侧清晰桌面逐渐过渡到中心背景模糊；没有框线、阴影或整块深色填充。矩形 Clip 仅防止背景采样的外扩图像泄漏，边界处 alpha 为零。遮罩使用绝对视口，与窗口坐标同步，没有增加实时模糊实例或采集服务。
- 深色半透明卡片保留（默认 #A8151518，浅配色 #A8303035），卡片内文字保持白色；独立的标题/编辑文字沿用现有亮度采样，自适应清晰背景。修复预览重建且背景静止时文字未重新初始化的情况。
- 胶囊内部按钮改为独立 EllipseGeometry 与等宽高，完全脱离外框圆角及 InsetOutline；外框 0/35/99% 圆角均为完整圆。只在直径变化时重建圆形裁剪。
- 控制中心图标替换为官方 Ionicons v8 Filled，29 个语义名称使用 28 个官方 SVG，资源沿用现有 MIT 许可。统一可见轮廓尺度、主图标最小 20DIP 视口，按光学质量中心偏移。SVG 解析/冻结缓存；光学中心在离线生成，不在首次弹出时密集测量。Refresh/Sync 采用 Filled circle 版本。
- 点击图标 0.8 倍回弹（260ms），圆形容器按压 0.9 倍，音量/麦克风/专注图标悬停轻摆并回正；实际状态图标交叉淡化 180ms，快速二态反向沿当前进度衔接。按钮动作立即执行，不等待动画；尊重现有动画开关与系统减弱动画设置。
- 所有卡片路径、导航弹簧、系统调用、布局和正式用户配置保留。Liquid Glass 的 Shader 和采集服务没有修改。

## 验证

- tools/Build.ps1 -Configuration Release：0 警告/0 错误。
- --verify-classic-refinement：宽过渡遮罩/绝对坐标/无填色层、一个可见模糊实例、深色卡片、0/35/99% 外框下的正圆、重建后的文字对比、29 个语义 SVG 的冻结/归一化、主图标尺寸、快速反向淡化、点击回弹、轻摆归零、展开返回与原玻璃模式恢复通过。
- --verify-classic-blur：仅对本轮受影响的背景/材质做回归。七次背景变色均到达上传与真实屏幕读回，包含窗口移动和 125% 面板/115% 控件缩放；快速切换保持同一捕获服务、唯一共享模糊层，退出经典模式释放源绑定，原玻璃恢复。
- SVG 原件逐字节核对官方下载，源 URL 与 SHA256 存于会话 used-official-svg-provenance.json。HLSL、.ps、WindowFrameCapture、GlassBackdropService、ControlMotion 与此前保护清单哈希一致。
- 测试构建仅保留既有 GuiChecks.cs:98 两条空值警告。未运行不相关的旧整套检查，没有硬件写入、用户配置覆盖、批量删除或强制重启。

## 当前诊断数据（不是实际呈现 FPS）

同一隔离进程、相同窗口与棋盘以外的固定渐变测量源、180Hz、160.42% DPI，最高完整玻璃参数与经典模式反序两轮。回调数据不能当作实际显示帧率、GPU 耗时或输入到光子。瞬时 WS/Private Bytes 不是长期内存验收。

| 模式/轮次 | 回调均值 ms | 回调 P95 ms | 回调 P99 ms | WS MiB | Private Bytes MiB |
|---|---:|---:|---:|---:|---:|
| LiquidGlass/1 | 35.37 | 65.74 | 67.72 | 283.16 | 551.63 |
| ClassicBlur/1 | 9.52 | 14.17 | 16.78 | 277.10 | 494.50 |
| ClassicBlur/2 | 9.32 | 14.34 | 16.40 | 284.79 | 500.13 |
| LiquidGlass/2 | 34.92 | 64.98 | 69.12 | 285.27 | 545.56 |

七次背景屏幕响应 43.02–79.68ms，包含源窗口渲染、WGC、上传、模糊、DWM 和轮询；不宣称零延迟。实际 FPS/GPU/DWM 和长时间稳定性仍未验收。原始结果另存 CLASSIC-REFINEMENT-2026-11-10.json。

## 维护与回退

核心文件：ClassicBlurBackdrop.cs、ControlCenterWindow.Material.cs / Rendering.cs / Appearance.cs / Cards.cs / System.cs、ControlCenterTokens.cs、ControlCenterIcon.cs / Metrics.cs。

修改前原件在会话 classic-refinement-change/before；用户进一步澄清前的中间版本在 before-no-plate。不覆盖用户后续修改，不将这些原件作为垃圾删除。官方 SVG 更新后，通过测试工具 --generate-control-icon-metrics <output.cs> 重新生成光学数据，并替换 ControlCenterIcon.Metrics.cs；该命令仅为离线资源生成。

实机棋盘背景图 classic-refinement-change/verification/classic-refined.png 用于清楚显示“外侧清晰、向内模糊”的过渡；不是正式用户布局配置。现有运行实例不保证已加载此版本，需正常重开加载。
