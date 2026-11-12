# 紧凑控制中心

2026-11-07。开发工程仍在 D:\DockForWindows，不生成安装包。

入口：任务栏网络/音量/电池区域、托盘菜单、Ctrl+Win+A。Ctrl+单击任务栏仍打开 Windows 原生入口。快捷键可在设置 → 控制中心更改；冲突会提示。点击外部或 Esc 收起。

控制中心采用宽 360 DIP 的四列紧凑网格，背景空隙透明；每个卡片单独使用现有 DockSurface、LiquidGlassEffect 和 GlassBackdropService，所有卡片共享一次背景采样。没有重写共享引擎、着色器、DockStrip 或图标逻辑。默认继承全局玻璃参数；设置内可启用仅对此面板生效的材质覆盖。设置窗口保持原有不透明、三栏、四套配色布局。

“编辑控制”后拖动卡片换位，右键可移除或改为小按钮、半宽、全宽；“添加控件”可恢复。顺序、尺寸和显隐保存到全局 settings.json 的 ControlCenter 节点，重启保留。主设置里也提供模块与排列、热键、入口、材质和任务栏设置。

| 模块 | 实际系统接口 / 边界 |
| --- | --- |
| Wi-Fi | Windows Radio 开关、Native WLAN 状态/网络列表/扫描/已保存连接/断开；个人网络可输入密码，通过 WlanSetProfile 存到当前用户的 Windows 配置。企业认证使用原生网络面板。附近网络可能需要位置权限。 |
| 蓝牙 | Radio 真实开关、DeviceInformation 已配对设备/连接状态。配对与连接由 Windows 原生蓝牙页面处理。 |
| 专注 | FocusSessionManager 读取实际状态。第三方直接启停受 Microsoft Limited Access Feature 限制，提供 Windows 专注入口，未伪造开关。 |
| 显示器 | WMI MonitorBrightness/MonitorBrightnessMethods 实际亮度读写；硬件没有接口时停用，提供显示设置。 |
| 声音 | Core Audio 主音量/静音、实际输出设备列表；默认输出切换封装在独立 COM 服务，系统拒绝时显示错误并保留原生声音设置入口。 |
| 屏幕投影 | QueryDisplayConfig 读取；SetDisplayConfig 切换仅电脑、复制、扩展、仅第二屏。无线显示使用 Windows 连接入口。 |
| 媒体 | GSMTC 当前会话、标题/作者/封面、上一首/播放暂停/下一首。没有播放器会话时停用相应控件。 |
| 电池 | GetSystemPowerStatus 实际电量与电源状态，附原生电源设置入口。 |

读取一般每 900ms 校对，Radio/媒体事件及时触发同步；亮度写入合并防止排队，音量直接跟随。采样忙时跳帧，动画使用现有 DockMath 的时间步进和 WPF 动画，与采样独立。关闭释放捕获、COM/WinRT 订阅、计时器和鼠标/热键钩子。定位按鼠标所在显示器工作区和 DPI，空间不足整体适配。

任务栏隐藏是独立可关闭选项，随附 Windhawk 1.7.3 便携适配，仅折叠应用/搜索/小组件按钮，不删除 Windows 固定项，保留开始与托盘。正常退出或守护检测到 Dock 异常退出后卸载适配，恢复原任务栏；发现其他 Windhawk 实例时不接管。没有修改 Explorer 文件或安装系统服务。来源和许可见 third-party/windhawk/NOTICE.md。

验证证据见 VERIFICATION-control-center.md。
