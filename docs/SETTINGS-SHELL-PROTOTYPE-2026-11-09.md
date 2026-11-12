# 设置窗口双栏样板与验收记录

## 当前交付范围

本轮完成可运行、可切页、可返回的 WPF 设置窗口样板，供用户确认设计后再推广。没有替换默认设置窗口，没有重写原有全部设置页，没有迁移技术栈。原设置窗口增加“新版界面预览”入口；旧设置的全部功能仍保留。

已读取用户的 Word《关于界面的UI和动画设计.docx》和 macOS 设置参考截图。Word 中的 CSS 只作为控件尺寸、颜色和运动参考，没有执行文档中的代码，也没有将 HTML/WebView 引入程序。

## 运行方式

- 独立样板：双击 D:\DockForWindows\Open-Settings-UI-Preview.cmd。脚本使用项目本地 SDK/NuGet，编译后打开单独的预览宿主，不启动第二个 Dock，也不接管任务栏。
- 独立样板使用默认测试草稿；“完成预览”只将该草稿写入 docs/settings-shell-preview/preview-draft.json，关闭则结束。不会修改正式 data 或系统设置。
- 应用内入口：加载新构建后，在原设置底部点击“新版界面预览”。该窗口是原设置的模态子窗口，使用当前未保存草稿的深拷贝；取消不影响原草稿。“检查并保存…”或“查看全部设置…”将副本交回原设置对应分类，再经原来的验证/保存/回滚流程提交。
- 当前独立预览进程启动时 PID46224；原应用现场核对为 PID9236、Debug。没有关闭或重启原应用；PID随用户操作可能变化，应现场核对。

## 窗口与控件

- 固定220 DIP浅色侧栏，分类图标、搜索、滚动、键盘方向键和圆角选中指示块。
- 右侧标题与返回/前进、分组容器、细分割线、统一排版、固定操作区；缩小时内容滚动，页脚按钮保留。
- 红/黄/绿按钮分别执行原生关闭/最小化/最大化或还原。WindowChrome保留标题拖动、双击与边缘缩放；Windows11启用系统圆角。窗口没有 AllowsTransparency，没有额外WGC背景采集。
- 侧栏采用轻微灰蓝渐变表达层次，本阶段没有新增实时侧栏模糊或玻璃折射。壁纸仅用既有静态图片作缩略图。
- 开关为51×31 DIP轨道、27 DIP白色滑块，绿色开启、浅灰关闭；按下用缩放拉伸，移动用TranslateTransform。使用原生CheckBox语义。
- 分段选择器为原生ListBox派生控件，灰色轨道、白色带轻阴影的指示块；宽度由选项数计算，切换只动画位移，支持键盘选择。
- 统一局部XAML样式，不修改全局IosStyles或控制中心控件。字体优先Noto Sans SC，回退至Microsoft YaHei UI和Segoe UI，不安装字体。

## 现有页面盘点与样板映射

| 原分类 | 原有功能保留 | 样板已接入草稿 |
|---|---|---|
| 外观与主题 | 主题/预设、颜色、透明度、玻璃全部参数、边框、阴影、运行点、恢复默认 | 主题/透明度；材质二级页的圆润、折射、磨砂、扭曲、阴影开关/强度 |
| 图标与倒影 | 图标尺寸/间距、倒影开关/强度/长度/间隔/模糊及可用条件 | 尺寸/间距/倒影开关 |
| 动画与交互 | 动画开关、波浪倍率/上浮、弹跳高度/时长 | 开关及独立参数二级页 |
| 应用与文件 | 应用/文件/文件夹添加、运行/最近应用、最近数量 | 运行/最近开关及数量，原管理页入口 |
| 位置与隐藏 | 边缘、显示器、距离、桌面/普通窗口/全屏隐藏 | 边缘、距离与三类隐藏开关 |
| 系统与启动 | 原启动项读取、草稿、保存、失败回滚 | 开机自启草稿开关 |
| 界面主题 | 四套配色、跟随系统/浅色/深色 | 三段外观选择和原完整配色入口；样板自身仍固定浅色 |
| 控制中心 | 原入口/热键/导入、模块/页面/图标/布局、配色、材质覆盖、尺寸圆角 | 启用草稿、面板尺寸/组件缩放/间距二级页与原完整设置入口 |

不在样板中的项目仍能通过原设置访问；没有伪造系统功能状态。全部页面迁移、深色主题与真正系统侧栏材质等待设计确认后再做。

## 统一导航与生命周期

SettingsPageHost集中管理11个已注册路由、懒创建缓存、返回/前进历史（最多64条）。一级切换200ms/8 DIP；二级进入返回270ms/20 DIP；Cubic EaseOut。只动画Opacity和RenderTransform，不每帧创建页面，不注册CompositionTarget.Rendering。

快速操作使用当前变换/透明度衔接，丢弃中间页时保留其透明度权重，避免反复切换导致整个内容越来越透明。任一时刻最多两个活动页面；缓存ScrollViewer保持滚动与控件状态，关闭清理局部动画。Windows减少动画/高对比状态下不播放页面位移动画。

原SettingsWindow接收非空草稿时，不再用当前开机启动注册状态覆盖该草稿字段。普通首次打开仍读取真实注册状态；StartupRegistration API、提交逻辑及系统功能代码均未修改。

## 文件

- src/DockForWindows/Views/SettingsPageHost.cs
- src/DockForWindows/Views/SettingsSegmentedControl.cs
- src/DockForWindows/Views/SettingsShellStyles.xaml
- src/DockForWindows/Views/SettingsShellWindow.cs
- src/DockForWindows/Views/SettingsShellWindow.Pages.cs
- src/DockForWindows/Views/SettingsSwitchMotion.cs
- src/DockForWindows/Views/SettingsWindow.ShellPreview.cs
- src/DockForWindows/Views/SettingsWindow.cs
- tests/DockForWindows.Tests/SettingsShellChecks.cs
- tests/DockForWindows.Tests/Program.cs
- tools/Preview-SettingsUI.ps1
- Open-Settings-UI-Preview.cmd

原SettingsWindow.cs备份位于会话settings-shell-change/originals。实际已应用哈希在settings-shell-change/applied-hashes.json，回退前须比对，不能覆盖用户后续修改。实际工程没有Git元数据，不声称存在Git提交备份。

## 验证

- tools/Build.ps1 Release构建通过，0警告/0错误；独立启动器端到端运行成功。
- 新样板定向检查22项通过，另执行80次连续切页并检查双页上限及非空白状态。
- 34个受保护的悬浮控制中心、共用玻璃/Shader/捕获/动画相关源码文件哈希全部不变。没有重跑旧整套玻璃/控制中心性能测试。
- 本机实际DPI 160.42%；980×740和780×570 DIP窗口检查，最小化/最大化/还原、键盘方向键、搜索/无结果、开关位置、草稿深拷贝、三类二级页面、滚动恢复和回调交接通过。
- 测试结束缓存11页、活动1页；工作集216.55MiB、Private Bytes 227.36MiB。这是包含原设置构造测试夹具的单次测试进程读数，未强制GC，不能当作独立样板长期稳态或主应用总内存结论。
- 测试项目保留既有GuiChecks两项空值警告；样板构建无新增警告。本轮没有可靠测量实际呈现FPS/GPU帧耗时，未以回调次数冒充FPS。
- 实机截图在会话settings-shell-change/verification：01外观、02动画页、03二级页、04小窗口、05面板参数；已逐项检查主要图像，无控件重叠或页脚截断。

- legacy review preserves the supplied startup draft
- eight original categories remain available
- settings shell has no live glass/capture visual
- sidebar is 220 DIP
- controls edit an isolated draft
- secondary navigation keeps the sidebar fixed
- secondary page can scroll in a small window
- back restores the cached page and scroll offset
- forward restores category history
- switch state and knob agree
- switch reaches the checked position
- page cache stays bounded
- third detail page edits only its control-center draft
- search finds settings by category keywords
- search explains an empty result
- rapid navigation preserves draft controls
- sidebar supports native arrow-key navigation
- yellow control minimizes the native window
- green control maximizes the native window
- green control restores the native window
- small window retains content and actions
- review callback receives the modified draft and category

## 尚未验收

只完成样板，不宣称所有旧设置已迁移。跨显示器真实DPI切换、100/125/200%实屏、原生拖动吸附的人工体验、高对比视觉、完整屏幕阅读器和长时间内存稳定性仍未全面验收。现有设置与新样板的最终保存需要用户在原保存页明确操作；本轮未写正式配置、未修改系统选项。
