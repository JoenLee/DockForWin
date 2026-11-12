# 玻璃实时响应专项诊断：2026-11-09

本轮完成的是帧交接延迟修复，不是最高画质高刷新率验收。200MB 限制取消，700MB 为软目标；没有增加任何内存硬限、强制 GC 或工作集修剪。

## 源码与运行核对

实际工程 D:\DockForWindows 无 .git 元数据，不能声称取得完整提交历史。本次用上一轮保存的原始源码、已应用哈希和实际文件做逐项差异检查。检查时上一轮改动涉及的文件全部匹配已应用哈希；只对匹配文件写入最小修复。原始备份位于会话目录 freshness-change/originals。

检查时运行 PID 9020 加载的是 Debug。已确认仅有 Dock 主窗口、无设置或控制中心草稿窗口，通过程序自己的 --shutdown 正常保存退出；当前 PID 14200 加载实际工程新 Release，配置仍为 D:\DockForWindows\data。运行切换没有手工改配置、显示器或电源设置。

## 诊断结论与证据

1. GlassButtonChrome 复用 Shader 与 ImageBrush：同一个 WriteableBitmap 连续变色 10 次，真实屏幕检测均成功，13.01–21.39ms。该小型隔离检查排除了“同一采样器完全不更新”；不能代表整个控制中心的高负载时延。
2. 上一轮将借用的后台像素数组改为池化独立快照，是必要的所有权修复。原数组可能在 UI 上传前被后台覆盖，不能回滚为竞争实现。
3. ControlCenterWindow.Rendering.cs 原 CaptureFrame 将完成快照放入 _pendingSample，由下一次 OnRendering 消费。该待提交机制原先已经存在；独立快照使这一等待期间的内容不再被后台偷偷覆盖。测量确认存在额外等待，但尚不能证明它解释用户观察到的全部新回归。
4. 最小修复：在 await Task.Run 返回的 UI continuation 直接 ApplySample，成功后释放上一帧，失败时释放本帧；删除待提交字段、OnRendering 消费分支和 Closed 对应释放。保留单路采样、快照所有权、逐帧像素比较、原画质、真实时间弹簧和所有交互。没有后台直接写 WPF 对象。
5. LiquidGlassEffect 常量去重没有降低 Shader 运行频率；HLSL/.ps 本轮未改。既有 20Hz chrome 刷新门槛、约 120ms 色调观察门槛、250ms 及更久的失败窗口回退缓存均在原始源码中存在，不能冒称本轮新增。回退缓存仍可能掩盖捕获失败，需要独立跟踪每个源窗口的 WGC SystemRelativeTime。
6. WindowFrameCapture.Read 在持锁期间执行 CopySubresourceRegion 后同步 Map；WPF 将全背景纹理用于多层 Shader/模糊。它们是下一步需分离计时的候选；本轮未测出可归因的 GPU 时长，未据此盲目重写。

## 同条件最高画质对照

2560×1600、180Hz、DPI 1.6041667，同一壁纸、窗口、布局和程序配置副本。基准将现有玻璃强度、扭曲、磨砂、高光、颗粒、侧暗和不透明度均置 1，高光宽度 8，衬色 .25，阴影 40/.65，Gaussian RenderingBias.Quality。仅改测试进程内副本，未保存正式配置。6 轮打开、展开、返回、关闭，末轮快速反向。两次顺序测量用于定位，非多轮统计置信结论。

| 指标 | 修复前 | 立即上传候选 |
|---|---:|---:|
| 快照就绪至上传开始：均值（ms） | 4.93 | 0.44 |
| 快照就绪至上传开始：P95（ms） | 10.18 | 0.68 |
| 快照就绪至上传开始：P99（ms） | 26.47 | 5.88 |
| WPF 回调频率（次/s，非真实 FPS） | 29.37 | 31.54 |
| WPF 回调平均间隔（ms） | 34.04 | 31.71 |
| WPF 回调 P95 间隔（ms） | 89.57 | 86.82 |
| WPF 回调 P99 间隔（ms） | 105.71 | 102.33 |
| WPF 回调最大间隔（ms） | 183.99 | 169.91 |
| 采样线程耗时均值（ms） | 7.90 | 7.79 |
| 上传及材质更新均值（ms） | 0.75 | 0.45 |
| 进程 CPU（整机逻辑核归一化 %） | 7.09 | 6.85 |
| Working Set 峰值（MiB） | 390.58 | 383.20 |
| Private Bytes 峰值（MiB） | 829.44 | 718.55 |

采样就绪时间戳取在独立快照构造开始，含很短的租用/复制时间，不等于 WGC 源画面时间。立即上传减少这一等待，不代表 GPU 队列已清空。最大回调间隔仍达 169.91ms，卡顿峰值没有解决。

原始 JSON：rendering-lab/results-regression-pending-1/report.json 与 results-regression-immediate-1/report.json。gpu.csv 为整块 NVIDIA GPU 的占用/显存，不能归因应用，也不能与进程 Private Bytes 相加。真实 FPS、应用 GPU 帧时间、DWM 队列深度、输入到光子延迟仍未可靠取得；PresentMon 对该透明 WPF 窗口没有有效应用呈现记录。不得用上述回调频率替代 FPS。

## 动态背景到实际屏幕玻璃响应

新测试 --verify-glass-response：完整最高材质、纯红/蓝背景轮换，分别读取共享 WriteableBitmap 与实际屏幕玻璃像素；每组静止 4 次、展开/返回 4 次。表中每格为“检测到上传 / 检测到玻璃屏幕颜色”的平均 ms。计时起点是背景属性改变，含测试背景自身的 WPF 呈现和 WGC 采集，并非硬件输入到光子。5ms 请求轮询受 UI 忙碌影响；样本少，数值只作诊断。

| 版本 | 静止：上传 / 屏幕 | 展开返回：上传 / 屏幕 |
|---|---:|---:|
| 优化前源码快照 | 133.81 / 260.59 | 176.31 / 267.20 |
| 本次修复前 | 139.19 / 258.90 | 186.28 / 286.33 |
| 隔离候选立即上传 | 131.25 / 229.64 | 107.69 / 238.39 |
| 实际源码修复后 | 107.84 / 227.13 | 121.17 / 255.46 |

优化前快照本身也有较长屏幕延迟。因此“本轮新增了全部玻璃延迟”的归因没有得到证明。立即上传有实测改善，当前绝对延迟仍未达到目标。最初 response-*.log（无 max 前缀）采用默认材质，仅作排查，不能充当最高画质验收。

实际源码另完成 4 次窗口移动伴随背景变化的屏幕检测，全部跟随成功；同目录 response-max-source-fixed.log 保留逐次数据。它不等于连续拖动录像。滑块实际鼠标拖动与材质同步、源画面准确时间戳、长时间稳定性、严格逐帧像素一致性尚未完成。Shader/材质参数未改，功能性变色测试不能替代严格视觉验收。

## 验证及边界

- 工程 Build.ps1 Release 构建通过，0 警告/错误。
- 新动态背景/展开返回/窗口移动共 12 次真实屏幕检测通过。
- 更新生命周期测试删除对已移除字段的反射访问后，快速反向、排序、形变中关闭、父级恢复、帧租约释放及关闭后停更通过。
- 测试项目构建保留原有 GuiChecks 空值警告和 NuGet 漏洞缓存访问警告；没有冒充漏洞检查通过。
- 未改 Ionicons、点击逻辑、布局、弹簧曲线、材质参数、采样分辨率或采样品质；未直接回滚上一轮成果。
- 必要检查限定为新响应检查和受影响的生命周期检查。早期一次测试启动与构建重叠，误启动旧 DLL 的默认 69 项检查；这些结果不计入本轮验收，之后严格串行构建与执行。性能验收未通过，不能进入下一阶段内存/代码优化并称其完成。

参考用户指定资料：[animation-performance](https://github.com/emilkowalski/skills/blob/main/skills/animation-performance/SKILL.md)、[ui-animation](https://github.com/mblode/agent-skills/blob/main/skills/ui-animation/SKILL.md)、[motion-animation](https://github.com/s1gmamale1/apple-design-skills/blob/main/skills/apple-design-motion/references/motion-animation.md)。采用先定位开销、保留可中断速度衔接的原则；网页 CSS/WAAPI 机制不直接移植进 WPF。
