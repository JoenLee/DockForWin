using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Views;

internal static class GlassDeskOpticsChecks
{
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    private static void Check(bool valid, string message)
    { if (!valid) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static (int R, int G, int B) Pixel(Point point)
    {
        var dc = GetDC(0);
        try { uint p = GetPixel(dc, (int)point.X, (int)point.Y); return ((int)(p & 255), (int)(p >> 8 & 255), (int)(p >> 16 & 255)); }
        finally { ReleaseDC(0, dc); }
    }
    private static void Capture(DockSurface surface, string path)
    {
        var a = surface.PointToScreen(new Point());
        var b = surface.PointToScreen(new Point(surface.ActualWidth, surface.ActualHeight));
        using var bitmap = new System.Drawing.Bitmap((int)Math.Round(b.X - a.X), (int)Math.Round(b.Y - a.Y));
        using var drawing = System.Drawing.Graphics.FromImage(bitmap);
        drawing.CopyFromScreen((int)a.X, (int)a.Y, 0, 0, bitmap.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    public static int Run(string directory)
    {
        SetProcessDpiAwarenessContext(new nint(-4)); Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var canvas = new Canvas();
        var surface = new DockSurface { Width = 600, Height = 120, Fill = Brushes.White, StrokeWidth = 0,
            UseGlass = true, Radius = 85, GlassOpacity = 1, GlassIntensity = 0, GlassDistortion = .65,
            GlassFrost = 0, GlassHighlight = 0, GlassSideDarkness = 0, GlassTint = 0 };
        Canvas.SetLeft(surface, 50); Canvas.SetTop(surface, 40); canvas.Children.Add(surface);
        var window = new Window { Title = "GlassDesk lens optical verification", WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false, Topmost = true,
            Width = 700, Height = 200, Left = 100, Top = 150, Background = Brushes.DimGray, Content = canvas };
        int exit = 0;
        window.Loaded += async (_, _) =>
        {
            try
            {
                Check((RenderCapability.Tier >> 16) >= 2, "硬件 GPU 着色器可用");
                var dpi = VisualTreeHelper.GetDpi(surface).DpiScaleX;
                var origin = surface.PointToScreen(new Point());
                int pad = (int)Math.Ceiling(32 * dpi), width = (int)Math.Round(600 * dpi) + pad * 2,
                    height = (int)Math.Round(120 * dpi) + pad * 2;
                var pixels = new byte[width * height * 4];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    pixels[i] = (byte)Math.Round(60 + (y + .5) / dpi * .3);
                    pixels[i + 1] = (byte)Math.Clamp(Math.Round(20 + (y + .5) / dpi * 1.2), 0, 255);
                    pixels[i + 2] = (byte)Math.Round(128 + 90 * Math.Sin((x + .5) / dpi * .04));
                    pixels[i + 3] = 255;
                }
                surface.BackdropScreenBounds = new Rect(origin.X - pad, origin.Y - pad, width, height);
                surface.Backdrop = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
                await Task.Delay(350);
                Point At(double x, double y) => surface.PointToScreen(new Point(x, y));
                double SourceGreen(Point point, double offsetDip = 0) =>
                    Math.Clamp(20 + (((int)point.Y + .5 - origin.Y + pad) / dpi + offsetDip) * 1.2, 0, 255);
                var top = At(300, 4);
                int unbent = Pixel(top).G;
                Check(Math.Abs(unbent - SourceGreen(top)) <= 3, "折射强度零端点按背景原坐标显示");
                Capture(surface, Path.Combine(directory, "01-zero-refraction.png"));
                surface.GlassIntensity = 1; surface.InvalidateVisual(); await Task.Delay(250);
                foreach (double depthDip in new[] { 4d, 8d, 16d, 28d })
                {
                    var point = At(300, depthDip);
                    double actualDepth = ((int)point.Y + .5 - origin.Y) / dpi;
                    double t = Math.Clamp(1 - actualDepth / 24, 0, 1);
                    double offset = (1 - Math.Sqrt(1 - t * t)) * 32;
                    int measured = Pixel(point).G;
                    double expected = SourceGreen(point, offset);
                    Console.WriteLine($"OPTICS depth={actualDepth:F2} expectedG={expected:F2} actualG={measured} offsetDip={offset:F3}");
                    Check(Math.Abs(measured - expected) <= 4, $"距上沿 {depthDip} DIP 的实屏位移符合 GlassDesk 透镜曲线");
                }
                int bent = Pixel(top).G;
                Check(bent > unbent + 12, "玻璃强度改变真实背景取样位置而非高光亮度");
                Check(Math.Abs(Pixel(At(300, 60)).G - SourceGreen(At(300, 60))) <= 3, "底板中心保持一比一且无整体缩放");
                var bottom = At(300, 116);
                double bottomDepth = 120 - ((int)bottom.Y + .5 - origin.Y) / dpi;
                double bottomT = Math.Clamp(1 - bottomDepth / 24, 0, 1);
                double bottomOffset = (1 - Math.Sqrt(1 - bottomT * bottomT)) * 32;
                Check(Math.Abs(Pixel(bottom).G - SourceGreen(bottom, -bottomOffset)) <= 4, "下沿沿相反法线折射且保持屏幕坐标");
                Capture(surface, Path.Combine(directory, "02-glassdesk-lens.png"));
                surface.GlassIntensity = .25; surface.InvalidateVisual(); await Task.Delay(180);
                int weak = Pixel(top).G;
                Check(weak > unbent + 2 && weak < bent - 7, "现有折射强度滑块连续控制实际位移");
                surface.GlassIntensity = 1; surface.GlassDistortion = 0; surface.InvalidateVisual(); await Task.Delay(180);
                Check(Math.Abs(Pixel(top).G - unbent) <= 2, "现有扭曲度零端点关闭透镜位移");
                surface.GlassDistortion = .65; surface.GlassFrost = .8; surface.InvalidateVisual(); await Task.Delay(220);
                Capture(surface, Path.Combine(directory, "03-gaussian-over-lens.png"));
                surface.GlassFrost = 0; surface.GlassHighlight = .75; surface.GlassSideDarkness = .8; surface.GlassTint = .06;
                foreach (double radius in new[] { 0d, 85d, 100d })
                {
                    surface.Radius = radius; surface.InvalidateVisual(); await Task.Delay(150);
                    Capture(surface, Path.Combine(directory, $"04-corners-{radius}.png"));
                }
                surface.GlassOpacity = 0; surface.InvalidateVisual(); await Task.Delay(150);
                var transparent = Pixel(At(300, 60));
                Check(Math.Abs(transparent.R - 105) <= 2 && Math.Abs(transparent.G - 105) <= 2, "透明度零端点移除底板与灯光");
                Console.WriteLine("GLASSDESK OPTICS RESULT PASS");
            }
            catch (Exception ex) { Console.WriteLine("GLASSDESK OPTICS RESULT FAIL " + ex); exit = 1; }
            finally { window.Close(); app.Shutdown(exit); }
        };
        app.Run(window); return exit;
    }
}
