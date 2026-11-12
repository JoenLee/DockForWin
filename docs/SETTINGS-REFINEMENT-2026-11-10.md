# 2026-11-10 窗口样式与自适应分隔线

## 完成

- 窗口只保留黑白配色及跟随系统/浅色/深色。旧配色枚举保留读取兼容，Normalize 将旧选择迁移为 Mono；不修改 Dock 底板主题。四套配色选项和相关说明已移除。
- 按用户补充确认，移除控制中心设置中图1全部内容：快捷键输入、“打开控制中心”、“导入网页设计参数…”以及该组说明文字。两个入口开关与其余设置仍保留。已存快捷键及系统入口服务未删除。
- Ionicons Filled 保持不变；侧栏底色改为参考图的橙、蓝、绿和灰。窗口滑块进度使用黑白强调色。
- 三个窗口控制移至右上，左至右为绿/黄/红。16 DIP 圆圈，绿色双三角、黄色横线、红色叉号使用居中矢量路径，背景和符号色按参考图近似取色，无外部框。悬停缩放至1.12倍，120ms过渡；实际最大化、还原、最小化和关闭行为继续使用 WPF 原生窗口行为。
- 设置开关按提供的 CSS 用原生 WPF 实现：轨道47×22 DIP（比41略长），白色滑块27×17 DIP，偏移15 DIP，250ms，KeySpline(.4,0,.2,1)，黑色轨道，圆角和柔和阴影。保留原 CheckBox 绑定、键盘、点击及草稿提交链路。
- 最近/运行应用分隔线复用正在上传的原始背景帧，在自身屏幕位置采样，使用与运行小圆点相同的亮度平滑、滞回阈值和240ms颜色过渡。浅背景显示深色，深背景显示浅色。原分隔线尺寸、位置和0.35透明度保持；未增加 WGC 捕获或背景上传频率。停用玻璃时恢复原静态配色。

## 文件

设置：SettingsWindow.cs、SettingsWindow.ControlCenter.cs、SettingsShellWindow.cs、SettingsShellWindow.Palette.cs、SettingsShellStyles.xaml、SettingsSwitchMotion.cs。

配色迁移：Views/InterfaceStyles.cs、Models/DockModels.cs。

分隔线：Views/DockWindow.cs（转为partial，接入现有帧回调）、新增Views/DockWindow.Separator.cs。

必要测试：新增SettingsRefinementChecks.cs及Program入口；适配原SettingsShellChecks开关距离、FullSettingsChecks取消被删除控件的断言及主题重建入口。没有重复完整GPU或玻璃性能测试。

## 验证

- tools/Build.ps1 Release：0警告/0错误。
- --verify-settings-refinement：20项通过，包括旧配色兼容、右上键顺序/位置、三符号居中、原生窗口动作、开关尺寸/位移/状态、删除内容、深色界面、分隔线亮暗双向切换及不保存配置。分隔线测试使用真实Dock视觉与合成黑/白背景帧，不代表真实壁纸端到端时延验收。
- --verify-settings-rebuild：8项通过，包括主题重建、二级导航、滚动/尺寸、未保存草稿、预览和取消恢复。使用新的浅深外观重建入口；彩色主题切换入口已删除。
- 测试项目构建仍有旧GuiChecks.cs:98两条可空警告。
- 实机截图01-light、02-switch、03-monochrome、04-control-entry、05-dark已核对。本机缩放下符号中心误差限制在半个物理像素内。
- 对上轮49个服务/Shader/浮层受保护文件重新核对SHA256，全部未变。

没有改用户正式data或开机启动注册。现场原程序已由用户切为Debug PID27272且设置窗口打开，为保留草稿未关闭/重启。新的Release已构建，不能声称该Debug实例已加载本轮改动。

本目录originals保存修改前副本，verification为截图及检查证据。不自动清理。真实混合壁纸与跨屏DPI、悬停实机体感和长时间内存仍需使用验收；未声称新的FPS或GPU耗时收益。
