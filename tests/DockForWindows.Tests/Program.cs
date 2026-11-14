using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class Program
{
    private static int _passed, _failed;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string name);
    private static void Check(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length==3&&args[0]=="--preview-settings-polish")return SettingsPolishChecks.Preview(args[1],args[2]=="en-US");
        if(args.Length==2&&args[0]=="--verify-settings-polish")return SettingsPolishChecks.Run(args[1],false);
        if(args.Length==2&&args[0]=="--verify-settings-polish-en")return SettingsPolishChecks.Run(args[1],true);
        if(args.Length==2&&args[0]=="--verify-themed-menus")return ThemedMenuChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-sidebar-preview")return SidebarPreviewChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--preview-widget-system")return WidgetSystemChecks.Run(new[]{args[1],"--preview"});
        if(args.Length==2&&args[0]=="--verify-widget-connected")return WidgetConnectedChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-widget-system")return WidgetSystemChecks.Run(new[]{args[1]});
        if(args.Length==2&&args[0]=="--verify-widget-desktop")return WidgetSystemChecks.Run(new[]{args[1],"--desktop-only"});
        if(args.Length==2&&args[0]=="--verify-widget-system-live")return WidgetSystemChecks.Run(new[]{args[1],"--desktop-only","--live"});
        if(args.Length==2&&args[0]=="--verify-widget-gallery")return WidgetGalleryChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--preview-widget-gallery")return WidgetGalleryChecks.Run(args[1],true);
        if(args.Length==2&&args[0]=="--verify-widget-weather-live")return WidgetGalleryChecks.LiveWeather(args[1]);
        if(args.Length==2&&args[0]=="--measure-widget-gallery")return WidgetGalleryChecks.Measure(args[1]);
        if(args.Length==2&&args[0]=="--verify-dock-interactions")return DockInteractionChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--measure-backdrop-latency")return BackdropLatencyChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-shadow-render")return ShadowRenderChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--measure-material-perf")return MaterialPerfChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--measure-control-center-perf")return ControlCenterPerfChecks.Run(args[1]);
        if(args.Length is 5 or 6&&args[0]=="--perf-background")return ControlCenterPerfChecks.Background(double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture),double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),double.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture),double.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture),args.Length==6?int.Parse(args[5]):0);
        if(args.Length==2&&args[0]=="--verify-card-frost")return CardFrostChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-control-polish")return ControlPolishChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-interactive-motion")return InteractiveMotionChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-entrance-motion")return EntranceMotionChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--generate-control-icon-metrics")return GenerateIconMetrics.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-classic-refinement")return ClassicRefinementChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-classic-theme-ui")return ClassicThemeUiChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-classic-blur")return ClassicBlurChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-settings-color-row")return SettingsFocusChecks.Run(args[1],false,true);
        if(args.Length==2&&args[0]=="--verify-switch-shape")return SwitchShapeChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-settings-refinement")return SettingsRefinementChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-settings-icons")return SettingsFocusChecks.Run(args[1],true);
        if(args.Length==2&&args[0]=="--verify-settings-focus")return SettingsFocusChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-settings-rebuild")return FullSettingsChecks.Rebuild(args[1]);
        if(args.Length==2&&args[0]=="--verify-dashboard")return FullSettingsChecks.Run(args[1],false,true);
        if(args.Length==2&&args[0]=="--verify-full-settings")return FullSettingsChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--preview-full-settings")return FullSettingsChecks.Run(args[1],true);
        if(args.Length==2&&args[0]=="--verify-settings-shell")return SettingsShellChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--preview-settings-shell")return SettingsShellChecks.Run(args[1],true);
        if(args.Length==1&&args[0]=="--verify-capture-stamp")return CaptureStampChecks.Run();
        if(args.Length==2&&args[0]=="--verify-slider-animation")return SliderAnimationChecks.Run(args[1]);
        if(args.Length==1&&args[0]=="--verify-glass-response")return GlassResponseChecks.Run();
        if(args.Length==1&&args[0]=="--verify-frame-ownership")return FrameOwnershipChecks.Run();
        if(args.Length==2&&args[0]=="--verify-spring-curves")return SpringCurveChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-glass-resources")return GlassResourceChecks.Run(args[1]);
        if(args.Length==3&&args[0]=="--verify-shader-equivalence")return ShaderEquivalenceChecks.Run(args[1],args[2]);
        if(args.Length==1&&args[0]=="--verify-navigation-lifetime")return NavigationLifetimeChecks.Run();
        if(args.Length==3&&args[0]=="--verify-glass-steady")return GlassSteadyChecks.Run(int.Parse(args[1]),args[2]);
        if(args.Length==1&&args[0]=="--probe-media")return MediaProbeChecks.Run();
        if(args.Length==2&&args[0]=="--verify-control-sweep")return ControlSweepChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-frame-pacing")return FramePacingChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-control-motion")return ControlMotionChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-control-design")return ControlCenterDesignChecks.Run(args[1]);
        if(args.Length==2 && args[0]=="--verify-glass-alignment")return GlassAlignmentChecks.Run(args[1]);
        if(args.Length==2 && args[0]=="--verify-settings-preview")return SettingsPreviewChecks.Run(args[1]);
        if(args.Length==2 && args[0]=="--verify-control-refinement")return ControlCenterRefinementChecks.Run(args[1]);
        if(args.Length==2 && args[0]=="--verify-control-presentation")return ControlCenterPresentationChecks.Run(args[1]);
        if(args.Length==1 && args[0]=="--verify-control-services")return ControlCenterServiceChecks.Run();
        if(args.Length==1 && args[0]=="--verify-control-center-interactions")return ControlCenterInteractionChecks.Run();
        if(args.Length==2 && args[0]=="--verify-control-center-scale")return ControlCenterScaleChecks.Run(args[1]);
        if(args.Length==1 && args[0]=="--verify-control-center-rendering-short")return ControlCenterRenderingChecks.Run(true);
        if(args.Length==1 && args[0]=="--verify-control-center-rendering")return ControlCenterRenderingChecks.Run();
        if(args.Length==1 && args[0]=="--verify-scaled-backdrop")return ControlCenterRenderingChecks.ScaledBackdrop();
        if(args.Length==1&&args[0]=="--capture-lifetime")return CaptureLifetimeChecks.Run();
        if(args.Length==1&&args[0]=="--verify-reorder-animation")return ReorderAnimationChecks.Run();
        if (Array.Exists(args, a => a == "--fixture")) return RunFixture();
        if (args.Length == 2 && args[0] == "--verify-glass-layout") return GuiChecks.Run(args[1]);
        if (args.Length == 2 && args[0] == "--verify-glassdesk-optics") return GlassDeskOpticsChecks.Run(args[1]);
        if (args.Length == 2 && args[0] == "--verify-adaptive-contrast") return AdaptiveContrastChecks.Run(args[1]);
        if (args.Length == 2 && args[0] == "--verify-redesign") return RedesignChecks.Run(args[1]);
        if (args.Length == 2 && args[0] == "--verify-four-themes") return FourThemeChecks.Run(args[1]);
        if(args.Length==2&&args[0]=="--verify-control-center")return ControlCenterChecks.Run(args[1]);
        if (args.Length == 2 && args[0] == "--drop-fixture") return RunDropFixture(args[1]);
        if (args.Length == 2 && args[0] == "--launch-probe") { File.WriteAllText(args[1], "launch-ok"); return 0; }
        if (args.Length == 3 && args[0] == "--capture-window")
        {
            using var capture = new WindowFrameCapture();
            for (int attempt = 0; attempt < 30; attempt++)
            {
                using var bitmap = capture.Read(nint.Parse(args[1]), new System.Drawing.Rectangle(-32768, -32768, 65536, 65536), out var bounds);
                if (bitmap is not null)
                {
                    Console.WriteLine($"bounds={bounds} center={bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2)} copies={capture.Copies} error={capture.LastError}");
                    bitmap.Save(args[2], System.Drawing.Imaging.ImageFormat.Png); return 0;
                }
                Thread.Sleep(50);
            }
            Console.WriteLine("CAPTURE FAILED " + capture.LastError); return 1;
        }
        if (args.Length == 6 && args[0] == "--glass-capture")
        {
            using var backdrop = new GlassBackdropService();
            var bounds = new System.Drawing.Rectangle(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]));
            var frame = backdrop.Render(FindWindow(null, "DockForWindows"), bounds, 82, 1, 154.0 / 96);
            Console.WriteLine(backdrop.LastSourceWindows);
            if (frame is null) return 1;
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
            using var file = File.Create(args[5]); encoder.Save(file); return 0;
        }
        if (args.Length == 2 && args[0] == "--audit-icons")
        {
            using var auditIcons = new IconService();
            var pins = new SettingsService(args[1]).Load().Pins;
            int fallback = 0, dependent = 0;
            foreach (var pin in pins)
            {
                var image = auditIcons.GetAsync(pin, true).GetAwaiter().GetResult();
                bool missing = IconService.IsFallback(image);
                bool desktopLink = pin.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && pin.Target.Contains("\\Desktop\\", StringComparison.OrdinalIgnoreCase);
                if (missing) fallback++; if (desktopLink) dependent++;
                Console.WriteLine($"{pin.Name}: icon={(missing ? "FALLBACK" : "OK")}, desktopDependency={desktopLink}");
            }
            Console.WriteLine($"AUDIT {pins.Count} pins; {fallback} fallback icons; {dependent} desktop dependencies");
            return fallback + dependent == 0 ? 0 : 1;
        }
        var root = Path.Combine(Environment.GetEnvironmentVariable("DOCK_TEST_ARTIFACTS") ?? Path.GetTempPath(), "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Check("旧界面配色统一黑白且不更改 Dock 材质",()=>{
            var s=new DockSettings{Theme=DockTheme.Glass};var original=ThemeService.Resolve(s,false,true);var accents=new HashSet<string>();
            foreach(InterfaceStyle style in Enum.GetValues<InterfaceStyle>()){s.InterfaceStyle=style;accents.Add(InterfaceStyles.Resolve(style,false).Accent);Assert(ThemeService.Resolve(s,false,true)==original,"界面配色改变了 Dock 材质");}
            Assert(accents.Count==1,"旧界面配色未统一为黑白");
        });
        Check("普通浅色深色系统主题均可实际调节圆角边框与阴影",()=>{
            foreach(var theme in new[]{DockTheme.Light,DockTheme.Dark,DockTheme.System}){
                var s=new DockSettings{Theme=theme};s.Appearance.CornerRadius=76.5;s.Appearance.BorderThickness=2.3;s.Appearance.BorderColor="#345678";s.Appearance.ShadowOpacity=.43;s.Appearance.ShadowBlur=31;s.Appearance.ShadowEnabled=false;
                var p=ThemeService.Resolve(s,false,true);Assert(p.CornerRadius==76.5&&p.BorderThickness==2.3&&!p.ShadowEnabled,"普通主题设置被忽略");
                s.Appearance.ShadowEnabled=true;p=ThemeService.Resolve(s,false,true);Assert(p.ShadowEnabled&&p.ShadowOpacity==.43&&p.ShadowBlur==31,"普通主题阴影未生效");
            }
        });
        Check("旧界面风格保存和读取时统一迁移为黑白",()=>{
            var s=new DockSettings{InterfaceStyle=InterfaceStyle.Warm};var store=new SettingsService(Path.Combine(root,"interface-styles"));store.Save(s);Assert(store.Load().InterfaceStyle==InterfaceStyle.Mono,"旧界面风格未迁移为黑白");
            s.InterfaceStyle=(InterfaceStyle)999;s.Normalize();Assert(s.InterfaceStyle==InterfaceStyle.Mono,"非法界面风格未修正");
        });
        Check("所有主题只有完全无底板时才允许倒影",()=>{
            foreach(DockTheme theme in Enum.GetValues<DockTheme>()){
                var s=new DockSettings{Theme=theme,Reflections=true};s.Appearance.Opacity=.01;
                Assert(!AppearancePolicy.ShowReflections(s),"半透明主题仍显示倒影");
                s.Appearance.Opacity=0;Assert(AppearancePolicy.ShowReflections(s),"全透明主题未允许倒影");
                var visual=ThemeService.Resolve(s,false,true);Assert(visual.Background.A==0&&!visual.ShadowEnabled,"全透明主题残留底板或阴影");
            }
            var glass=new DockSettings{Theme=DockTheme.Glass,GlassOpacity=0};Assert(AppearancePolicy.ShowReflections(glass),"玻璃完全透明时不允许倒影");
        });
        Check("桌面默认固定且桌面隐藏与开机自启独立持久化",()=>{
            var s=new DockSettings();Assert(!s.HideOnDesktop&&!s.StartWithWindows,"新选项默认误开启");
            s.HideOnDesktop=true;s.StartWithWindows=true;s.AutoHide=false;var store=new SettingsService(Path.Combine(root,"new-behavior"));store.Save(s);var loaded=store.Load();
            Assert(loaded.HideOnDesktop&&loaded.StartWithWindows&&!loaded.AutoHide,"新选项未保存或互相耦合");
        });
        Check("开机自启正确引用程序和独立配置路径",()=>{
            var command=StartupRegistration.BuildCommand(@"D:\Program Files\dotnet.exe",@"D:\Dock folder\DockForWindows.dll",@"D:\Profile folder");
            Assert(command=="\"D:\\Program Files\\dotnet.exe\" \"D:\\Dock folder\\DockForWindows.dll\" --data-directory \"D:\\Profile folder\"","开发版启动命令错误");
            command=StartupRegistration.BuildCommand(@"D:\Dock app\DockForWindows.exe","",@"D:\User data");Assert(!command.Contains(".dll")&&command.Contains("--data-directory"),"独立版启动命令错误");
        });
        Check("启动注册可写入、读取和关闭且隔离于真实开机启动项",()=>{
            string key=@"Software\DockForWindows\Tests\"+Guid.NewGuid().ToString("N");
            try{StartupRegistration.WriteCommand("\"D:\\Fixture\\Dock.exe\"",key);Assert(StartupRegistration.ReadCommand(key)=="\"D:\\Fixture\\Dock.exe\"","启动注册未写入");StartupRegistration.WriteCommand(null,key);Assert(StartupRegistration.ReadCommand(key) is null,"关闭后注册仍存在");}
            finally{
                // Remove the fixture key and the parents it created when they are empty, leaving no trace.
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(key,false);
                foreach(var parent in new[]{@"Software\DockForWindows\Tests",@"Software\DockForWindows"}){using var probe=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(parent);if(probe is not null&&probe.SubKeyCount==0&&probe.ValueCount==0){probe.Close();Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(parent,false);}}
            }
        });
        Check("GPT例外保留原有黑形白框与独立缓存",()=>{
            var source=RedesignChecks.Glyph(System.Windows.Media.Brushes.White);
            var icon=(System.Windows.Media.Imaging.BitmapSource)IconPresentation.Normalize(source,ItemKind.Application,true,true)!;
            Assert(icon.PixelWidth==256&&icon.PixelHeight==256,"图标尺寸未归一");
            var pixels=new byte[256*256*4];icon.CopyPixels(pixels,1024,0);int center=(128*256+128)*4,blank=(128*256+30)*4;
            Assert(pixels[center]<40&&pixels[blank]>235&&pixels[blank+3]>235,"白色图标未转换为黑形白底");
            Assert(ReferenceEquals(source,IconPresentation.Normalize(source,ItemKind.RecycleBin,true,true)),"回收站被改动");
            Assert(ReferenceEquals(icon,IconPresentation.Normalize(source,ItemKind.Application,true,true)),"图标规范化未缓存");
        });
        Check("GPT例外外框保持系统深浅色",()=>{
            var source=RedesignChecks.Glyph(System.Windows.Media.Brushes.Red);var a=(System.Windows.Media.Imaging.BitmapSource)IconPresentation.Normalize(source,ItemKind.Application,false,true)!;var b=(System.Windows.Media.Imaging.BitmapSource)IconPresentation.Normalize(source,ItemKind.Application,true,true)!;
            var pixel=new byte[4];a.CopyPixels(new System.Windows.Int32Rect(30,128,1,1),pixel,4,0);Assert(pixel[0]>230,"浅色外框不是白底");b.CopyPixels(new System.Windows.Int32Rect(30,128,1,1),pixel,4,0);Assert(pixel[0]<40,"深色外框不是黑底");
        });
        Check("衬色在用户上限内随背景明暗单调调节，零强度保持关闭", () =>
        {
            double prior = -1;
            for (int i = 0; i <= 100; i++)
            {
                double value = AdaptiveGlassAppearance.TintOpacity(.25, i / 100d);
                Assert(value >= prior && value >= .18 - 1e-8 && value <= .25, "自动衬色越界或方向反了"); prior = value;
                Assert(AdaptiveGlassAppearance.TintOpacity(0, i / 100d) == 0, "零强度仍有自动底色");
            }
            Assert(AdaptiveGlassAppearance.TintOpacity(double.NaN, 1) == 0, "异常强度不安全");
        });
        Check("明暗跟随平滑且不同刷新率下变化一致", () =>
        {
            var a = new AdaptiveGlassAppearance(); var b = new AdaptiveGlassAppearance();
            a.Advance(0, 0); b.Advance(0, 0);
            a.Advance(1, 1d / 60); Assert(a.Luminance > 0 && a.Luminance < .1, "明暗变化直接跳变");
            for (int i = 1; i < 60; i++) a.Advance(1, 1d / 60);
            for (int i = 0; i < 30; i++) b.Advance(1, 1d / 30);
            Assert(Math.Abs(a.Luminance - b.Luminance) < 1e-8, "时间平滑依赖帧率");
        });
        Check("运行点深底变亮浅底变暗且中间亮度不抖动", () =>
        {
            Assert(AdaptiveGlassAppearance.LightIndicator(.1, null), "深底没有亮点");
            Assert(!AdaptiveGlassAppearance.LightIndicator(.9, null), "浅底没有暗点");
            Assert(AdaptiveGlassAppearance.LightIndicator(.52, true) && !AdaptiveGlassAppearance.LightIndicator(.48, false), "中间灰附近反复切换");
        });
        Check("背景采样只取指定区域并正确处理透明像素", () =>
        {
            var pixels = Enumerable.Repeat((byte)255, 40 * 20 * 4).ToArray();
            for (int y = 0; y < 20; y++) for (int x = 0; x < 20; x++) { int p = (y * 40 + x) * 4; pixels[p] = pixels[p + 1] = pixels[p + 2] = 0; }
            var image = System.Windows.Media.Imaging.BitmapSource.Create(40, 20, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 160);
            Assert(AdaptiveGlassAppearance.SampleLuminance(image, new System.Windows.Int32Rect(0, 0, 20, 20)) == 0, "暗色区域受到外部亮区污染");
            Assert(AdaptiveGlassAppearance.SampleLuminance(image, new System.Windows.Int32Rect(20, 0, 20, 20)) > .999, "亮色区域采样错误");
            Array.Clear(pixels);
            image = System.Windows.Media.Imaging.BitmapSource.Create(40, 20, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, pixels, 160);
            Assert(AdaptiveGlassAppearance.SampleLuminance(image, new System.Windows.Int32Rect(0, 0, 40, 20)) == .5, "全透明被当成黑色背景");
        });
        Check("安装版导入设置并复制独立快捷方式", () =>
        {
            string source = Path.Combine(root, "migration-source"), destination = Path.Combine(root, "migration-target");
            Directory.CreateDirectory(Path.Combine(source, "shortcuts"));
            string oldLink = Path.Combine(source, "shortcuts", "owned.lnk"); File.WriteAllText(oldLink, "shortcut-fixture");
            var oldStore = new SettingsService(source);
            oldStore.Save(new DockSettings { Theme = DockTheme.Glass, GlassFrost = .37, GlassDistortion = .82, Pins = new() { new PinnedItem { Name = "中文应用", Target = oldLink, ManagedShortcut = oldLink, Arguments = "--sample 中文" } } });
            string original = File.ReadAllText(oldStore.SettingsPath);
            var current = new SettingsService(destination); Assert(current.TryImportFrom(source), "没有导入");
            var migrated = current.Load(); File.Delete(oldLink);
            Assert(migrated.GlassFrost == .37 && migrated.GlassDistortion == .82 && migrated.Pins[0].Name == "中文应用" && migrated.Pins[0].Arguments == "--sample 中文" && migrated.Pins[0].Target == migrated.Pins[0].ManagedShortcut && File.Exists(migrated.Pins[0].ManagedShortcut), "设置、参数或独立快捷方式丢失");
            Assert(File.ReadAllText(oldStore.SettingsPath) == original, "导入修改了原配置");
        });
        Check("安装升级保留已有用户配置", () =>
        {
            var current = new SettingsService(Path.Combine(root, "existing-profile")); current.Save(new DockSettings { Theme = DockTheme.Dark });
            string before = File.ReadAllText(current.SettingsPath);
            Assert(!current.TryImportFrom(Path.Combine(root, "migration-source")) && File.ReadAllText(current.SettingsPath) == before, "已有配置被覆盖");
        });
        Check("安装导入损坏配置不会破坏源文件", () =>
        {
            string source = Path.Combine(root, "bad-migration"); Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "settings.json"), "{invalid-json");
            var current = new SettingsService(Path.Combine(root, "bad-migration-target"));
            Assert(!current.TryImportFrom(source) && !File.Exists(current.SettingsPath) && File.ReadAllText(Path.Combine(source, "settings.json")) == "{invalid-json", "损坏配置被错误提交");
        });
        Check("玻璃中央区域在不同强度下都保持原比例", () =>
        {
            foreach (double amount in new[] { .1, .5, 1.0 })
                foreach (double x in new[] { 40.0, 80, 100, 130, 160 })
                {
                    var p = GlassRefraction.Offset(x, 30, 200, 60, 85, amount);
                    Assert(p.X == 0 && p.Y == 0 && p.Rim == 0, "玻璃中央仍有缩放或位移");
                }
        });
        Check("轻磨砂和透明度独立保存且不削弱折射参数", () =>
        {
            var configuration = new DockSettings { Theme = DockTheme.Glass, GlassIntensity = .8, GlassDistortion = .6, GlassFrost = .7, GlassOpacity = .4, GlassGrain = .3 };
            var storage = new SettingsService(Path.Combine(root, "glass-material")); storage.Save(configuration);
            var read = storage.Load(); var copy = new DockSettings(); copy.ApplyPreferences(read);
            Assert(copy.GlassIntensity == .8 && copy.GlassDistortion == .6 && copy.GlassFrost == .7 && copy.GlassOpacity == .4 && copy.GlassGrain == .3, "玻璃参数互相覆盖");
            read.GlassOpacity = 0; var invisible = ThemeService.Resolve(read, false, true);
            Assert(invisible.Background.A == 0 && invisible.Border.A == 0 && !invisible.ShadowEnabled, "完全透明时未去除底板");
        });
        Check("设置窗口深浅色独立于底板主题持久化", () =>
        {
            var configuration = new DockSettings { Theme = DockTheme.Glass, InterfaceTheme = InterfaceTheme.Dark };
            var storage = new SettingsService(Path.Combine(root, "interface")); storage.Save(configuration);
            var read = storage.Load(); var copy = new DockSettings(); copy.ApplyPreferences(read);
            Assert(copy.InterfaceTheme == InterfaceTheme.Dark && copy.Theme == DockTheme.Glass, "界面颜色与底板主题耦合");
        });
        Check("玻璃折射强度零时保持原背景采样", () =>
        {
            var p = GlassRefraction.Offset(20, 3, 200, 60, 85, 0);
            Assert(p.X == 0 && p.Y == 0, "零强度仍在扭曲背景");
        });
        Check("玻璃折射在上下边缘方向相反且强度连续增加", () =>
        {
            var upper = GlassRefraction.Offset(100, 6, 200, 60, 85, .4);
            var lower = GlassRefraction.Offset(100, 54, 200, 60, 85, .4);
            var stronger = GlassRefraction.Offset(100, 6, 200, 60, 85, .9);
            Assert(upper.Y < 0 && lower.Y > 0 && Math.Abs(upper.Y + lower.Y) < .001 && Math.Abs(stronger.Y) > Math.Abs(upper.Y), "折射方向或强度错误");
        });
        Check("玻璃真实像素折射保留中心并改变边缘", () =>
        {
            const int w = 200, h = 60, pad = 24, sw = w + pad * 2, sh = h + pad * 2;
            var pixels = new byte[sw * sh * 4];
            for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) { int i = (y * sw + x) * 4; pixels[i] = (byte)(x % 256); pixels[i + 1] = (byte)(y * 2); pixels[i + 3] = 255; }
            var plain = GlassRefraction.Render(pixels, sw, sh, pad, w, h, 85, 0, 1);
            var glass = GlassRefraction.Render(pixels, sw, sh, pad, w, h, 85, 1, 1);
            var a = new byte[w * h * 4]; var b = new byte[a.Length]; plain.CopyPixels(a, w * 4, 0); glass.CopyPixels(b, w * 4, 0);
            int center = (30 * w + 100) * 4, edge = (6 * w + 100) * 4;
            Assert(Math.Abs(a[center] - b[center]) <= 1 && Math.Abs(a[center + 1] - b[center + 1]) <= 1 && Math.Abs(a[edge + 1] - b[edge + 1]) > 3 && glass.IsFrozen, $"中心/边缘采样：{a[center]},{b[center]}; {a[center + 1]},{b[center + 1]}; {a[edge + 1]},{b[edge + 1]}");
            using var imageFile = File.Create(Path.Combine(root, "glass-pixels.png"));
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(glass)); png.Save(imageFile);
        });
        var store = new SettingsService(root);
        var settings = store.Load();
        Check("首次运行提供真实应用与回收站", () => Assert(settings.Pins.Count >= 3 && settings.Pins.Any(p => p.Kind == ItemKind.RecycleBin), "默认项目缺失"));
        Check("配置往返保存中文、顺序、位置", () =>
        {
            settings.Edge = DockEdge.Right; settings.IconSize = 62; settings.AutoHide = true;
            settings.Pins[0].Name = "中文 测试"; store.Save(settings);
            var loaded = store.Load();
            Assert(loaded.Edge == DockEdge.Right && loaded.IconSize == 62 && loaded.AutoHide && loaded.Pins[0].Name == "中文 测试", "往返数据丢失");
        });
        Check("第二次保存原子替换且保留上一版", () =>
        {
            settings.IconSize = 64; store.Save(settings);
            Assert(store.Load().IconSize == 64 && File.Exists(store.SettingsPath + ".bak"), "原子更新失败");
        });
        Check("损坏配置恢复并保留原文", () =>
        {
            File.WriteAllText(store.SettingsPath, "{invalid-json");
            var recovered = store.Load();
            Assert(recovered.Pins.Count > 0 && store.RecoveryNotice != null && Directory.GetFiles(root, "*.invalid-*").Any(f => File.ReadAllText(f) == "{invalid-json"), "未保留损坏配置");
        });
        Check("未知配置版本安全降级", () =>
        {
            File.WriteAllText(store.SettingsPath, "{\"SchemaVersion\":999}");
            Assert(store.Load().Pins.Count > 0, "未恢复默认值");
        });
        Check("配置边界与 NaN 修复", () =>
        {
            var dirty = new DockSettings { IconSize = double.NaN, Spacing = -20, Magnification = 9, Edge = (DockEdge)99, Theme = (DockTheme)99 };
            dirty.Pins.Add(new() { Target = "a", Id = "duplicate" }); dirty.Pins.Add(new() { Target = "b", Id = "duplicate" });
            dirty.Normalize();
            Assert(dirty.IconSize == 48 && dirty.Spacing == 2 && dirty.Magnification == 1.8 && dirty.Edge == DockEdge.Bottom && dirty.Theme == DockTheme.System && dirty.Pins.Select(p => p.Id).Distinct().Count() == 2, "异常配置未归一化");
        });
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string executable = Path.Combine(windows, "explorer.exe");
        Check("路径标识忽略大小写及冗余路径段", () => Assert(LaunchService.Identity(executable) == LaunchService.Identity(Path.Combine(windows, "System32", "..", "EXPLORER.EXE")), "同一应用身份不一致"));
        Check("文件夹可固定", () => Assert(LaunchService.CreatePin(root).Kind == ItemKind.Folder, "文件夹类型错误"));
        Check("普通文档按文件项目固定", () =>
        {
            var path = Path.Combine(root, "note.txt"); File.WriteAllText(path, "test");
            Assert(LaunchService.CreatePin(path).Kind == ItemKind.Document, "普通文档未分类为文件");
        });
        Check("丢失目标给出错误", () =>
        {
            try { LaunchService.CreatePin(Path.Combine(root, "missing.exe")); throw new Exception("未报告目标丢失"); }
            catch (FileNotFoundException) { }
        });
        Check("快捷方式保存真实目标与来源路径", () =>
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            object shortcut = ((dynamic)shell).CreateShortcut(Path.Combine(root, "中文 快捷方式.lnk"));
            try
            {
                ((dynamic)shortcut).TargetPath = executable; ((dynamic)shortcut).Arguments = "/n"; ((dynamic)shortcut).Save();
                var pin = LaunchService.CreatePin(Path.Combine(root, "中文 快捷方式.lnk"));
                Assert(pin.SourceShortcut.EndsWith(".lnk") && LaunchService.Identity(pin.Target) == LaunchService.Identity(executable) && pin.Arguments == "/n", "快捷方式目标或参数丢失");
            }
            finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        });
        Check("通过 Shell 实际启动快捷方式并保留中文参数", () =>
        {
            string marker = Path.Combine(root, "中文 参数 marker.txt");
            string link = Path.Combine(root, "launch-probe.lnk");
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            object shortcut = ((dynamic)shell).CreateShortcut(link);
            try
            {
                ((dynamic)shortcut).TargetPath = Environment.ProcessPath!;
                ((dynamic)shortcut).Arguments = "\"" + typeof(Program).Assembly.Location + "\" --launch-probe \"" + marker + "\"";
                ((dynamic)shortcut).WindowStyle = 7;
                ((dynamic)shortcut).Save();
            }
            finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
            new LaunchService().Launch(LaunchService.CreatePin(link));
            for (int i = 0; i < 50 && !File.Exists(marker); i++) Thread.Sleep(100);
            Assert(File.Exists(marker) && File.ReadAllText(marker) == "launch-ok", "快捷方式参数或 Shell 启动失败");
        });
        using var icons = new IconService();
        Check("删除原快捷方式后仍能独立启动并保留参数", () =>
        {
            string marker = Path.Combine(root, "独立启动 标记.txt"), link = Path.Combine(root, "桌面待删除.lnk");
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            object shortcut = ((dynamic)shell).CreateShortcut(link);
            try
            {
                ((dynamic)shortcut).TargetPath = Environment.ProcessPath!;
                ((dynamic)shortcut).Arguments = "\"" + typeof(Program).Assembly.Location + "\" --launch-probe \"" + marker + "\"";
                ((dynamic)shortcut).WorkingDirectory = root; ((dynamic)shortcut).WindowStyle = 7; ((dynamic)shortcut).Save();
            }
            finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
            var pin = LaunchService.CreatePin(link, Path.Combine(root, "private-shortcuts"));
            File.Delete(link);
            Assert(File.Exists(pin.ManagedShortcut) && pin.WorkingDirectory == root && pin.Arguments.Contains("--launch-probe"), "没有保存独立副本或启动信息");
            new LaunchService().Launch(pin);
            for (int i = 0; i < 50 && !File.Exists(marker); i++) Thread.Sleep(100);
            Assert(File.Exists(marker), "仍然依赖已删除的原快捷方式");
        });
        Check("旧固定项的桌面快捷方式丢失时按程序路径修复", () =>
        {
            var pin = new PinnedItem { Target = Path.Combine(root, "missing-shortcut.lnk"), ExecutablePath = executable, Name = "保留名称" };
            var repaired = LaunchService.MakeIndependent(pin, Path.Combine(root, "private-shortcuts"));
            Assert(repaired.Id == pin.Id && repaired.Name == pin.Name && repaired.Target == executable && repaired.SourceShortcut == pin.Target, "旧固定项修复改变了身份或没有找到程序");
        });
        Check("已保存应用 ID 可独立于桌面快捷方式恢复", () =>
        {
            var pin = new PinnedItem { Target = Path.Combine(root, "store-app.lnk"), AppUserModelId = "Test.Package!App" };
            Assert(LaunchService.MakeIndependent(pin, root).Target == "shell:AppsFolder\\Test.Package!App", "应用ID未保存为独立入口");
        });
        Check("原快捷方式缺失时从真实程序提取图标", () =>
        {
            var pin = new PinnedItem { Target = Path.Combine(root, "missing-icon.lnk"), ExecutablePath = executable };
            var icon = icons.GetAsync(pin).GetAwaiter().GetResult();
            Assert(icon.IsFrozen && !IconService.IsFallback(icon), "没有回退到真实程序图标");
        });
        Check("失败占位图不缓存并可在资源出现后恢复", () =>
        {
            var pin = new PinnedItem { Target = Path.Combine(root, "late-icon.ico"), Kind = ItemKind.Document };
            Assert(IconService.IsFallback(icons.GetAsync(pin).GetAwaiter().GetResult()), "缺失图标没有明确占位");
            using (var stream = File.Create(pin.Target)) System.Drawing.SystemIcons.Information.Save(stream);
            Assert(!IconService.IsFallback(icons.GetAsync(pin).GetAwaiter().GetResult()), "失败占位被永久缓存");
        });
        Check("液态玻璃强度持久化且全透明仍无底板", () =>
        {
            var settingsGlass = new DockSettings { Theme = DockTheme.Glass, GlassIntensity = .8 };
            var copy = settingsGlass.Copy(); var target = new DockSettings(); target.ApplyPreferences(copy);
            Assert(target.Theme == DockTheme.Glass && target.GlassIntensity == .8, "玻璃强度未应用");
            var weak = settingsGlass.Copy(); weak.GlassIntensity = 0;
            Assert(ThemeService.Resolve(settingsGlass, false, true).Background.A == ThemeService.Resolve(weak, false, true).Background.A, "折射强度不应改变底板颜色透明度或高光");
            Assert(GlassRefraction.Offset(100, 4, 200, 60, 85, .8).Y != GlassRefraction.Offset(100, 4, 200, 60, 85, 0).Y, "强度没有影响真实背景偏折");
            settingsGlass.Appearance.Opacity = 0; var visuals = ThemeService.Resolve(settingsGlass, false, true);
            Assert(visuals.Background.A == 0 && !visuals.ShadowEnabled, "全透明时玻璃仍绘制底板");
        });
        Check("实时交换提交固定顺序且不移动回收站", () =>
        {
            var config = new DockSettings { Pins = new() { LaunchService.CreatePin(executable), LaunchService.CreatePin(root) } };
            var orderStore = new SettingsService(Path.Combine(root, "swap-order"));
            var vm = new DockViewModel(config, orderStore, icons);
            var order = vm.DisplayItems.Where(i => i.Pin.Kind != ItemKind.RecycleBin).Select(i => i.Pin.Id).Reverse().ToArray();
            Assert(vm.ApplyPinnedOrder(order), "交换提交失败");
            Assert(vm.DisplayItems.First().Pin.Id == order[0] && vm.DisplayItems.Last().Pin.Kind == ItemKind.RecycleBin, "交换顺序或回收站位置错误");
            Assert(orderStore.Load().Pins.Where(p => p.Kind != ItemKind.RecycleBin).Select(p => p.Id).SequenceEqual(order), "交换结果未持久化");
            Assert(!vm.ApplyPinnedOrder(new[] { "missing" }), "接受了不完整的拖动预览顺序");
        });
        Check("百分之百透明同时移除底座边框和阴影", () =>
        {
            var config = new DockSettings { Theme = DockTheme.Custom, Appearance = new() { Opacity = 0, BorderOpacity = 1, ShadowEnabled = true } };
            foreach (bool transparency in new[] { false, true })
            {
                var visual = ThemeService.Resolve(config, false, transparency);
                Assert(visual.Background.A == 0 && visual.Border.A == 0 && !visual.ShadowEnabled, "全透明仍留有底板装饰");
            }
        });
        Check("最大圆角是胶囊而不是整块椭圆", () =>
        {
            var geometry = DockSurface.CreateGeometry(new System.Windows.Size(300, 80), 100);
            Assert(geometry.FillContains(new System.Windows.Point(80, 2)) && !geometry.FillContains(new System.Windows.Point(1, 1)) && geometry.Bounds.Width == 300 && geometry.Bounds.Height == 80, "最大圆角形状或边界错误");
            var continuous = DockSurface.CreateGeometry(new System.Windows.Size(300, 80), 22);
            Assert(continuous.IsFrozen && continuous.Bounds.Width <= 300 && continuous.Bounds.Height <= 80 && continuous.FillContains(new System.Windows.Point(150, 2)), "连续圆角形状错误");
            Assert(DockSurface.CreateGeometry(new System.Windows.Size(300, 80), 99.9) is not System.Windows.Media.RectangleGeometry, "未到100%就提前变成了半圆");
            Assert(DockSurface.CreateGeometry(new System.Windows.Size(300, 80), 100) is System.Windows.Media.RectangleGeometry, "100%没有收敛到半圆端帽");
        });
        Check("倒影与弹跳配置校验和持久化", () =>
        {
            var config = new DockSettings { ReflectionOpacity = 9, ReflectionLength = -2, ReflectionGap = -1, BounceHeight = 999, BounceDuration = 1 };
            config.Normalize();
            Assert(config.ReflectionOpacity == 1 && config.ReflectionLength == .05 && config.ReflectionGap == 0 && config.BounceHeight == 64 && config.BounceDuration == 350, "动效设置未限制范围");
            var copy = config.Copy(); var target = new DockSettings(); target.ApplyPreferences(copy);
            Assert(target.Reflections && target.BounceHeight == 64 && target.ReflectionLength == .05, "动效设置未应用");
        });
        Check("波浪上浮与底边距离独立保存且保留合理默认", () =>
        {
            var config = new DockSettings();
            Assert(config.HoverLift == 12 && config.EdgeDistance == 6, "波浪和底边距离默认值错误");
            config.HoverLift = 20; config.EdgeDistance = 3;
            var target = new DockSettings(); target.ApplyPreferences(config.Copy());
            Assert(target.HoverLift == 20 && target.EdgeDistance == 3, "偏好复制丢失波浪或距离");
            var waveStore = new SettingsService(Path.Combine(root, "wave-position")); waveStore.Save(target);
            Assert(waveStore.Load().EdgeDistance == 3 && waveStore.Load().HoverLift == 20, "波浪和距离未持久化");
            config.HoverLift = double.NaN; config.EdgeDistance = double.PositiveInfinity; config.Normalize();
            Assert(config.HoverLift == 12 && config.EdgeDistance == 6, "非法偏好未恢复默认");
        });
        Check("常驻区与运行最近区分开并始终以回收站结尾", () =>
        {
            var config = new DockSettings { Pins = new() { LaunchService.CreatePin(root) } };
            var vm = new DockViewModel(config, new SettingsService(Path.Combine(root, "zones")), icons);
            vm.UpdateWindows(new[] { new TrackedWindow(new nint(456), 999, "测试应用", executable) });
            var display = vm.DisplayItems.ToArray();
            Assert(display[0].IsPinned && display[0].Pin.Kind == ItemKind.Folder && !display[1].IsPinned && display[^1].Pin.Kind == ItemKind.RecycleBin, "项目分区顺序错误");
            vm.UpdateWindows(Array.Empty<TrackedWindow>());
            Assert(vm.Items.Any(i => !i.IsPinned && !i.IsRunning), "已关闭应用没有进入最近区");
            config.ShowRecentApps = false; vm.UpdateWindows(Array.Empty<TrackedWindow>());
            Assert(vm.Items.All(i => i.IsPinned), "关闭最近应用后仍保留历史项");
        });
        Check("旧版配置无主题字段时保留用户原设置", () =>
        {
            var migrationStore = new SettingsService(Path.Combine(root, "migration"));
            File.WriteAllText(migrationStore.SettingsPath, "{\"SchemaVersion\":1,\"Theme\":\"Light\",\"IconSize\":32,\"Spacing\":2,\"AutoHide\":true}");
            var migrated = migrationStore.Load();
            Assert(migrated.Theme == DockTheme.Light && migrated.IconSize == 32 && migrated.Spacing == 2 && migrated.AutoHide && migrated.Appearance != null, "迁移丢失原有偏好");
        });
        Check("颜色格式归一化并拒绝无效输入", () =>
        {
            Assert(ThemeAppearance.TryColor(" abc ", out var shortColor) && shortColor == "#AABBCC", "短色号未展开");
            Assert(ThemeAppearance.TryColor("#1234aB", out var color) && color == "#1234AB", "有效色号被拒绝");
            Assert(!ThemeAppearance.TryColor("##123456", out _) && !ThemeAppearance.TryColor("#GG3344", out _) && !ThemeAppearance.TryColor("#12345678", out _), "无效色号被接受");
        });
        Check("主题异常数值不会令底座消失或越界", () =>
        {
            var appearance = new ThemeAppearance { BackgroundColor = "bad-color", Opacity = -1, CornerRadius = 999, BorderThickness = double.NaN, ShadowBlur = double.PositiveInfinity, ShadowOpacity = 9 };
            appearance.Normalize();
            Assert(appearance.BackgroundColor == "#242B38" && appearance.Opacity == 0 && appearance.CornerRadius == 100 && appearance.BorderThickness == 1 && appearance.ShadowBlur == 22 && appearance.ShadowOpacity == .65, "主题数值校验失败");
        });
        Check("自定义颜色透明度边框指示点与阴影生效", () =>
        {
            var custom = new DockSettings { Theme = DockTheme.Custom, Appearance = new() { BackgroundColor = "#173B57", Opacity = .45, BorderColor = "#FF8800", BorderOpacity = .5, IndicatorColor = "#00FFAA", CornerRadius = 30, BorderThickness = 2, ShadowEnabled = false } };
            var visuals = ThemeService.Resolve(custom, false, true);
            Assert(visuals.Background.R == 23 && visuals.Background.G == 59 && visuals.Background.B == 87 && visuals.Background.A == 115 && visuals.Border.A == 128 && visuals.Indicator.G == 255 && visuals.CornerRadius == 30 && visuals.BorderThickness == 2 && !visuals.ShadowEnabled && visuals.Dark, "主题参数未传入实际视觉结果");
        });
        Check("系统关闭透明时纯色回退且不覆盖用户透明度", () =>
        {
            var config = new DockSettings { Theme = DockTheme.Custom, Appearance = new() { Opacity = .45 } };
            var visuals = ThemeService.Resolve(config, false, false);
            Assert(visuals.Background.A == 255 && config.Appearance.Opacity == .45, "降级破坏自定义设置");
        });
        Check("浅深色与系统模式继续工作", () =>
        {
            var config = new DockSettings { Theme = DockTheme.System };
            Assert(ThemeService.Resolve(config, true, true).Dark && !ThemeService.Resolve(config, false, true).Dark, "跟随系统失效");
            config.Theme = DockTheme.Light; Assert(!ThemeService.Resolve(config, true, true).Dark, "浅色被系统深色覆盖");
            config.Theme = DockTheme.Dark; Assert(ThemeService.Resolve(config, false, true).Dark, "深色被系统浅色覆盖");
        });
        Check("预览副本隔离颜色且不共享固定列表容器", () =>
        {
            var original = new DockSettings { Pins = new() { LaunchService.CreatePin(executable) } };
            var draft = original.Copy(); draft.Theme = DockTheme.Custom; draft.Appearance.BackgroundColor = "#ABCDEF"; draft.Pins.Clear();
            Assert(original.Theme == DockTheme.System && original.Appearance.BackgroundColor == "#242B38" && original.Pins.Count == 1, "编辑副本污染正式设置");
        });
        Check("主题保存往返并保留设置期间新增固定项", () =>
        {
            var preferenceStore = new SettingsService(Path.Combine(root, "preferences"));
            var original = new DockSettings { Pins = new() { LaunchService.CreatePin(executable) } };
            var vm = new DockViewModel(original, preferenceStore, icons);
            var draft = original.Copy(); draft.Theme = DockTheme.Custom; draft.Appearance = ThemePresets.Create("海湾蓝");
            vm.Items.Add(new DockItem(LaunchService.CreatePin(root)));
            Assert(vm.TryApplyPreferences(draft), "保存失败");
            var loaded = preferenceStore.Load();
            Assert(loaded.Theme == DockTheme.Custom && loaded.Appearance.BackgroundColor == "#173B57" && loaded.Pins.Count(p => p.Kind != ItemKind.RecycleBin) == 2, "主题或新固定项丢失");
        });
        Check("保存失败保留正式设置和原有磁盘配置", () =>
        {
            var failureStore = new SettingsService(Path.Combine(root, "save-failure"));
            var original = new DockSettings(); failureStore.Save(original);
            var vm = new DockViewModel(original, failureStore, icons); var draft = original.Copy(); draft.Theme = DockTheme.Custom;
            using (new FileStream(failureStore.SettingsPath + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                Assert(!vm.TryApplyPreferences(draft), "文件被锁时仍报告保存成功");
            Assert(original.Theme == DockTheme.System && failureStore.Load().Theme == DockTheme.System, "失败后原配置被修改");
        });
        Check("配色预设各自独立且均为有效颜色", () =>
        {
            foreach (var name in ThemePresets.Names.Skip(1))
            {
                var appearance = ThemePresets.Create(name);
                Assert(ThemeAppearance.TryColor(appearance.BackgroundColor, out _) && ThemeAppearance.TryColor(appearance.IndicatorColor, out _), "预设颜色无效");
                var copy = ThemePresets.Create(name); copy.Opacity = .2; Assert(appearance.Opacity != .2, "预设共享可变对象");
            }
        });
        Check("Shell 图标异步提取且可跨线程访问", () =>
        {
            var icon = icons.GetAsync(LaunchService.CreatePin(executable)).GetAwaiter().GetResult();
            Assert(icon.IsFrozen && icon.Width > 0 && icon.Height > 0, "图标无效或未冻结");
        });
        Check("真实回收站图标提取", () =>
        {
            var icon = icons.GetAsync(new() { Kind = ItemKind.RecycleBin, Target = "shell:RecycleBinFolder" }).GetAwaiter().GetResult();
            Assert(icon.IsFrozen && icon.Width > 0, "回收站图标失败");
        });
        Check("重复添加、拖动排序与重启顺序", () =>
        {
            var vm = new DockViewModel(new DockSettings { ShowRunningApps = false }, new SettingsService(Path.Combine(root, "view-model")), icons);
            vm.AddAsync(new[] { executable, root, executable }).GetAwaiter().GetResult();
            Assert(vm.Items.Count(i => i.Pin.Kind != ItemKind.RecycleBin) == 2, "重复添加未去重");
            vm.Reorder(vm.Items.First(i => i.Pin.Kind == ItemKind.Application), vm.Items.First(i => i.Pin.Kind == ItemKind.Folder));
            var restored = new SettingsService(Path.Combine(root, "view-model")).Load();
            Assert(restored.Pins.First(p => p.Kind != ItemKind.RecycleBin).Kind == ItemKind.Folder && restored.Pins.Count(p => p.Kind != ItemKind.RecycleBin) == 2, "顺序未持久化");
        });
        Check("窗口状态、取消固定、重新固定、退出清理", () =>
        {
            var config = new DockSettings { ShowRunningApps = true, Pins = new() { LaunchService.CreatePin(executable) } };
            var vm = new DockViewModel(config, new SettingsService(Path.Combine(root, "windows")), icons);
            vm.UpdateWindows(new[] { new TrackedWindow(new nint(123), 999, "测试窗口", executable) });
            Assert(vm.Items[0].IsRunning && vm.Items[0].Windows.Count == 1, "运行状态不匹配");
            var applicationItem = vm.Items[0]; vm.Remove(applicationItem); Assert(!applicationItem.IsPinned && config.Pins.Count(p => p.Kind != ItemKind.RecycleBin) == 0, "取消固定删除了运行项或仍被保存");
            vm.Pin(applicationItem); Assert(applicationItem.IsPinned && config.Pins.Count(p => p.Kind != ItemKind.RecycleBin) == 1, "重新固定失败");
            vm.Remove(applicationItem); vm.UpdateWindows(Array.Empty<TrackedWindow>()); Assert(vm.Items.Count(i => i.Pin.Kind != ItemKind.RecycleBin) == 0, "运行项未清理");
        });
        Check("不将文件夹当成 Explorer 运行实例", () =>
        {
            var config = new DockSettings { ShowRunningApps = false, Pins = new() { LaunchService.CreatePin(root) } };
            var vm = new DockViewModel(config, store, icons);
            vm.UpdateWindows(new[] { new TrackedWindow(new nint(123), 999, "Folder", executable) });
            Assert(!vm.Items[0].IsRunning, "文件夹被误标为应用");
        });
        Check("放大曲线连续、对称且边缘回归", () =>
        {
            Assert(DockMath.Scale(0, 2.4, 1.5) == 1.5 && DockMath.Scale(2.4, 2.4, 1.5) == 1, "曲线端点错误");
            double previous = 1.5;
            for (double x = 0; x <= 3; x += .01) { var scale = DockMath.Scale(x, 2.4, 1.5); Assert(scale <= previous + 1e-8 && scale >= 1, "曲线非单调"); previous = scale; }
        });
        Check("动画对不同帧率保持相近时长", () =>
        {
            double a = 1, b = 1;
            for (int i = 0; i < 30; i++) a = DockMath.Step(a, 1.5, 1d / 60);
            for (int i = 0; i < 60; i++) b = DockMath.Step(b, 1.5, 1d / 120);
            Assert(Math.Abs(a - b) < .000001 && a > 1.49, "帧率改变动画时长");
        });
        Check("真实 Windows 窗口枚举排除自身", () =>
        {
            var snapshot = WindowTracker.Snapshot();
            Assert(snapshot.All(w => w.ProcessId != Environment.ProcessId && w.Handle != 0 && w.ExecutablePath.Length > 0), "枚举包含自身或无效窗口");
            Console.WriteLine("INFO Visible application windows: " + snapshot.Count);
        });
        Check("控制中心设置副本隔离、保存与旧数据兼容",()=>
        {
            var settings=new DockSettings();var copy=settings.Copy();copy.ControlCenter.Modules[0].Visible=false;copy.ControlCenter.Modules.Reverse();Assert(settings.ControlCenter.Modules[0].Visible,"配置副本污染正式设置");
            copy.ControlCenter.Hotkey="Ctrl+Alt+C";settings.ApplyPreferences(copy);Assert(settings.ControlCenter.Hotkey=="Ctrl+Alt+C","快捷键未提交");
            var store=new SettingsService(Path.Combine(root,"control-config"));store.Save(settings);var read=store.Load();Assert(read.ControlCenter.Modules[0].Module==copy.ControlCenter.Modules[0].Module,"模块排列未保存");
            var legacy=System.Text.Json.JsonSerializer.Deserialize<DockSettings>("{\"IconSize\":48}")!;legacy.Normalize();Assert(legacy.ControlCenter.Modules.Count==Enum.GetValues<ControlModule>().Length,"旧设置兼容失败");
        });
        Check("四种规格统一网格、缩放比例与任意排列无重叠",()=>
        {
            var options=new ControlCenterOptions();options.Normalize();var geometry=options.Geometry;var circle=ControlCenterLayout.Size(ControlCardShape.Circle,geometry);var capsule=ControlCenterLayout.Size(ControlCardShape.Capsule,geometry);var square=ControlCenterLayout.Size(ControlCardShape.Square,geometry);var rectangle=ControlCenterLayout.Size(ControlCardShape.Rectangle,geometry);
            Assert(circle.Width==circle.Height&&square.Width==square.Height&&capsule.Height==circle.Height&&rectangle.Height==circle.Height,"同规格高度不一致");Assert(Math.Abs(capsule.Width-(2*circle.Width+geometry.Gap))<.001,"网格步长错误");
            geometry.ComponentScale=1.25;var zoom=ControlCenterLayout.Size(ControlCardShape.Capsule,geometry);Assert(Math.Abs(zoom.Width/capsule.Width-1.25)<.000001&&Math.Abs(zoom.Height/capsule.Height-1.25)<.000001,"组件非等比例缩放");
            foreach(var module in new ControlModule?[]{null,ControlModule.Media})foreach(var shape in ControlCenterLayout.Shapes(module))Assert(ControlCenterLayout.Snap(ControlCenterLayout.Size(shape,geometry,module),geometry,module==ControlModule.Media?ControlCardShape.Square:ControlCardShape.Circle,module)==shape,"无法吸附规格 "+shape);
            var random=new Random(42);for(int trial=0;trial<512;trial++){foreach(var module in options.Modules)module.SetShape((ControlCardShape)random.Next(4));var placements=ControlCenterLayout.Arrange(options);for(int i=0;i<placements.Count;i++){Assert(placements[i].Bounds.Right<=(geometry.PanelWidth-36)*geometry.ComponentScale+.001,"超出面板");for(int j=i+1;j<placements.Count;j++)Assert(!placements[i].Bounds.IntersectsWith(placements[j].Bounds),"自动让位后卡片重叠");}options.Modules.Reverse();}
            var copy=options.Copy();copy.Geometry.PanelScale=.8;Assert(options.Geometry.PanelScale==1,"缩放副本污染原配置");
        });
        Check("控制中心覆盖只影响本面板材质",()=>
        {
            var global=new DockSettings{GlassIntensity=.8,GlassFrost=.23};var inherited=global.ControlCenter.Material(global);Assert(inherited.GlassIntensity==.8&&inherited.GlassFrost==.23,"未完整继承");
            global.ControlCenter.OverrideMaterial=true;global.ControlCenter.Frost=.7;var local=global.ControlCenter.Material(global);Assert(local.GlassFrost==.7&&global.GlassFrost==.23,"覆盖修改了 Dock");
        });
        Check("控制中心热键解析与输入校验",()=>
        {
            Assert(ControlCenterEntryService.TryParseHotkey("Ctrl+Win+A",out var modifiers,out var key)&&modifiers==10&&key==65,"默认组合键错误");Assert(!ControlCenterEntryService.TryParseHotkey("A",out _,out _),"允许单键占用");
        });
        Check("媒体时间轴连续推进、暂停与倍速",()=>{
            var p=TimeSpan.FromSeconds(10);var d=TimeSpan.FromSeconds(300);
            Assert(MediaControlService.PositionAt(p,d,true,1,75)==TimeSpan.FromSeconds(85),"long intervals must not freeze at 60 seconds");
            Assert(MediaControlService.PositionAt(p,d,false,1,75)==p,"paused timeline must not drift");
            Assert(MediaControlService.PositionAt(p,d,true,2,3)==TimeSpan.FromSeconds(16),"playback rate must propagate");
            Assert(MediaControlService.PositionAt(p,d,true,1,500)==d,"position clamps at end");
            Assert(MediaControlService.PositionAt(p,TimeSpan.Zero,true,1,10)==TimeSpan.Zero,"unknown duration must not invent progress");
        });
        Check("Wi-Fi 列表只在系统已允许位置访问时读取",()=>{
            Assert(WifiLocationConsent.Evaluate("Allow","Allow","Allow",false),"全部允许时被拒绝");
            Assert(WifiLocationConsent.Evaluate(null,null,null,false),"缺失的值不应视为拒绝");
            Assert(!WifiLocationConsent.Evaluate("Deny","Allow","Allow",false),"定位服务关闭时仍会读取");
            Assert(!WifiLocationConsent.Evaluate("Allow","deny ","Allow",false),"应用位置权限关闭时仍会读取");
            Assert(!WifiLocationConsent.Evaluate("Allow","Allow","Deny",false),"桌面应用位置权限关闭时仍会读取");
            Assert(!WifiLocationConsent.Evaluate("Allow","Allow","Allow",true),"组策略禁用定位时仍会读取");
        });
        Check("界面异常偶发时恢复，连续出错或致命错误时退出",()=>{
            var policy=new UiFaultPolicy(3,60_000);var fault=new InvalidOperationException("fixture");
            Assert(policy.ShouldRecover(fault,0)&&policy.ShouldRecover(fault,1_000),"偶发错误未恢复");
            Assert(!policy.ShouldRecover(fault,2_000),"一分钟内第三次错误仍在恢复");
            Assert(policy.ShouldRecover(fault,200_000),"时间窗口过后未重新允许恢复");
            Assert(!new UiFaultPolicy().ShouldRecover(new OutOfMemoryException(),0),"内存不足仍尝试恢复");
            Assert(UiFaultPolicy.IsFatal(new System.Reflection.TargetInvocationException(new AccessViolationException())),"包装后的致命错误未识别");
        });
        Check("两个音频服务并发读取不再互相冲突",()=>{
            // Read-only: default endpoint volume and the endpoint list, concurrently, many times.
            Exception? failure=null;bool noDevice=false;
            void Loop(Action read){for(int i=0;i<40&&failure is null&&!noDevice;i++){try{read();}catch(System.Runtime.InteropServices.COMException ex)when((uint)ex.HResult==0x80070490){noDevice=true;}catch(Exception ex){failure=ex;}}}
            Parallel.Invoke(()=>Loop(()=>SystemControlService.ReadAudio()),()=>Loop(()=>AudioDeviceService.Outputs()),()=>Loop(()=>SystemControlService.ReadMicrophone()));
            if(failure is not null)throw new Exception("并发读取失败："+failure.GetType().Name+" "+failure.Message);
        });
        Check("低级鼠标钩子运行在独立线程并可干净退出",()=>{
            int calls=0;var hook=new LowLevelMouseHook((_,_,_)=>{Interlocked.Increment(ref calls);return false;},"DockForWindows.TestHook");
            Assert(hook.Active,"钩子未安装");hook.Dispose();Assert(!hook.Active,"释放后钩子仍在");
            hook.Dispose();
        });
        Console.WriteLine($"RESULT {_passed} passed, {_failed} failed");
        Console.WriteLine("Artifacts: " + root);
        return _failed == 0 ? 0 : 1;
    }
    private static int RunFixture()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        using var first = new System.Windows.Forms.Form { Text = "DockForWindows Test A", Width = 440, Height = 220, StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen };
        using var second = new System.Windows.Forms.Form { Text = "DockForWindows Test B", Width = 440, Height = 220, StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen };
        first.Controls.Add(new System.Windows.Forms.Label { Text = "程序坞窗口切换测试 A（无用户数据）", Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
        second.Controls.Add(new System.Windows.Forms.Label { Text = "程序坞窗口切换测试 B（无用户数据）", Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
        first.Show(); second.Show();
        System.Windows.Forms.Application.Run(first);
        return 0;
    }
[DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    private static int RunDropFixture(string file)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        using var window = new System.Windows.Forms.Form { Text = "DockForWindows Drag Source", Width = 360, Height = 220, StartPosition = System.Windows.Forms.FormStartPosition.Manual, Location = new System.Drawing.Point(120, 120) };
        var label = new System.Windows.Forms.Label { Text = "拖动此测试文档到程序坞", Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
        label.MouseDown += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) label.DoDragDrop(new System.Windows.Forms.DataObject(System.Windows.Forms.DataFormats.FileDrop, new[] { file }), System.Windows.Forms.DragDropEffects.Copy); };
        window.Load += (_, _) => window.BeginInvoke(new Action(() => ShowWindow(window.Handle, 5)));
        window.Controls.Add(label); System.Windows.Forms.Application.Run(window); return 0;
    }
}
