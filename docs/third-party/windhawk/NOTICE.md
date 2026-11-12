# 任务栏可见性适配来源

- Windhawk 1.7.3，官方便携构建，原始安装文件签名已验证：Open Source Developer, Michael Maltsev。
- 官方项目：https://github.com/ramensoftware/windhawk
- Taskbar Styler 1.11，作者 m417z，GPL-3.0；完整未修改源码保存在 tools/windhawk/AppData/ModsSource/windows-11-taskbar-styler.wh.cpp。
- 原始源码：https://github.com/ramensoftware/windhawk-mods/blob/main/mods/windows-11-taskbar-styler.wh.cpp
- 原始许可证：https://github.com/ramensoftware/windhawk-mods/blob/main/LICENSE

本工程使用该适配器仅修改 Taskbar.TaskListButton、TaskListLabeledButton、SearchBoxButton 和 AugmentedEntryPointButton 的 Visibility。未用它建立新的玻璃视觉系统。Dock 与控制中心仍使用工程已有的渲染引擎。

模块使用随附 Clang 20、C++23、x86_64-w64-mingw32 编译，链接 windhawk.lib 与 Windows 系统库 comctl32/gdi32/ole32/oleaut32/runtimeobject/shlwapi，编译参数遵循源文件 Windhawk 元数据。配置位于 tools/windhawk/AppData/Engine/Mods/windows-11-taskbar-styler.ini。二进制与所需运行库保存在其 64 子目录，原有工具许可随工具保留。

本轮仅为本机开发集成，未发布 EXE 或安装包。将来分发需一并保留相应许可、完整对应源码与构建说明。
