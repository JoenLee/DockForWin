# 2026-11-11 两种材质共享外围模糊

- Liquid Glass 与 Classic Blur 都显示同一个 ClassicBlurBackdrop 实时背景层；继续共用原 WGC 位图，不启动新捕获，不缓存过期画面。玻璃卡片折射、扭曲、高光与原 Shader 不变。
- 外围 Gaussian 半径由 24 微降至 22 DIP。以控制中心实际变换后的边界向外 57 DIP（标准逻辑 DPI 下约 1.5cm）结束，88 DIP 平滑渐隐带从边界内侧开始；左上较强，向右下逐渐减弱。实际物理距离受显示器标定、面板缩放及工作区裁切影响。
- 复用已有平滑背景亮度判断：亮度 .55 至 .9 之间平滑增加黑色叠层，最多 7%，暗背景为零。第一帧前叠层透明，避免无捕获时黑闪。
- 磨砂卡片共享层仍为 40 DIP / 83% 混合，玻璃模式不显示该卡片层。移动只更新遮罩 Viewport，尺寸变化才重建冻结的渐变绘图。
- 改动：Views/ClassicBlurBackdrop.cs、ControlCenterWindow.Material.cs、ControlCenterWindow.Rendering.cs；对应 CardFrostChecks 更新。
- 专项 15 项通过：实际绘制透明范围和方向、亮暗背景、位图共享、两模式切换复用、详情返回、关闭释放。Release 构建 0 警告/0 错误；测试工程另有既存 GuiChecks.cs:98 两项 nullable 警告。
- 已正常退出旧实例并启动 Release，核验 PID 15780 与正式 data 路径；未强制结束或覆盖用户配置。
- 本轮没有可靠实际屏幕 FPS/GPU 帧耗时测量，不据此声称性能改善。完整动态观感及非高性能模式帧率尚待实机验收。
- 修改前副本及 build/checks 日志：C:\Users\HENG\Documents\ChatGPT\DockForWindows\backdrop-scatter-change。
