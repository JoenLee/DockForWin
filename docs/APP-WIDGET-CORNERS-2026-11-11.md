# 小组件圆角、滚轮与间距修正

用户提供图二后，本修正优先于上一版完整超椭圆外轮廓及边界滚轮冒泡的说明。

- 卡片改为直边搭配小连续圆角：半径 `min(14 DIP, 短边×14%)`，现有规格约 11.76–14 DIP。复用现有 `DockSurface.CreateGeometry` 的局部连续曲率几何，保留 n=2.5 / 3；没有修改 DockSurface 本身。背景、边框、内容裁剪、阴影仍共用冻结几何缓存。
- 按小圆角调整天气和日历内容内边距，保留原尺寸、数据与功能。
- 鼠标在轮播内时滚轮始终消费；在首尾也不带动外层页面，不循环。轮播区域外继续正常纵向滚动。
- 按卡片实时缩放后的宽度累计位置，边缘间距统一为 22 DIP，替代固定中心间距。小窗口为相邻卡片预留露出空间；拖动固定起始映射间距，避免缩放期间输入映射抖动。每帧只更新既有变换和数值，不创建新几何或控件。

改动文件：`Views/WidgetHost.cs`、`Views/WidgetCarousel.cs`、`Views/WidgetContent.cs`，以及 `tests/DockForWindows.Tests/WidgetGalleryChecks.cs`。应用路径以 `D:\DockForWindows\src\DockForWindows` 为基准。

验证：隔离 Release 构建成功，应用无新增警告，测试仍有 GuiChecks.cs:98 两项既存 nullable 警告。71 项定向检查通过，包括所有规格的小圆角与几何缓存、实际轮廓/裁剪一致性、首尾滚轮不滚页、4 种选中状态的边缘间距、真实鼠标拖动/中途反向/捕获丢失/吸附，以及原草稿保存和启动注册保留。

本机约 160.42% DPI 下的相邻卡片实测变换边界间距约 21.86–22.23 DIP，差异为布局像素舍入；没有卡片轮廓重叠。已查看修正版 Light 截图；三材质仍通过共用内容与轮廓检查。未重测实际 FPS、GPU 或长时间内存。

证据：`C:\Users\HENG\Documents\ChatGPT\DockForWindows\widget-corners-spacing-fix` 下的 before、build.log、checks-final.log、checks-final 截图、runtime.json。首次过严的间距断言未等待布局且未考虑 DPI 舍入，保留 checks.log；后续按真实布局和像素容差通过。

已独立打开“小组件库 · 小圆角与间距修正版”（当时 PID 21704）；旧预览 PID 38116 保留以保护未保存内容。正式 PID 10604 未重启。修正版使用本轮目录下 preview/isolated-data，未覆盖正式 data 或旧预览配置。下一次构建统一预览入口前须先正常关闭占用其输出的旧预览。
