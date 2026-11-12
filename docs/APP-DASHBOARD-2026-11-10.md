# APP Dashboard 视觉升级 · 2026-11-10

## 最小方案与范围

保留原有 SettingsShellWindow / SettingsWindow、SettingsPageHost 页面缓存与转场、草稿 Copy/Normalize 和 SettingsService 提交链路。只调整 APP 本体，增加概览页面与导航映射，不复制控制中心或渲染系统、不新增框架。

## 交付

- 默认打开 Dashboard。240 DIP 浅灰白侧栏，可折叠至68 DIP；七个主分类与底部关于/真实版本。中性灰 Filled 图标、浅青 #E5F0F0 选中面、深青 #00575B 文字和图标；保留键盘、搜索及无蓝色焦点框。
- 新导航将旧设置归类：图标与倒影在外观页；应用/文件、位置/隐藏、系统/启动与窗口主题在通用页。原44个滑块、3个颜色编辑器及模块开关继续使用原回调。
- 六张统一卡片显示草稿入口状态、材质、真实进程Working Set、外观和动画。小组件服务未在源码中接入，因此标注尚未接入；CPU/GPU/FPS/P95/P99与背景延迟同样明确未接入，不显示伪造图表。内存只按进入页面/主动刷新取快照，区分Working Set、Private Bytes和未接入的Committed/GPU内存。
- 统一 SettingsShellStyles 的颜色、表面、圆角、导航及基础控件；合并重复路由/标题表，去掉旧彩色图标底块和无用导航字段。公共设置组与Dashboard使用同一表面Style；没有大规模改写业务。
- 一级沿用200ms淡入/轻移，二级270ms及反向返回；页面缓存、滚动、快速中断与历史恢复继续由原SettingsPageHost管理。
- 开关55×33 DIP、白色25 DIP正圆、四周至少4 DIP内缩、22 DIP移动、200ms缓出；关闭 #E9E9EB（深色使用适配灰），开启 #080808，轻阴影。
- 浅色为白/浅灰；仍保留深色与系统主题、原生拖动/缩放/最大化/最小化/关闭和右侧窗口控制键。

## 源码

SettingsShellWindow.cs、.Palette.cs、.Pages.cs、新.Overview.cs；SettingsShellStyles.xaml；SettingsSwitchMotion.cs；SettingsWindow.cs、.ShellLayout.cs、.ShellPreview.cs。测试更新了新分类与尺寸断言，并增加 --verify-dashboard 定向入口。

## 验证

Release构建0警告/0错误。全设置40项、Dashboard11项、开关6项、重建8项、焦点7项通过，均为隔离配置WPF实窗检查。本机160.42% DPI，覆盖最小窗口、浅深主题、快速切页、返回/滚动、真实换主题和取消恢复、颜色校验、隔离保存。测试项目仍有既有GuiChecks.cs:98两项可空性警告。

67个已有非设置Views/Services/Shader文件SHA256与修改前一致，包括控制中心、Dock、Liquid Glass、WGC/背景服务及其动画；未触碰用户data。实际源码目录无.git，原件保存在本会话dashboard-redesign/before，未做清理或覆盖其他修改。

证据位于会话 dashboard-redesign：verification/full-checks、dashboard-verification/00-dashboard.png、switch-verification/switch-shape.png、rebuild-verification、focus-verification、各checks.log与verification-summary.json。旧整套性能测试未重复运行；真实呈现FPS、跨屏DPI拖动和长期稳定性未新增验收。

主程序Release已更新；没有强制关闭旧实例或丢弃草稿。Open-Settings-UI-Preview.cmd仍可构建并打开隔离配置完整样板。新构建不代表旧进程已加载，正式体验前请保存旧草稿并正常重启。
