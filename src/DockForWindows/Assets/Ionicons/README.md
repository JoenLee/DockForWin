# Settings sidebar icons

Source: [Ionic official Ionicons repository, v8.0.0](https://github.com/ionic-team/ionicons/tree/v8.0.0/src/svg).

The unmodified SVG originals and MIT license are included here. Names without `-outline` or `-sharp` are the official Filled variant.

| Category | Official icon |
| --- | --- |
| 外观与主题 | contrast |
| 图标与倒影 | grid |
| 动画与交互 | sparkles |
| 应用与文件 | folder |
| 位置与隐藏 | desktop |
| 系统与启动 | settings |
| 界面主题 | color-palette |
| 控制中心 | options |

`SettingsIonicons.xaml` preserves the original 512×512 viewport, SVG nonzero path fill rule and circle primitives as frozen native WPF DrawingImages. The transparent viewport rectangle preserves consistent scaling. Only presentation color is set to white to match existing colored sidebar tiles; original path coordinates remain unchanged. No SVG parser or network access is used at runtime.


## Control center Filled icons

ControlCenterIcon uses 28 official v8.0.0 base/Filled SVGs from https://github.com/ionic-team/ionicons/tree/v8.0.0/src/svg . Existing MIT LICENSE applies. Source originals remain embedded resources. ControlCenterIcon.Metrics.cs contains generated optical normalization data; regenerate offline with the test tool --generate-control-icon-metrics <output.cs> after updating SVGs. No SVG parsing or optical-mass calculation occurs per animation frame.
