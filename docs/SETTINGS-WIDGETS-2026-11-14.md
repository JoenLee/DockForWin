# 设置、小组件与菜单整合版

本版本整合设置导航、显式保存、小组件浏览和参数编辑、统一右键菜单，并加入全局中英文、黑色及自定义主题色。以下为当前源码的最终行为。

## 功能核对

源码路径以 `src/DockForWindows/` 为前缀。

| 功能 | 最终行为 | 主要源码 |
| --- | --- | --- |
| 设置导航 | 程序坞、控制中心、小组件、通用四个入口；侧栏可收起，图标保持对齐；前进、返回位于内容顶部；选项行悬停背景延伸到卡片边缘 | `Views/SettingsShellWindow.cs`、`SettingsShellWindow.Pages.cs`、`SettingsShellStyles.xaml` |
| 窗口与托盘 | 黄、绿、红窗口按钮含功能符号和悬停反馈；最小化和关闭隐藏到托盘；托盘恢复原窗口，保留草稿与最大化状态 | `Views/DockWindow.cs`、`SettingsShellWindow.cs`、`Program.cs` |
| 保存与取消 | 不再后台自动提交；保存走原配置提交链路，取消恢复已保存配置；取消透明底，保存浅主题色底，均为紧凑扁平按钮 | `Views/SettingsWindow.cs`、`DockWindow.cs` |
| 程序坞与控制中心 | 设置页使用桌面实时预览开关；高级入口位于当前页面下方；快捷材质与详细参数同步；Dock 实时预览默认开启，大小、间距、放大传递到实际 Dock，关闭预览恢复已保存值 | `SettingsShellWindow.Overview.cs`、`SettingsWindow.Dashboard.cs`、`SettingsWindow.ControlCenter.cs` |
| 全局主题色 | 八种原有预设，加黑色及自定义选色/HEX；三列等宽对齐；按钮、滑块和开关复用主题色；非法颜色不覆盖有效草稿并禁止保存 | `Models/SettingsAccentPresets.cs`、`Models/ThemeAppearance.cs`、`Views/SettingsShellWindow.Pages.cs`、`SettingsShellWindow.Palette.cs` |
| 页面与滚动 | 页面切换、滚轮惯性和轻微边界回弹；关闭动画后直接滚动；顶部为直线边界和轻微内容模糊，不使用页面顶部渐隐遮罩 | `Views/SettingsPageHost.cs`、`SettingsScrollMotion.cs`、`SettingsShellWindow.cs` |
| 小组件首页 | 天气、日历、时钟、提醒事项、备忘录、音乐六类双列卡片；搜索与分类入口；进入类别后仅显示对应模板；卡片悬停反馈与居中时钟图标 | `Views/SettingsShellWindow.WidgetHome.cs`、`WidgetGalleryPage.cs` |
| 小组件轮播 | 手动切换；滚轮、拖动和按钮使用平滑收尾；重新出现的候选卡恢复内容；邻卡边缘根据明暗差柔化；月历圆形切换按钮保持箭头居中 | `Views/WidgetCarousel.cs`、`WidgetContent.Renderer.cs` |
| 大尺寸预览 | 先按完整尺寸布局，再按预览区域等比缩小；适配窗口、面板缩放及悬停边界，避免缩放前裁切 | `Views/WidgetCarousel.cs`、`WidgetHost.cs` |
| 小组件参数栏 | 左侧固定预览、右侧独立滚动，右栏 264 DIP；集中编辑尺寸、材质、轮廓、配色、字体、缩放、阴影、不透明度和数据源；细滚动条常态 3 DIP、悬停 4 DIP、滑块最长 48 DIP；控件等宽并保留安全边距；扁平添加按钮 | `Views/WidgetGalleryPage.Inspector.cs`、`WidgetGalleryPage.Colors.cs`、`SettingsShellStyles.xaml`、`SettingsScrollViewportConverter.cs` |
| Dock 与托盘菜单 | 共用同一 WPF 圆角模板、行高、内边距、分隔线、阴影及主题色高亮；避免文字裁切和弹出即关闭；重新打开读取当前主题与主题色 | `Views/NativeMenuStyles.cs`、`DockWindow.Interactions.cs`、`Services/TrayMenu.cs`、`Program.cs` |

## 外观与兼容性细节

- 悬浮侧栏不磨砂、不采样壁纸、不使用主题色衬底和描边，保留独立阴影。浅色为白色，深色为 `#242427`；文字、图标和选中态随外观同步更新，高对比度使用系统配色。
- 窗口外圆角 12 DIP、侧栏内圆角 9 DIP、侧栏留白 8 DIP，分别控制；选中项灰色背景圆角为 8 DIP。外框使用固定窗口坐标的抗锯齿遮罩，最大化时不保留圆角。
- 前进、返回所在悬浮框与右侧操作按钮均高 26 DIP。框体采用指数 2.5 的超椭圆圆角矩形和独立阴影，外半径 8 DIP、内半径 6 DIP、留白 2 DIP。
- 开关保留原来的 44×24 DIP 轨道、27×20 DIP 胶囊滑块及按压反馈，为边缘和阴影增加安全空间。
- 深浅色及跟随系统的变化在当前窗口更新资源，保留窗口位置、大小、页面、滚动位置与实时预览状态，避免关闭重建造成闪跳。
- 通用页提供中文和英文，保存并重启后应用。`Services/UiText.cs` 与嵌入的 `Resources/Strings.en-US.json` 覆盖设置、Dock、控制中心、小组件、菜单和提示；用户内容与机器路径不翻译。
- 配色预设按稳定的原始名称匹配，显示名称随语言变化；英文选项不会回退到默认配色，提前访问预设目录也不锁定启动语言。
- 小组件材质选择仅保留浅色、深色；旧版 Glass 枚举仍可读取，编辑时在草稿中转换，不直接改写已保存配置。
- 配置继续经过 Copy、Normalize 与 SettingsService；语言、预设及有效自定义颜色可保存、加载。用户数据和固定应用不随本版本提交。

## 验证

使用工程本地 .NET SDK 与 NuGet 缓存，GUI 检查使用隔离数据目录。已完成：

| 检查 | 结果与覆盖 |
| --- | --- |
| Debug / Release 构建 | 通过；最终测试项目有 `GuiChecks.cs:100` 两条既有 nullable 警告，0 错误 |
| 核心检查 | 73 项通过 |
| `--verify-sidebar-preview` | 56 项通过，含保存/取消、托盘恢复、滚动和 264 种小组件规格组合在两种窗口尺寸及 125% 缩放下的预览 |
| `--verify-settings-polish` / `--verify-settings-polish-en` | 中英文专项通过；最终英文运行 1007 项通过，其中 944 项为翻译占位符检查，其余覆盖实时 Dock 调节、颜色持久化、原窗口主题切换、深浅色侧栏、边缘半透明像素、原生拖动/缩放及最大化还原 |
| `--verify-themed-menus` | 11 项通过，含共享模板与尺寸、主题色、菜单重开、动作回调及托盘弹出保持 |

没有重启正式运行实例或覆盖其配置；源码提交不代表正在运行的二进制已经更新。未生成安装包，未验证不同 GPU、混合 DPI 多屏下的长期表现或实际呈现帧率。WPF/隔离测试不能替代这些验收。

## 与 master 的整合

- 保留设置与小组件整合版的四类悬浮侧栏、12/9/8 DIP 圆角、原形开关、全局主题色及中英文；保存仍为显式提交，取消恢复已保存值。
- 高级设置由程序坞页内入口直接打开对应详情页，移除详情页内重复的分类栏；详情页使用完整内容宽度，沿用统一的前进、返回历史及控件路由。
- Dock 菜单保留 master 的项目顺序和按停靠边缘定位，同时继续与托盘共用主题色 WPF 模板。
- 控制中心保留 master 的分段配色、已显示/未显示分组、可见模块排序、入口和快捷键说明；新增文字纳入英文目录。
- 保留 master 的键盘焦点提示、禁用开关反馈和文本输入双重内边距修复。颜色错误显示在对应字段下方，非法输入继续阻止显式保存。
- 控制中心采样、帧缓冲区、窗口合成、DockSurface 与 Fused HLSL/PS 性能实现和 master 一致。

合并结果验证：应用 Debug 构建 0 警告/0 错误，隔离 Release 测试构建 0 错误（`GuiChecks.cs:100` 两条既有 nullable 警告）；核心 73 项、英文专项 1037 项、中文专项 1034 项、侧栏预览 56 项、统一菜单 11 项通过。中英文计数包含 964 项翻译占位符检查，另覆盖新增高级设置与控制中心路由。frame-ownership、capture-stamp、navigation-lifetime 专项通过。

测试使用隔离配置，未更新正在运行的正式二进制或修改用户数据；未重新测量真实呈现 FPS，也未完成不同 GPU、多屏混合 DPI 的长期验收。
