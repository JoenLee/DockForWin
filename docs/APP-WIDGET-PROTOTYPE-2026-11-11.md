# APP 外壳与天气 / 日历小组件原型

> 最新视觉修正：按用户图二改为小连续圆角，轮播内滚轮不再滚动页面，卡片边缘统一约22 DIP间距。71项定向检查通过；详见 docs/APP-WIDGET-CORNERS-2026-11-11.md。该说明优先于下文完整超椭圆及边界滚轮冒泡的旧设计。

本轮已写入实际源码 `D:\DockForWindows`。正式 PID 10604 保留原实例，未被重启或覆盖其加载的 Release；新版本以独立输出验证。没有增加其他类别或真正的桌面放置。

## 最小改造与文件清单

沿用现有 SettingsShellWindow、SettingsPageHost、SettingsWindow 草稿与保存链路。顶部搜索、柔和灰绿侧栏、内容卡片和 Dashboard 引导区是 APP 专用改动。红黄绿按钮方法、模板、颜色、尺寸、排列及交互代码保持原样。

侧栏使用轻量半透明色层，不启动额外背景捕获。最终整理还将数据来源链接统一为青绿色。这两处静态配色整理在行为检查和资源采样之后完成，并在独立预览构建中检查；上表资源数据对应整理前同一轮播实现，不能据此推断最终版本精确帧时。

修改的既有应用文件只有 5 个：

- `Views/SettingsShellWindow.cs`：顶部全局搜索、青绿色聚焦反馈；搜索支持天气、日历、日程等入口，原有设置关键词仍参与匹配。
- `Views/SettingsShellWindow.Overview.cs`：注册图库页、Dashboard 引导卡与两个原型的真实摘要。
- `Views/SettingsShellWindow.Palette.cs`、`Views/SettingsShellStyles.xaml`：APP 局部背景与卡片层次。
- `Models/DockModels.cs`：Widgets 参与 Normalize、Copy、ApplyPreferences，仍由 SettingsService 保存。

新增应用文件：

| 文件 | 职责 |
| --- | --- |
| `Models/WidgetOptions.cs` | 配置、5 种网格规格、8 个定义、注册表与实例工厂、放置请求接口及拖拽数据 |
| `Services/WidgetWeatherService.cs` | 共用 HttpClient、城市查询、天气 / 五日预报解析与数据校验、取消与超时 |
| `Views/WidgetHost.cs` | 冻结几何与画刷缓存、三材质、独立轮廓阴影、共用缓存模糊背景、Filled 图标 |
| `Views/WidgetCarousel.cs` | 通用有界轮播、复用 ControlMotion、滚轮 / 拖动 / 阻尼 / 分页 / 键盘 |
| `Views/WidgetContent.cs` | 随规格变化的天气信息密度、真实日期 / 月历 / 日程布局 |
| `Views/WidgetGalleryPage.cs` | 单个纵向页面、两个独立分类、城市和本应用日程编辑、页面生命周期 |

另增 5 个官方 Ionicons v8 Filled SVG（calendar、cloud、partly-sunny、rainy、snow），沿用已有 MIT 许可。既有控制中心图标加载器未修改。

测试：新增 `tests/DockForWindows.Tests/WidgetGalleryChecks.cs`；Program 增加验证、实网天气、性能与预览入口；FullSettingsChecks 只更新已被本轮替换的占位摘要断言。

入口：`Open-Widget-Gallery-Preview.cmd`、`tools/Preview-WidgetGallery.ps1`。本报告中的 Views / Models / Services 相对 `src/DockForWindows`。

## 可运行范围

- 天气：城市搜索与地区选择，真实当前模型天气、描述、最高最低温、五日预报，手动刷新。无城市时显示空状态，网络失败明确反馈；保留上次数据时显示获取时间。未使用自动定位。
- 日历：真实系统日期、星期、一周从周一开始的完整月历、前后月 / 今天、按日期查看本应用日程。可添加、移除日程，校验日期与 HH:mm；更改随窗口底部保存设置提交。未接入 Windows / 云日历。
- 两类始终在同一个纵向页面，每类独立横向轮播、固定位置的分页与添加按钮。按钮当前禁用并说明桌面放置待开发。
- 注册表各有 4 种内容布局；统一单位 84 DIP、间隙 12 DIP，规格为 84×84、180×84、180×180、372×84、372×180 DIP。内部内容按尺寸安排。
- Light / Dark / Glass 共用内容树。Glass 使用一张取自现有 PreviewWallpaper 的缓存模糊背景，是图库材质预览；没有启动额外的 WGC、读回或实时折射采集。

## 轮廓、交互和资源

完整曲线满足 `|x/a|^n + |y/b|^n = 1`，默认 n=2.5，可选 3。512 个采样点生成一次 StreamGeometry 并冻结，按规格与指数缓存；底板、边缘、内容裁剪与阴影引用同一 Geometry。宽条也按完整数学超椭圆绘制。动画仅变换已有视觉对象。

每个轮播只有一个 WPF 位置动画，复用 ControlMotion 的速度衔接和解析弹簧，同步驱动 TranslateTransform、ScaleTransform、Opacity 与指示器变换。没有新增 CompositionTarget.Rendering 订阅。快速滚轮直接改变当前目标；首尾不循环，向外滚轮交还外层页面；拖动饱和阻尼约 0.22 个间距，释放吸附，支持捕获丢失与动画中按下。

图库只用一个分钟级计时器，处理日期切换及按需天气刷新；离页停止并取消请求，返回恢复；屏幕外分类和最小化窗口不发起周期天气请求。静态阴影缓存，画刷和几何复用，文本不参与背景模糊。未进行桌面吸附、窗口管理或完整桌面放置系统开发。

## 验证与证据

所有证据位于 `C:\Users\HENG\Documents\ChatGPT\DockForWindows\widget-gallery-change`。

- 隔离 Release 构建通过：应用没有新增警告，测试仍有 `GuiChecks.cs:98` 两项既存 nullable 警告。`release-ready` 是已验证输出，正式进程仍使用原二进制。
- 新定向检查 **66 项通过**：`checks-release.log`，`checks-release/result.json`。覆盖数学方程 / 缓存、深拷贝、数据拒绝 / 取消、两个独立轮播、真实鼠标拖动 / 反向 / 吸附 / 捕获丢失、边界滚轮实际冒泡、分页、三材质、月历与日程、隔离保存、启动项保留、最小宽度、离页 / 关闭停更等。
- 既有完整设置 **40 项通过**：`settings-regression.log`。原滑块 / 颜色 / 模块回调、二级页面、保存、主题重建与快速导航保留。
- 公开接口实网查询成功：`weather-live.log`、`live-weather/live-weather.json`。当时北京返回 7.1°C、当地模型时间 2026-11-11 20:00 和五日预报；这不是设备本地实测温度。
- 已查看实际 WPF 截图：天气 Light / Dark / Glass、日历与日程、Dashboard、最小窗口。本机 DPI 154/96 ≈ 160.42%；多显示器和不同真实 DPI 仍未验收。
- 144 个既有应用源码 / 资源中，139 个哈希不变，仅上述 5 个预期文件修改；`source-protection.json` 无意外变化。红黄绿按钮方法与完整样式逐字一致。Dock、控制中心、捕获与 Shader 文件未改。

首次检查的关闭断言没有等待 WPF Unloaded 派发，记录在 `checks.log`；后续等待卸载事件后通过。其他历史日志与原件均保留。

## 性能测量及限制

`measurement-no-etw/scenarios.json`：隔离 APP 窗口中，每种材质 100 次、每 150ms 重定向一次，共约 15.8 秒。未调用强制 GC、工作集修剪或降低显示器刷新率。CPU 为整个目标进程占机器总算力百分比，UI 为单个 UI 线程占一核百分比；内存为场景结束快照，不是峰值。

| 材质 | 平均 CPU | UI 线程占一核 | Working Set | Private Bytes |
| --- | ---: | ---: | ---: | ---: |
| Light | 2.71% | 3.17% | 175.74 MiB | 197.56 MiB |
| Dark | 2.25% | 3.07% | 192.51 MiB | 196.83 MiB |
| Glass | 2.24% | 1.88% | 225.21 MiB | 221.12 MiB |

这组场景没有前版同负载对照，不能推导性能提升，也不能证明长时间稳定性或多分类扩展后的开销。

实际呈现 FPS、平均呈现帧时间、P95 / P99 和 GPU 占用**未验证**。已核对 SHA256 的官方便携 PresentMon 2.6.0 在普通权限下返回 ETW access denied；一次 UAC 请求未完成，隔离目标等待 60 秒后退出，随后撤销本任务的过期提升调用者（未结束正式程序或 consent.exe）。证据 `present-permission-probe.log`、`measurement/`。没有获得有效 ETW。GPU Engine 查询返回无效计数器样本，见 `measurement-no-etw/gpu-error.log`；不报告虚构数字。未采用 WPF 回调率充当 FPS。

下一步应先在获准的 ETW 采集环境中完成呈现指标与更长时间的资源检查，再扩展其余类别。原控制中心历史鼠标捕获失败未在本轮修复；本轮新轮播的捕获通过不能覆盖它。

## 隔离预览与一次已恢复的测试副作用

双击工程根目录 `Open-Widget-Gallery-Preview.cmd`。它按本地 SDK / NuGet 和 Shader 准备流程构建到 `docs/widget-gallery-preview/build`，打开原生 WPF 窗口，不替换正在运行的正式 DLL。预览设置只保存在 `docs/widget-gallery-preview/isolated-data`；开机启动开关禁用。预览窗口关闭后可再次构建 / 打开，已保存的预览小组件配置会重新读取。

首轮隔离保存测试遗漏将 StartWithWindows 初始化为现有注册状态，误清除了真实启动项。发现后依据正式 data 中 StartWithWindows=true、PID 10604 的正式启动命令及现有 BuildCommand 规则，恢复为本地 dotnet + 正式 Release DLL + 原 data 的注册。记录 `startup-restored.json`。测试已修正，最新检查确认隔离保存前后注册字符串完全一致；未修改正式 data。该副作用已处理，不隐瞒为“系统从未变更”。

## 设计参考

- [Apple HIG Widgets](https://developer.apple.com/design/human-interface-guidelines/widgets)：清晰的信息层次与随尺寸调整的信息密度。
- [Material 3 Carousel](https://m3.material.io/components/carousel/overview)、[Motion](https://m3.material.io/styles/motion/overview)：聚焦与连续运动参考；未引入对应运行时。
- [WPF UI](https://github.com/lepoco/wpfui)、[HandyControl](https://github.com/HandyOrg/HandyControl)：仅查看原生控件组织方式，未增加依赖。
- [Ionicons](https://ionic.io/ionicons)：官方 Filled SVG。
- [Open-Meteo 天气 API](https://open-meteo.com/en/docs)、[城市查询](https://open-meteo.com/en/docs/geocoding-api)：真实数据接口与字段。

Dribbble、Uiverse、Rainmeter 分类页已浏览；Figma Community 抓取被 robots 拒绝。用户提及的旧图与草图未随本次消息提供，原型按本次文字结构及已有 APP 实现推进。
