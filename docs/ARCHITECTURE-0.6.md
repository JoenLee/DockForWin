# 0.6 渲染与布局补充

此文补充并覆盖旧文档中的相应布局与材质描述。

`WindowFrameCapture` 为每个可见后方窗口保存 WGC 最新帧，以当前 DWM 窗口坐标组合。D3D11 immediate context 启用原生多线程保护，避免与 WGC 内部线程并发提交造成停帧；只复制所需小区域到 staging texture。`GlassBackdropService` 合成背景，处理监视器边缘颜色延伸；不支持的源使用有超时保护的打印回退，忽略驱动隐形覆盖层。

渲染顺序为固定屏幕坐标背景 → GPU 边缘折射 → Gaussian Blur → 独立窄幅高光、暗边和淡黑衬底。中央不放大，背景不随底板拉伸。只裁剪内容子层，外层 DropShadowEffect 保留自由扩展空间。旧颗粒配置仅为读写兼容，材质不使用颗粒。

`DockStrip` 只用 compositor transforms 做悬停与交换。波浪向上，底板高度固定，宽度根据实时图标范围变化。整排 LayoutTransform 在容量不足时自动缩放，保留原设置值。图标可见范围与底板始终按显示器中心对齐。

前台最大化或覆盖工作区时柔和退下，结束后恢复置顶。退下动画期间的 MouseEnter 不反向打断。设置界面外观与底板主题独立。

官方 API 参考：[WGC FreeThreaded frame pool](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframepool.createfreethreaded)、[D3D11 multithread protection](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_4/nn-d3d11_4-id3d11multithread)、[WPF ShaderEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect)。
