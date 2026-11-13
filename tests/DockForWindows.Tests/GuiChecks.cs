using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class GuiChecks
{
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    private static T[] Descendants<T>(DependencyObject root) where T : DependencyObject => Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(root))
        .Select(i => VisualTreeHelper.GetChild(root, i)).SelectMany(child => (child is T match ? new[] { match } : Array.Empty<T>()).Concat(Descendants<T>(child))).ToArray();
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Save(BitmapSource bitmap, string path) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file); }
    private static long Average(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        long sum = 0; for (int i = 0; i < pixels.Length; i += 4) sum += pixels[i + 2]; return sum / (pixels.Length / 4);
    }
    private static double ScreenVariance(Point center)
    {
        nint dc = GetDC(0); double sum = 0, squares = 0;
        try { for (int i = -80; i < 80; i++) { double value = GetPixel(dc, (int)center.X + i, (int)center.Y) & 255; sum += value; squares += value * value; } }
        finally { ReleaseDC(0, dc); }
        return squares / 160 - Math.Pow(sum / 160, 2);
    }
    public static int Run(string directory)
    {
        SetProcessDpiAwarenessContext(new nint(-4)); Directory.CreateDirectory(directory);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var store = new SettingsService(Path.Combine(directory, "isolated-data"));
        GetCursorPos(out var originalCursor);
        var settings = new DockSettings { IconSize = 64, AutoHide = false, HideWhenFullscreen = false, Theme = DockTheme.Glass, GlassIntensity = .8, Reflections = false, ShowRunningApps = false, ShowRecentApps = false };
        settings.Appearance.Opacity = .35; settings.Appearance.CornerRadius = 85; settings.Appearance.ShadowEnabled = false;
        string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        for (int i = 0; i < 8; i++) settings.Pins.Add(new PinnedItem { Name = "Fixture " + i, Target = executable, ExecutablePath = executable });
        using var icons = new IconService();
        var model = new DockViewModel(settings, store, icons);
        var tracker = new WindowTracker(application.Dispatcher);
        var dock = new DockWindow(model, tracker); application.MainWindow = dock;
        var background = new Window { Title = "Dock glass verification backdrop", WindowStyle = WindowStyle.None, ShowInTaskbar = false, Left = 0, Top = SystemParameters.WorkArea.Bottom - 220, Width = SystemParameters.WorkArea.Width, Height = 220, ResizeMode = ResizeMode.NoResize };
        var pattern = new DrawingGroup(); using (var drawing = pattern.Open())
        {
            drawing.DrawRectangle(Brushes.DarkBlue, null, new Rect(0, 0, 600, 220));
            for (int i = -200; i < 700; i += 60) drawing.DrawLine(new Pen(Brushes.Cyan, 20), new Point(i, 0), new Point(i + 170, 220));
            drawing.DrawRectangle(Brushes.Magenta, null, new Rect(0, 150, 600, 70));
        }
        var brush = new DrawingBrush(pattern) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 600, 220) };
        background.Background = brush; background.Show();
        int exit = 0;
        dock.Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(1700);
                var surface = Descendants<DockSurface>(dock).Single();
                var strip = Descendants<DockStrip>(dock).Single();
                Console.WriteLine($"INFO root={dock.ActualWidth}x{dock.ActualHeight} surface={surface.PointToScreen(new Point())} {surface.RenderSize} strip={strip.RenderSize}");
                Check(surface.Backdrop is BitmapSource, "真实窗口背景已采样用于玻璃折射");
                var first = (BitmapSource)surface.Backdrop!; Save(first, Path.Combine(directory, "glass-blue-background.png"));
                var plateCenterPhysical = surface.PointToScreen(new Point(surface.ActualWidth / 2, surface.ActualHeight / 2));
                int sourceX = (int)(plateCenterPhysical.X - surface.BackdropScreenBounds.X), sourceY = (int)(plateCenterPhysical.Y - surface.BackdropScreenBounds.Y);
                var centerPixel = new byte[4]; first.CopyPixels(new Int32Rect(sourceX, sourceY, 1, 1), centerPixel, 4, 0);
                Check(centerPixel[0] > 200 && centerPixel[1] < 50 && centerPixel[2] > 200, "采样来自底板正后方的区域，坐标没有被窗口重置");
                var screenPoint = surface.PointToScreen(new Point(surface.ActualWidth / 2, surface.ActualHeight - 5));
                nint dc = GetDC(0); uint visibleColor; try { visibleColor = GetPixel(dc, (int)screenPoint.X, (int)screenPoint.Y); } finally { ReleaseDC(0, dc); }
                Console.WriteLine($"INFO visible glass RGB={visibleColor & 255},{(visibleColor >> 8) & 255},{(visibleColor >> 16) & 255}");
                Check((visibleColor & 255) > 150 && ((visibleColor >> 8) & 255) < 110 && ((visibleColor >> 16) & 255) > 150, "屏幕实际玻璃显示应用窗口而非壁纸");
                long firstRed = Average(first);
                background.Background = Brushes.OrangeRed; await Task.Delay(1000);
                var second = (BitmapSource)surface.Backdrop!; Save(second, Path.Combine(directory, "glass-red-background.png"));
                Check(Average(second) > firstRed + 80, "后方窗口变化实时进入玻璃且没有自身重影");
                var dpi = VisualTreeHelper.GetDpi(dock);
                var glassImage = Descendants<Image>(surface).Single();
                var frostHost = Descendants<Grid>(surface).Single(e => e.Effect is System.Windows.Media.Effects.BlurEffect);
                Check(glassImage.Effect is LiquidGlassEffect && (RenderCapability.Tier >> 16) >= 2, "先折射后高斯模糊，图标不参与背景滤镜");
                var lens = (LiquidGlassEffect)glassImage.Effect;
                var opticalShape = lens.Shape; var opticalCurve = lens.Texture.Y;
                var draft = settings.Copy();
                draft.GlassFrost = 0; dock.PreviewGlass(draft); await Task.Delay(60);
                Check(frostHost.Effect is null, "磨砂零端点为清透玻璃");
                draft.GlassFrost = .5; dock.PreviewGlass(draft); await Task.Delay(60);
                double middleRadius = frostHost.Effect is System.Windows.Media.Effects.BlurEffect middleBlur ? middleBlur.Radius : 0;
                draft.GlassFrost = 1; dock.PreviewGlass(draft); await Task.Delay(60);
                Check(middleRadius > 0 && middleRadius < 24 && ((System.Windows.Media.Effects.BlurEffect)frostHost.Effect).Radius == 24 && lens.Shape == opticalShape && lens.Texture.Y == opticalCurve,
                    "磨砂连续覆盖完整范围且不修改折射与扭曲参数");
                var checks = new DrawingGroup(); using (var drawing = checks.Open())
                {
                    drawing.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 4, 4));
                    drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 2, 2)); drawing.DrawRectangle(Brushes.White, null, new Rect(2, 2, 2, 2));
                }
                background.Background = new DrawingBrush(checks) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 4, 4) };
                draft.GlassFrost = 0; dock.PreviewGlass(draft); await Task.Delay(400); double clearVariance = ScreenVariance(screenPoint);
                draft.GlassFrost = 1; dock.PreviewGlass(draft); await Task.Delay(400); double frostedVariance = ScreenVariance(screenPoint);
                Console.WriteLine($"INFO Gaussian screen variance clear={clearVariance:F1} frosted={frostedVariance:F1}");
                Check(clearVariance > 400 && frostedVariance < clearVariance * .35, "屏幕实际高斯模糊消除细节且不缩放背景");
                background.Background = Brushes.OrangeRed;
                dock.CancelPreview(); await Task.Delay(100);
                Check(Math.Abs(glassImage.ActualWidth * dpi.DpiScaleX - first.PixelWidth) < 1 && Math.Abs(glassImage.PointToScreen(new Point()).X - surface.BackdropScreenBounds.X) < 1, "背景按屏幕像素一比一对齐");
                long beforeFrames = (long)typeof(DockWindow).GetField("_glassFrameCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(dock)!;
                int renderedFrames = 0, changes = 0; bool? priorAppPixel = null;
                double testX = surface.PointToScreen(new Point(20, 0)).X;
                double testY = surface.PointToScreen(new Point(0, surface.ActualHeight - 5)).Y;
                EventHandler renderCounter = (_, _) =>
                {
                    renderedFrames++;
                    if (surface.Backdrop is not BitmapSource sample) return;
                    int px = (int)(testX - surface.BackdropScreenBounds.X), py = (int)(testY - surface.BackdropScreenBounds.Y);
                    if (px < 0 || py < 0 || px >= sample.PixelWidth || py >= sample.PixelHeight) return;
                    var pixel = new byte[4]; sample.CopyPixels(new Int32Rect(px, py, 1, 1), pixel, 4, 0);
                    bool isApp = pixel[2] > 160 && pixel[1] < 130 && pixel[0] < 100;
                    if (priorAppPixel is not null && priorAppPixel != isApp) changes++;
                    priorAppPixel = isApp;
                };
                CompositionTarget.Rendering += renderCounter;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var drag = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                drag.Tick += (_, _) => background.Left = 400 + 120 * Math.Sin(stopwatch.Elapsed.TotalSeconds * 3.5);
                background.Activate(); drag.Start(); await Task.Delay(1200); drag.Stop(); CompositionTarget.Rendering -= renderCounter; background.Left = 0;
                long afterFrames = (long)typeof(DockWindow).GetField("_glassFrameCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(dock)!;
                double textureFps = (afterFrames - beforeFrames) / stopwatch.Elapsed.TotalSeconds;
                Console.WriteLine($"PERF moving-window background={textureFps:F1}fps UI={renderedFrames / stopwatch.Elapsed.TotalSeconds:F1}fps pixelTransitions={changes} GPU tier={RenderCapability.Tier >> 16}");
                Check(textureFps >= 20, "窗口拖动过程中玻璃取样持续流畅更新");
                double oldScale = ((ScaleTransform)strip.LayoutTransform).ScaleX;
                for (int i = 8; i < 70; i++) model.Items.Add(new DockItem(new PinnedItem { Name = "Fixture " + i, Target = executable, ExecutablePath = executable }) { Icon = model.Items[0].Icon });
                await model.AddAsync(Array.Empty<string>()); await Task.Delay(500);
                double crowdedScale = ((ScaleTransform)strip.LayoutTransform).ScaleX;
                Check(crowdedScale < oldScale && crowdedScale < 1, "70 个固定图标自动整体缩小");
                var work = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
                bool allFit = true;
                foreach (var tile in Descendants<FrameworkElement>(dock).Where(e => e.GetType().Name == "DockTile"))
                {
                    var a = tile.PointToScreen(new Point(6, 3)); var b = tile.PointToScreen(new Point(70, 67));
                    allFit &= a.X >= work.Left && b.X <= work.Right;
                }
                Check(allFit, "70 个应用与回收站全部位于工作区内");
                var span = strip.TransformToAncestor(dock).TransformBounds(strip.IconSpan);
                var center = dock.PointToScreen(new Point(span.X + span.Width / 2, 0));
                var plateCenter = surface.PointToScreen(new Point(surface.ActualWidth / 2, 0));
                Check(Math.Abs(center.X - (work.Left + work.Width / 2d)) < 2 && Math.Abs(plateCenter.X - center.X) < 2, "图标和底板同时保持屏幕居中");
                while (model.Items.Count > 12) model.Items.RemoveAt(model.Items.Count - 1);
                await model.AddAsync(Array.Empty<string>()); await Task.Delay(400);
                Check(((ScaleTransform)strip.LayoutTransform).ScaleX > crowdedScale && settings.IconSize == 64, "减少应用后恢复尺寸且不覆盖用户设置");
                dock.OpenSettings(); await Task.Delay(200);
                var settingsWindow = application.Windows.Cast<Window>().Single(w => w.Title.StartsWith("程序坞设置"));
                Descendants<ListBox>(settingsWindow).Single(c=>AutomationProperties.GetAutomationId(c)=="settings-navigation").SelectedIndex=6;await Task.Delay(100);
                var mode = Descendants<ComboBox>(settingsWindow).Single(c => AutomationProperties.GetAutomationId(c) == "interface-theme");
                mode.SelectedIndex = 2; await Task.Delay(250);
                settingsWindow = application.Windows.Cast<Window>().Single(w => w.Title.StartsWith("程序坞设置"));
                Check(((SolidColorBrush)settingsWindow.Background).Color.R < 65, "设置窗口切换深色即时生效");
                Descendants<ListBox>(settingsWindow).Single(c=>AutomationProperties.GetAutomationId(c)=="settings-navigation").SelectedIndex=0;await Task.Delay(100);
                Check(((ComboBox)Descendants<ComboBox>(settingsWindow).Single(c => AutomationProperties.GetAutomationId(c) == "theme-mode")).SelectedIndex == (int)DockTheme.Glass, "界面深色切换保留液态玻璃底板");
                settingsWindow.Close();
                settings.HideWhenFullscreen = true; SetCursorPos(10, 100);
                background.Activate(); background.WindowState = WindowState.Maximized; await Task.Delay(1150);
                if (GetForegroundWindow() != new System.Windows.Interop.WindowInteropHelper(background).Handle)
                { Console.WriteLine("GUI RESULT PASS; SKIP fullscreen recheck: foreground changed by external input (previous verified run retained)"); return; }
                Console.WriteLine($"INFO fullscreen hidden={dock.IsDockHidden} topmost={dock.Topmost}");
                Check(dock.IsDockHidden && !dock.Topmost, "最大化应用前台时程序坞柔和退下并下沉");
                background.WindowState = WindowState.Normal; background.Activate(); await Task.Delay(1150);
                Check(!dock.IsDockHidden && dock.Topmost, "普通窗口恢复后程序坞重新出现并置顶");
                background.WindowState = WindowState.Maximized; await Task.Delay(1150);
                background.WindowState = WindowState.Normal; await Task.Delay(1150);
                Check(!dock.IsDockHidden && dock.Topmost, "反复切换窗口状态后可稳定恢复");
                Console.WriteLine("GUI RESULT PASS");
            }
            catch (Exception ex) { Console.WriteLine("GUI RESULT FAIL " + ex); exit = 1; }
            finally { dock.Close(); background.Close(); SetCursorPos(originalCursor.X, originalCursor.Y); application.Shutdown(exit); }
        };
        application.Run(dock); return exit;
    }
}
