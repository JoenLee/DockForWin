using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class WindowControlAlignmentChecks
{
    internal static int Run(string output)
    {
        NativeMethods.SetProcessDpiAwarenessContext(-4);
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            try {
                string[] labels = { "−", "+", "×" };
                string[] fills = { "#F7D789", "#9CD591", "#F6A099" }, inks = { "#93600C", "#276C28", "#A32E29" };
                int count = 0;
                foreach (double scale in new[] { 1d, 1.25, 1.5, 1.6, 1.75, 2 }) for (int i = 0; i < 3; i++) {
                    var glyph = new WindowControlGlyph(labels[i], WidgetBrushes.Get(inks[i]), WidgetBrushes.Get(fills[i]));
                    glyph.Measure(new Size(28, 28)); glyph.Arrange(new Rect(0, 0, 28, 28)); glyph.UpdateLayout();
                    int size = (int)Math.Ceiling(28 * scale);
                    var bitmap = new RenderTargetBitmap(size, size, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(glyph);
                    Point circle = Center(bitmap, WidgetBrushes.Get(fills[i]).Color), symbol = Center(bitmap, WidgetBrushes.Get(inks[i]).Color, WidgetBrushes.Get(fills[i]).Color);
                    if (Math.Abs(circle.X - symbol.X) > .55 || Math.Abs(circle.Y - symbol.Y) > .55)
                        throw new Exception($"{labels[i]} at {scale}: circle {circle}, symbol {symbol}");
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, $"button-{i}-{scale:0.00}.png")); encoder.Save(stream);
                    count++; Console.WriteLine($"PASS {labels[i]} at {scale:P0}: circle {circle}, symbol {symbol}");
                }
                var window = new SettingsShellWindow(new DockSettings { InterfaceTheme = InterfaceTheme.Light }, (_, _) => { }, 0) { Topmost = true };
                window.Show(); await Task.Delay(450); window.UpdateLayout();
                var corner = window.PointToScreen(new Point(window.ActualWidth - 126, 4)); var dpi = VisualTreeHelper.GetDpi(window);
                using (var screenshot = new System.Drawing.Bitmap((int)(126 * dpi.DpiScaleX), (int)(44 * dpi.DpiScaleY))) {
                    using var graphics = System.Drawing.Graphics.FromImage(screenshot);
                    graphics.CopyFromScreen((int)corner.X, (int)corner.Y, 0, 0, screenshot.Size);
                    screenshot.Save(Path.Combine(output, "live-controls.png"));
                }
                window.ClosePermanently();
                File.WriteAllText(Path.Combine(output, "result.json"), $"{{\"Passed\":{count},\"Failed\":0,\"MaximumCenterDifferencePixels\":0.55}}"); app.Shutdown();
            } catch (Exception error) { Console.WriteLine(error); app.Shutdown(1); }
        };
        return app.Run();
    }

    private static Point Center(BitmapSource bitmap, Color color, Color? background = null)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        int left = bitmap.PixelWidth, top = bitmap.PixelHeight, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.PixelHeight; y++) for (int x = 0; x < bitmap.PixelWidth; x++) {
            int p = (y * bitmap.PixelWidth + x) * 4;
            if (pixels[p + 3] < 254) continue;
            if (background is Color fill) {
                if (Math.Pow(x + .5 - bitmap.PixelWidth / 2d, 2) + Math.Pow(y + .5 - bitmap.PixelHeight / 2d, 2) > Math.Pow(7 * bitmap.DpiX / 96, 2)) continue;
                double db = fill.B - color.B, dg = fill.G - color.G, dr = fill.R - color.R;
                double coverage = ((fill.B - pixels[p]) * db + (fill.G - pixels[p + 1]) * dg + (fill.R - pixels[p + 2]) * dr) / (db * db + dg * dg + dr * dr);
                if (coverage < .5) continue;
            } else if (Math.Abs(pixels[p] - color.B) > 4 || Math.Abs(pixels[p + 1] - color.G) > 4 || Math.Abs(pixels[p + 2] - color.R) > 4) continue;
            left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        if (right < left) throw new Exception("Rendered color missing: " + color);
        return new Point((left + right + 1) / 2d, (top + bottom + 1) / 2d);
    }
}
