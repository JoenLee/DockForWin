# APP 设置双栏界面全面接入

用户已确认样板方向，本轮将新版界面接入正式 `SettingsWindow`。实际源码位于 `D:\DockForWindows`。工程没有 `.git`；本轮依据工作区 `settings-full-change/originals` 的修改前副本比较，不能声称完整 Git 历史对照。

## 完成范围

- 保留外观与主题、图标与倒影、动画与交互、应用与文件、位置与隐藏、系统与启动、界面主题、控制中心全部八类设置。
- 固定 220 DIP 侧栏、搜索、移动选中高亮、返回/前进和 Windows 原生最小化/最大化/关闭。普通切页仍为 200ms/8 DIP，二级转场为 270ms/20 DIP，使用 Opacity 与 RenderTransform。
- 每类首组设置直接展示，后续功能组进入缓存二级页。原有 44 个滑块、3 个颜色编辑器、全部模块开关及其他选项继续使用原控件事件和草稿对象。三个短选项组改用原生分段选择器，双向同步原 ComboBox 的选中值；其余菜单保持原生 ComboBox。
- 原第三栏预览移至底部“预览”入口，可返回原位置；不同来源共用一个壁纸预览控件。未显示的示例不更新图片/图标，打开后按当前草稿恢复。桌面实时预览、控制中心实时预览、导入、添加应用/文件夹、模块排序/页面/名称/图标/分组及形状功能继续走原实现。
- 统一设置窗口内的开关、分段、按钮、下拉、滑块和分组容器。四套原界面配色及浅/深色模式均保留；样式只作用于设置窗口，不合并或修改全局玻璃控件模板。
- 主题、模块等操作重建设置窗口时保留当前二级页、返回/前进历史、滚动位置、窗口尺寸及未保存草稿。修复历史中同一路由多次出现时，恢复误跳到最后一次记录的问题。
- 原有 Copy/Normalize、颜色校验、SettingsService 保存、失败处理和取消恢复链路保留。测试使用隔离目录，未写正式 `data`。独立预览中的开机自启开关禁用，避免预览意外修改系统注册。

## 核心文件

| 文件（相对于实际源码根目录） | 作用 |
| --- | --- |
| src/DockForWindows/Views/SettingsWindow.cs | 正式设置继承已确认外壳；原功能控件、回调和保存逻辑接入新排版 |
| src/DockForWindows/Views/SettingsWindow.ShellLayout.cs | 功能分组、缓存路由、控件索引与重建时滚动/历史恢复 |
| src/DockForWindows/Views/SettingsWindow.ShellPreview.cs | 打开共用预览页 |
| src/DockForWindows/Views/SettingsWindow.ControlCenter.cs | 仅五处按钮创建语句更换为设置局部模板；回调/系统逻辑未改 |
| src/DockForWindows/Views/SettingsShellWindow.cs / SettingsShellWindow.Pages.cs | 复用双栏外壳；保留原独立样板构造入口 |
| src/DockForWindows/Views/SettingsShellWindow.Palette.cs | 四配色与浅/深色的局部资源映射 |
| src/DockForWindows/Views/SettingsShellStyles.xaml / SettingsSwitchMotion.cs | 设置窗口内控件样式与开关颜色 |
| src/DockForWindows/Views/SettingsPageHost.cs | 精确恢复重复历史记录位置，缓存计数按实际页面去重 |
| src/DockForWindows/Views/DockWindow.cs | 仅 ReopenSettings 增加导航/滚动恢复 |
| tests/DockForWindows.Tests/FullSettingsChecks.cs / Program.cs | 完整设置及重建的隔离验证、独立完整界面预览入口 |
| tests/DockForWindows.Tests/SettingsPreviewChecks.cs / RedesignChecks.cs | 原测试适配缓存二级页；保留预览与 Dock 检查 |
| tests/DockForWindows.Tests/FourThemeChecks.cs | 设置检查转到完整新版检查；共享玻璃按钮的 CheckButtonOptics 原样保留 |
| tools/Preview-SettingsUI.ps1 | 原独立样板启动器改为打开完整设置，使用隔离配置 |

## 已执行验证

1. `tools/Build.ps1 -Configuration Release`：成功，0 警告、0 错误。测试项目构建成功；已有 GuiChecks.cs:98 的两条可空警告仍在，未顺带改写。
2. `--verify-full-settings <目录>`：31 项通过。覆盖完整类别/控件可达性、草稿深拷贝、分段同步、下拉打开、玻璃参数细调、继承/覆盖启用状态、模块开关、无效颜色阻止保存、80 次快速切页、重复历史恢复、最小窗口、四配色×浅深模式及实际隔离保存。
3. `--verify-settings-rebuild <目录>`：8 项通过。实际主题回调重建、二级路由/尺寸/滚动恢复、草稿保留、壁纸预览返回、取消丢弃草稿以及未写配置。
4. `--verify-settings-preview <目录>`：7 项通过。实际控制中心预览打开、复用同一预览窗口、未保存几何同步、两次保存、取消恢复最后保存、关闭设置。未更改系统开机启动注册。
5. `--verify-settings-shell <目录>`：22 项通过。受影响的外壳、开关、键盘、搜索、快速导航和窗口按钮检查；没有重复旧整套玻璃性能测试。
6. `--verify-redesign <目录>`：23 项通过。旧设置检查适配双栏/缓存页后，验证壁纸材质、玻璃/倒影启用条件、实时 Dock 预览、深色重建，以及原左右侧 Dock 缩放与居中行为。
7. 49 个受保护文件（全部 Services、Shaders 和悬浮控制中心/共享玻璃相关 Views）修改前后 SHA256 一致。主设置功能控件创建及回调代码块，归一化换行和按钮工厂名称后与修改前完全一致；控制中心设置 partial 仅按钮工厂名称变化。

本机 DPI 为 160.42%。已检查 1040×780、820×580 和 760×540 DIP 的实机截图、模块长列表、二级滚动、下拉和深色主题。

完整检查进程末尾采样：Working Set 225.12 MiB，Private Bytes 213.06 MiB；缓存页面 12、活动页面 1。这是隔离设置测试进程的瞬时读数，包含多套测试窗口创建留下的运行时资源；没有强制 GC。不能据此代表完整 Dock+控制中心的稳定/峰值内存、GPU 显存或 700MB 验收结果。

## 运行、证据与回退

- 正式 `Open-Settings.cmd` 仍走原主程序入口，下一次正常启动后使用完整新版设置。当前 Debug PID9236 的设置窗口打开，为保留用户草稿未关闭、未重启；不能声称其已加载新版。
- `Open-Settings-UI-Preview.cmd` 现在打开完整设置的独立预览，隔离配置位于 `docs/settings-full-preview/isolated-data`。按钮“保存设置”仅保存此目录，不是正式用户配置。测试中禁止开机启动开关修改系统注册。
- 工作区 `settings-full-change/verification/result.json`、各验证目录、`shell.log`、`rebuild.log`、`live-preview.log`、`redesign.log`、`source-preservation.json`、`protected-hashes.json` 为本轮证据。`originals` 及额外 `*.original.*` 是本轮改动前的副本，不自动清理。
- 需要回退时先核对当前哈希与用户后续改动，仅恢复本轮明确修改的文件；不能覆盖整个工程，也不能删除未提交文件。

## 尚未验收

跨显示器真实 DPI 切换、高对比/完整读屏、长期内存曲线与所有硬件系统操作未全面验收。未测实际屏幕呈现 FPS、GPU 帧时间或新的玻璃背景延迟数据；此次不宣称解决此前的高刷新率性能问题。侧栏沿用已确认的低成本渐变层次，不额外采集实时桌面背景。

## 2026-11-10 用户追加调整

- 按用户截图移除设置窗口中侧栏、按钮、开关、分段、下拉及滑块的蓝色焦点描边和默认焦点装饰。保留键盘焦点、点击、侧栏选中底色、分段指示块与滑块进度色。TextBox 使用保留 PART_ContentHost 的圆角模板，焦点不再改变边框；颜色校验的错误边框继续保留。
- `--verify-settings-focus` 的 7 项实际焦点检查通过，搜索输入仍正常。`focus-verification/no-focus-border.png` 为焦点停在第一分类时的实机截图。
- 侧栏八个图标换为官方 Ionicons **v8.0.0 Filled**：contrast、grid、sparkles、folder、desktop、settings、color-palette、options。来源 https://github.com/ionic-team/ionicons/tree/v8.0.0/src/svg 。原 SVG 和 MIT 许可保存于 `Assets/Ionicons`，XAML DrawingImage 使用原路径、Nonzero 填充规则及 512×512 视区/裁切；只将显示颜色设为白色。保持原彩色底块、尺寸、文字与导航布局。
- 原 SVG 离线转换为冻结的 WPF 矢量资源，不新增 SVG 运行时依赖或网络请求。未修改共享 NavigationGlyphs、ControlCenterIcon 或悬浮控制中心的图标资源。
- `--verify-settings-icons` 的 9 项检查通过，八个矢量资源冻结、视区及 16 DIP 尺寸正确。实机截图 `icons-verification/ionicons-filled.png` 已检查。
- 测试默认输出被独立预览 PID48008 锁定，未关闭该窗口或丢弃其草稿，改用工作区 `settings-full-change/focus-build` 隔离输出构建并验证。正式 Release 构建成功；旧运行窗口仍须正常重开才会加载新版。没有为焦点/图标修改重复完整性能测试。
