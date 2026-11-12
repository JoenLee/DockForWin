# Dock 图标、窗口菜单与堆叠预览（2026-11-11）

## 实现
- 多窗口应用悬停约350ms显示后台式堆叠卡片，最多同时持有3张；滚轮双向循环，标题显示当前序号/总数，点击选中真实窗口。卡片260×178 DIP，原生圆角裁切，按监视器工作区/DPI定位。离开图标和预览区域280ms后关闭。
- 使用DWM实时缩略图，不新增WGC采集、截图读回或帧刷新计时器。关闭时注销全部缩略图和窗口；窗口失效时更新列表。
- 已确认新窗口命令的Chrome、Edge、Brave、Firefox、VS Code/Insiders、Windows Terminal增加“新建窗口”；未知应用不伪造支持。保留原启动参数和工作目录，不修改固定应用配置。
- 点击图标恢复/激活应用；再次点击当前应用时收起该应用可见窗口组。菜单和预览开启时才安装外部鼠标点击监听，桌面任意外部点击关闭菜单且继续传递原点击。
- 通知点接入真实Shell任务栏闪烁/attention信号，橙色呼吸，激活后清除。它不是通用未读数/Toast监听器：仅发Toast而不请求任务栏注意的应用不保证变橙。
- 增加最近应用/回收站分隔线，两个分隔线各自适配明暗背景。
- 普通Dock图标去掉程序生成方形底板，按可见内容最长边统一至88%，保留比例；GPT保留旧呈现。不会破坏性裁切应用原图内自带白底。
- 控制中心Battery保留battery-half，HDR改color-filter、夜间护眼eye、截屏scan、录屏videocam，全部官方Ionicons v8.0.0。修复SVG加载器遗漏line和CSS描边，解决电池轮廓/极柱丢失；重新生成光学归一化指标。
- APP红黄绿窗口控件符号中心绘制，轻微悬停放大1.12→1.17。布局、系统逻辑、玻璃Shader、背景采集管线保持本轮前实现。

## 修改位置
- Views/DockWindow.cs、DockWindow.Interactions.cs：菜单生命周期、悬停、Shell注意信号、激活/收起。
- Views/WindowPreviewStack.cs、Services/OutsideClickWatcher.cs：原生堆叠预览、临时外部点击监听。
- Services/LaunchService.cs、Models/DockModels.cs：已知新窗口命令、瞬态NeedsAttention。
- Views/DockTile.cs、IconPresentation.cs、DockWindow.Separator.cs：通知灯、裸图标和分隔线。
- Views/ControlCenterIcon.cs、ControlCenterIcon.Metrics.cs、Assets/Ionicons/{eye,scan,videocam,color-filter}.svg：官方图标及SVG描边解析。
- Views/WindowControlGlyph.cs、SettingsShellWindow.cs、SettingsShellStyles.xaml：窗口符号居中及悬停。
- tests/DockForWindows.Tests/{DockInteractionChecks.cs,Program.cs}：专项检查入口及原保留底板测试显式指定GPT例外。

## 验证与限制
- --verify-dock-interactions：24项通过，包括真实窗口点击收起/恢复、菜单外部点击、三张有效DWM缩略图、圆角窗口区域、滚轮双向切页、关闭释放、通知灯启动/停止、两条分隔线及图标几何。通知信号测试注入真实Shell消息格式，不代表每款第三方应用均实测。
- --verify-interactive-motion：前7项通过（独立图标动画、快速重触发后原像素恢复、滑块阻尼与弹簧），随后在真实鼠标捕获断言失败；完整42项不能标记通过，本轮未修改滑块代码。已保留失败情况，未把失败当成正常验收。
- 测试构建成功，GuiChecks.cs:98有2项既存nullable警告。
- tools/Build.ps1 Release已完成Shader步骤/编译，但正式DLL被PID25116占用，复制失败。独立Release输出release-ready构建0警告0错误。
- PID25116仍打开“程序坞设置”，为保护可能未保存草稿未关闭或重启，当前运行版本尚未更新。保存并关闭设置后运行本会话Apply-Dock-Changes.cmd，脚本再次检查草稿、正常退出、正式构建后再启动。
- 未测实际屏幕FPS/GPU帧耗时、所有第三方新窗口命令的真实启动、跨显示器各种DPI及长时间稳定性；不宣称零延迟或帧率提升。最小化/受保护窗口预览可能受DWM源画面可用性限制。

## 证据和回退
会话目录：C:\Users\HENG\Documents\ChatGPT\DockForWindows\dock-interaction-change。
before保存原件；checks.log为24项结果；checks/preview-stack.png为原生预览截图；release-staged.log为独立Release构建结果。实际源码D:\DockForWindows当前无.git，未覆盖/清理用户数据。

## 来源
- 官方SVG：https://github.com/ionic-team/ionicons/tree/v8.0.0/src/svg （现有MIT声明保留）
- DWM：https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail
- VS Code：https://code.visualstudio.com/docs/configure/command-line
- Windows Terminal：https://learn.microsoft.com/en-us/windows/terminal/command-line-arguments
