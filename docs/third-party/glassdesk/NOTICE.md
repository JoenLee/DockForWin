# GlassDesk optical material adaptation

Adapted from https://github.com/iishen717/GlassDesk at commit
`737b8dc1737e029b292c4348f7d2254b290da971` (product snapshot 1.3.1).
GlassDesk: Copyright 2026 everlosttime and GlassDesk contributors, Apache-2.0.

Source files: `src/GlassDesk.UI/Effects/TransitionGlass.hlsl` and
`src/GlassDesk.UI/Controls/AndroidLiquidGlassRenderer.cs`.

Original lens profile and expanded-radius gradient: Kyant0/AndroidLiquidGlass,
commit `65ab177e90e5c1d8c62e70cf7755841982da65f6`, Copyright 2025 Kyant,
Apache-2.0. Original attribution is retained in `android-liquid-glass/NOTICE.md`.

Local changes (2026-11-07): ported GlassDesk's compact 24 DIP / 32 DIP lens
and subpixel chromatic split into DockForWindows' existing WPF pixel shader.
Retained the existing continuous cubic-to-capsule outline distance and used the
upstream expanded-radius normal for lens direction. Mapped the existing distortion
control to shoulder width and displacement, with .65 matching upstream dimensions.
Preserved the existing independent lighting/tint pass and Gaussian post-filter;
did not port upstream strength-coupled highlights, saturation, tint, host windows,
fixed-wallpaper behavior, settings UI or animations. Real-time live capture remains
unchanged. No GlassDesk application icons or branding assets were copied.

Original licenses and notices accompany this file. Raw reference source remains
in `D:\Glassdeskcode_glass_copy`; only the shader optics is integrated.
