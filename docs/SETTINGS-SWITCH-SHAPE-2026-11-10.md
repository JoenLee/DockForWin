
## 19. 2026-11-10 开关内缩与单层配色框修正

- 用户指出上一版胶囊边缘挤压，且明确关闭为灰色、开启才为黑色。设置开关调整为54×24DIP轨道、32×18DIP滑块，四周最小3DIP内缩、16DIP位移；移除滑块重阴影和按压拉伸，保留250ms曲线及真实CheckBox状态。浅色关闭#D1D1D6、深色关闭#5B5B61、开启#080808。
- 控制中心配色设置保留外层圆角组框，去掉ComboBox内层描边。下拉模板改用TemplateBinding BorderThickness、默认仍为1，仅此行设为0；新增cc-color-mode定位ID。其余下拉外观不变。
- 新--verify-switch-shape六项通过（灰/黑状态、四边留白、垂直一致、干净轮廓、24次快速反向不越界、终点）；--verify-settings-color-row三项通过（单层边框、打开下拉、只改草稿）。Release构建0警告/0错误。没有重跑旧整套测试、没有写正式配置或关闭用户窗口。
- 修改文件：SettingsShellStyles.xaml、SettingsSwitchMotion.cs、SettingsShellWindow.Palette.cs、SettingsWindow.ControlCenter.cs。相应旧测试尺寸/位移断言同步更新，新增SwitchShapeChecks并增加专用测试入口。证据和本轮原件位于会话switch-shape-change；截图verification/switch-shape.png。当前已运行实例是否加载新版本仍须现场核对。