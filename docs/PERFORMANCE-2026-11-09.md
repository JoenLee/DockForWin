# 2026-11-09 动画与最高画质性能记录

实际源码：D:\DockForWindows。原有布局、功能、折射、Gaussian 模糊、高光和阴影路径保留。没有修改用户 data、安装包、电源计划、显示刷新率或 VSync。

## 已保留的改动

- 解析弹簧仅在静止起步的最多 24ms、结尾最多 45ms 做局部 C2 修正。原时长和 6%–90% 中段完全不变，继承速度的反向不重置起步。30 组数值检查通过，最大位移差 0.4244%。
- 背景帧使用有明确所有权的池化快照。被替换、已显示、关闭中完成的帧均归还；变化比较只对最后实际显示帧执行。修复后台复用数组与 UI 上传之间的竞争。
- 按钮复用两个 ShaderEffect、输入画刷和稳定轮廓；相同 Shader 常量不重复提交。相同外观的样式只在当前 UI 线程保留一份，不跨线程共享。
- 关闭时恢复共享元素的真实父级；有独立解析移动路径时跳过会立即被覆盖的 X/Y 弹簧计算。
- 调试注释明确区分 WPF 回调与 GPU 呈现。

## 条件与口径

RTX 5060 Laptop GPU；Windows 10.0.26200；.NET 10.0.14；2560×1600、180Hz、DPI 160.42%；相同用户布局及固定渐变背景窗口。180Hz 预算约 5.56ms。

测试内存配置：Frost/Intensity/Distortion/Highlight/SideDarkness/Opacity/Grain=1，HighlightWidth=8，Tint=.25，ShadowBlur=40，ShadowOpacity=.65，背景 Gaussian 使用 RenderingBias.Quality。设置未写入正式配置。Grain 在原有 HLSL 中仍为占位参数，不宣称已有可见噪声。

每组预热后重复 6 次弹出、Wi-Fi 展开、返回、收起，并做快速反向。连接测试使用替身，正常系统状态读取、媒体轮询仍运行。没有真实无线写入或伪造产品状态。

下面是 CompositionTarget.Rendering **回调**数据，不是 GPU 实际 FPS；首个可见状态回调也不是 Input-to-Photon。构造计时独立记录。不同进程顺序采样，存在后台负载、JIT、驱动资源缓存与调度波动；单轮结果不能证明统计显著收益。

| 指标 | A 修改前完整玻璃 | B 临时关闭玻璃 | C 最终保留方案 |
| --- | ---: | ---: | ---: |
| 回调频率（次/秒） | 29.53 | 91.56 | 30.72 |
| 平均回调间隔（ms） | 33.87 | 10.92 | 32.56 |
| P95（ms） | 95.27 | 19.08 | 88.69 |
| P99（ms） | 105.95 | 23.84 | 100.19 |
| 长间隔次数（>1.6×预算） | 301 | 1040 | 323 |
| 首个回调（ms） | 11.43 | 11.49 | 10.30 |
| 窗口构造均值（ms） | 14.94 | 13.42 | 8.67 |
| Show 到可见状态回调（ms） | 263.95 | 245.72 | 225.65 |
| 构造加可见状态回调（ms） | 278.89 | 259.14 | 234.32 |
| 采集墙钟均值（ms） | 11.58 | 12.12 | 8.81 |
| 采样应用墙钟均值（ms） | 0.35 | 0.18 | 0.70 |
| 进程 CPU 总时间（ms） | 28781.25 | 26921.88 | 27031.25 |
| 托管累计分配（MiB） | 251.1 | 348.1 | 230.1 |
| 峰值 Working Set（MiB） | 402.2 | 346.2 | 391.8 |
| 峰值 Private Commit（MiB） | 733.5 | 463.0 | 795.9 |

C 的回调频率变化约 4.02%，P99 变化 -5.44%，累计托管分配变化 -8.36%。平均速率仍远低于屏幕刷新率；没有达到稳定高帧率目标。

## 内存与资源

Working Set 是驻留物理内存；Private Bytes/Private Commit 是进程私有提交量，表中峰值取 Windows PeakPagefileUsage；托管分配是累计分配流量，不是活对象占用。它们不能混用。GPU 显存、共享 GPU 内存和系统总提交量不能与这些列直接相加。

NVIDIA 采样 CSV 记录的是整张显卡的利用率与显存，包含 DWM、其他程序及原运行实例，不能当作本程序的独占 GPU 数据。没有调用 GC.Collect、EmptyWorkingSet 或工作集修剪。

实际工程 Release 测试程序连续运行 420.1 秒：完成 22142 次采样；最后一分钟平均 Working Set 285.7MiB，Private Commit 579.1MiB。峰值分别 297.7MiB、707.0MiB。活动导航回调 P95/P99 为 96.76/104.54ms，此场景与上表混合弹出场景不同，不直接比较频率。

200MB 挑战目标未达到。90 秒内存候选对照中，平均工作集 269→276MiB、私有提交量 557→575MiB；因此回退了该候选，没有用分配减少冒充稳定占用降低。

## 拒绝合入的实验

- HLSL 早退：24 组真实 GPU 截屏对比像素差为 0，但 fxc 实际生成 4 次纹理读取/265 指令槽，原版为 3 次/247；未减少 GPU 工作，已回退。最终 HLSL/.ps 保持原版。
- DrawingImage 局部采样、WIC 局部纹理：未通过严格画面或帧尾检查，回退；不以裁剪引入的采样差异换内存。
- 不透明遮挡剔除、非阻塞位图上传：功能检查通过，但对照数据出现 P99/峰值内存回退，未合入。
- 原生缓存直接接管位图、直接写入调用方缓冲：所有权检查通过，稳定内存未改善，未合入。最终保留独立池化快照的简单方案。

## 检查与未验收项

通过：应用 Release 构建；30 组曲线数值；帧所有权与缓冲复用；两通道材质复用；四主题与 Dock 设置/取消预览；控制中心页面/播放器进度/长标题/拖动检查；排序收敛、快速反向、形变中关闭的父级恢复。只在源码变化或新问题出现时追加相关检查。

测试构建有原 GuiChecks.cs:98 可空警告，以及 NuGet 漏洞缓存访问警告；应用构建无错误、无警告。

管理员 ETW 已采集并保留原始记录与 CPU 模块汇总。PresentMon 未取得有效应用呈现样本；WPA 导出只有 Unknown MemoryTransfer 等不可归因数据，且出现 Duplicate start TraceLogging activity。GPU Frame Time、真实显示 FPS、应用 Present Latency、Input-to-Photon、精确 DWM 分摊均未可靠测得，不填估算值。

未完成：200MB、接近 180Hz 的验收；多小时稳定性、混合显示器 DPI 与全部真实硬件功能的验收。当前改动改善正确性、资源复用和部分回调尾延迟，不宣称完成所有性能目标。

## 证据

同目录 rendering-lab 下：results-final-A、results-final-B、results-accepted-C、accepted-steady、final-sweep.log、four-themes.log、shader-equivalence、shader-A.asm、shader-C.asm。早期试验的 C 标签不是最终交付版本，以 results-accepted-C 为准。

实际源文件已经哈希校验后合入 D:\DockForWindows。accepted-change/originals 与 rendering-change/originals 保留两轮原件。未打安装包。当前运行实例的更新状态另行记录，不能从 Release 构建成功推断。

运行核对：旧实例正常退出；当前 PID 39668，从 D:\DockForWindows\src\DockForWindows\bin\Release\net10.0-windows\DockForWindows.dll 启动，数据目录 D:\DockForWindows\data。没有强制结束进程。正式 HLSL/.ps 与原版哈希一致。conditions.json 已核对实际生效材质、几何、模块及页面一致；原始控制中心材质槽虽变化，OverrideMaterial=false，未参与测试渲染。
