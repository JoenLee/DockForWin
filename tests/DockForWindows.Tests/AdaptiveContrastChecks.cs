using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class AdaptiveContrastChecks
{
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    private static T[] Descendants<T>(DependencyObject parent) where T : DependencyObject => Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(parent))
        .Select(i => VisualTreeHelper.GetChild(parent, i)).SelectMany(c => (c is T v ? new[] { v } : Array.Empty<T>()).Concat(Descendants<T>(c))).ToArray();
    private static void Check(bool valid, string name) { if (!valid) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static int Red(Ellipse dot)
    {
        Point p = dot.PointToScreen(new Point(2, 2)); nint dc = GetDC(0);
        try { return (int)(GetPixel(dc, (int)p.X, (int)p.Y) & 255); } finally { ReleaseDC(0, dc); }
    }
    private static void Capture(Window dock, string file)
    {
        var topLeft = dock.PointToScreen(new Point()); var bottomRight = dock.PointToScreen(new Point(dock.ActualWidth, dock.ActualHeight));
        using var bitmap = new System.Drawing.Bitmap((int)(bottomRight.X - topLeft.X), (int)(bottomRight.Y - topLeft.Y));
        using var g = System.Drawing.Graphics.FromImage(bitmap); g.CopyFromScreen((int)topLeft.X, (int)topLeft.Y, 0, 0, bitmap.Size);
        bitmap.Save(file, System.Drawing.Imaging.ImageFormat.Png);
    }
    public static int Run(string directory)
    {
        SetProcessDpiAwarenessContext(new nint(-4)); Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var settings = new DockSettings { Theme = DockTheme.Glass, IconSize = 56, AutoHide = false, HideWhenFullscreen = false,
            ShowRunningApps = false, ShowRecentApps = false, Reflections = false, GlassTint = .25, GlassFrost = 0, GlassHighlight = 0, GlassSideDarkness = 0 };
        settings.Appearance.Opacity = 1; settings.Appearance.ShadowEnabled = false;
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        for (int i = 0; i < 8; i++) settings.Pins.Add(new PinnedItem { Name = "Contrast fixture " + i, Target = explorer, ExecutablePath = explorer });
        using var icons = new IconService(); var store = new SettingsService(Path.Combine(directory, "isolated-data"));
        var model = new DockViewModel(settings, store, icons); var tracker = new WindowTracker(app.Dispatcher);
        var dock = new DockWindow(model, tracker);
        var background = new Window { Title = "Adaptive contrast test background", WindowStyle = WindowStyle.None, ShowInTaskbar = false,
            Left = 0, Top = SystemParameters.WorkArea.Bottom - 250, Width = SystemParameters.WorkArea.Width, Height = 250,
            ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)) };
        background.Show(); int exit = 0;
        dock.Loaded += async (_, _) =>
        {
            try
            {
                model.Items[0].Windows = new[] { new TrackedWindow((nint)123, 123, "Fixture running", explorer) };
                model.Items[7].Windows = new[] { new TrackedWindow((nint)124, 124, "Fixture running", explorer) };
                await Task.Delay(1600);
                var surface = Descendants<DockSurface>(dock).Single();
                var dots = Descendants<Ellipse>(dock).Where(d => d.Visibility == Visibility.Visible).OrderBy(d => d.PointToScreen(new Point()).X).ToArray();
                Check(dots.Length == 2, "仅打开窗口的应用显示运行点");
                double darkTint = surface.EffectiveGlassTint;
                Check(darkTint >= .18 && darkTint < .19, "暗背景自动减淡黑色衬色且不超过用户上限");
                Check(dots.All(d => ((SolidColorBrush)d.Fill).Color.R >= 230 && Red(d) >= 215), "暗背景运行点在实屏显示亮色");
                Capture(dock, Path.Combine(directory, "01-dark-background.png"));
                background.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                await Task.Delay(90);
                Check(surface.EffectiveGlassTint < .247, "背景突变时衬色平滑跟随而非立即跳变");
                await Task.Delay(1450);
                Check(surface.EffectiveGlassTint > darkTint + .04 && surface.EffectiveGlassTint <= settings.GlassTint, "亮背景加深衬色但始终受用户上限约束");
                Check(dots.All(d => ((SolidColorBrush)d.Fill).Color.R < 40 && Red(d) < 60), "亮背景运行点在实屏显示深色");
                Capture(dock, Path.Combine(directory, "02-light-background.png"));
                var split = new Grid(); split.ColumnDefinitions.Add(new ColumnDefinition()); split.ColumnDefinitions.Add(new ColumnDefinition());
                split.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)) });
                var white = new Border { Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)) }; Grid.SetColumn(white, 1); split.Children.Add(white);
                background.Background = Brushes.Transparent; background.Content = split;
                await Task.Delay(1550);
                Check(((SolidColorBrush)dots[0].Fill).Color.R > 230 && ((SolidColorBrush)dots[1].Fill).Color.R < 40, "同一底板左右明暗不同时运行点各自按局部背景适配");
                Capture(dock, Path.Combine(directory, "03-mixed-background.png"));
                var draft = settings.Copy(); draft.GlassTint = 0; dock.PreviewGlass(draft); await Task.Delay(160);
                Check(surface.EffectiveGlassTint == 0, "用户衬色强度为零时自动衬色仍为零");
                draft.GlassOpacity = 0; draft.Appearance.Opacity = 0; dock.PreviewGlass(draft); await Task.Delay(350);
                Check(surface.Backdrop is not null && ((SolidColorBrush)dots[0].Fill).Color.R > 230 && ((SolidColorBrush)dots[1].Fill).Color.R < 40,
                    "全透明底板仍用同一实时画面适配运行点");
                Capture(dock, Path.Combine(directory, "04-transparent-plate.png"));
                model.Items[0].Windows = Array.Empty<TrackedWindow>(); await Task.Delay(100);
                Check(dots[0].Visibility == Visibility.Hidden, "关闭窗口后运行点正常消失");
                Console.WriteLine("ADAPTIVE CONTRAST RESULT PASS");
            }
            catch (Exception ex) { Console.WriteLine("ADAPTIVE CONTRAST RESULT FAIL " + ex); exit = 1; }
            finally { dock.Close(); background.Close(); app.Shutdown(exit); }
        };
        app.Run(dock); return exit;
    }
}
