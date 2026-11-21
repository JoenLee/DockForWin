using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using MediaState = DockForWindows.Services.MediaState;

internal static class WidgetReferenceChecks
{
    static int passed;
    static string output = "";
    static readonly List<object> captures = new();
    static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        passed++; Console.WriteLine("PASS " + name);
    }
    static IEnumerable<T> Tree<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Tree<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    internal static int Run(string folder, bool baseline, bool live = false)
    {
        NativeMethods.SetProcessDpiAwarenessContext(-4);
        output = folder; Directory.CreateDirectory(folder);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            try {
                if (live) await LiveBoard(); else {
                    await Board();
                    if (!baseline) { await States(); DropChecks(); } else ExistingDropBehavior();
                }
                File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new {
                    Passed = passed, Failed = 0, Baseline = baseline,
                    CaptureMethod = "Graphics.CopyFromScreen of visible WPF window", Data = live ? "real local clock, Beijing Open-Meteo, current system media; read-only preview" : "synthetic acceptance fixture",
                    ProcessId = Environment.ProcessId, Assembly = typeof(WidgetHost).Assembly.Location, Captures = captures
                }, new JsonSerializerOptions { WriteIndented = true }));
                app.Shutdown();
            } catch (Exception e) { Console.WriteLine(e); File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); app.Shutdown(1); }
        };
        return app.Run();
    }
    static WidgetRenderData Fixture()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(17, 57, 65), Color.FromRgb(115, 207, 193), 135), null, new Rect(0, 0, 200, 200));
            dc.DrawEllipse(WidgetBrushes.Get("#DDE9DB"), null, new Point(130, 65), 33, 33);
            var mountain = Geometry.Parse("M0,170 L55,100 110,155 165,120 200,160 200,200 0,200 Z");
            dc.DrawGeometry(WidgetBrushes.Get("#173C43"), null, mountain);
        }
        var art = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32); art.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(art));
        using var stream = new MemoryStream(); png.Save(stream);
        var time = new DateTime(2026, 11, 19, 8, 8, 0);
        var weather = new WidgetWeatherSnapshot(24, 2, true, time, new DateTimeOffset(time), new[] { new WidgetForecastDay(DateOnly.FromDateTime(time), 2, 30, 20) });
        return new(new(), new() { City = "杭州" }, weather,
            new MediaState("晚风与星光", "演示艺人 · 视觉验收数据", true, true, true, true, stream.ToArray(), TimeSpan.FromSeconds(88), TimeSpan.FromSeconds(275), true),
            MediaCommand: _ => { }) { Now = time, Seek = _ => { } };
    }
    static Window Window(Canvas canvas, double height = 536)
    {
        var window = new Window {
            Title = "小组件样板 · 隔离视觉验收", Width = 840, Height = height, Left = 36, Top = 60,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            ShowActivated = false, Topmost = true, Content = canvas,
            FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI, Segoe UI")
        };
        window.Show(); return window;
    }
    static WidgetHost Host(Canvas canvas, string id, WidgetSize size, WidgetRenderData data, WidgetMaterial material, double x, double y)
    {
        var definition = RetiredWidgetFixtures.Get(id) with { Size = size };
        var host = new WidgetHost(definition, 2.5);
        host.SetMaterial(material); host.SetContent(WidgetContent.Render(definition, data));
        Canvas.SetLeft(host, x); Canvas.SetTop(host, y); canvas.Children.Add(host); return host;
    }
    static void Caption(Canvas canvas, string text, double x, double y, double size = 12)
    {
        var label = new TextBlock { Text = text, FontSize = size, Foreground = WidgetBrushes.Get("#24383B") };
        Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
    }
    static void Capture(FrameworkElement target, string name)
    {
        var dpi = VisualTreeHelper.GetDpi(target); var origin = target.PointToScreen(new Point());
        int w = (int)Math.Round(target.ActualWidth * dpi.DpiScaleX), h = (int)Math.Round(target.ActualHeight * dpi.DpiScaleY);
        using var image = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(image)) g.CopyFromScreen((int)Math.Round(origin.X), (int)Math.Round(origin.Y), 0, 0, image.Size);
        image.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        captures.Add(new { Name = name, Width = w, Height = h, Dpi = dpi.PixelsPerInchX, X = origin.X, Y = origin.Y });
    }
    static async Task Board()
    {
        var canvas = new Canvas { Background = WidgetBrushes.Get("#D9E2DD") };
        Caption(canvas, "三个 WPF 样板 · 实际屏幕采集", 24, 15, 19);
        Caption(canvas, "固定验收数据：08:08 / 24°C / 1:28 · 未连接真实播放设备", 24, 43);
        var hosts = new List<(WidgetHost Host, string Name)>(); var data = Fixture();
        foreach (var material in new[] { WidgetMaterial.Dark, WidgetMaterial.Light }) {
            double y = material == WidgetMaterial.Dark ? 88 : 320;
            Caption(canvas, material == WidgetMaterial.Dark ? "深色 · 时钟 / 天气 / 播放器" : "浅色 · 相同布局与青绿色主题", 24, y - 24);
            hosts.Add((Host(canvas, "clock-dot", new(2, 2), data, material, 24, y), "clock-" + material));
            hosts.Add((Host(canvas, "weather-dot", new(2, 2), data, material, 228, y), "weather-" + material));
            hosts.Add((Host(canvas, "music-vinyl-wide", new(4, 2), data, material, 432, y), "music-" + material));
        }
        var window = Window(canvas);
        try {
            await Task.Delay(550); window.UpdateLayout();
            foreach (var item in hosts) {
                Capture(item.Host, item.Name); Check(item.Host.IsMeasureValid && item.Host.IsArrangeValid, item.Name + " arranged in running window");
                if (Tree<WidgetSampleWeatherIcon>(item.Host).Any() || Tree<WidgetDotNumber>(item.Host).Any() || item.Name.StartsWith("music")) Bounds(item.Host);
            }
            Capture(canvas, "three-samples-screen");
        } finally { window.Close(); }
    }
    static async Task States()
    {
        var canvas = new Canvas { Background = WidgetBrushes.Get("#D9E2DD") }; var window = Window(canvas, 540);
        try {
            var data = Fixture();
            foreach (var specimen in new[] { ("clock-dot", new WidgetSize(4, 4)), ("clock-dot-wide", new WidgetSize(4, 2)), ("clock-dot-wide", new WidgetSize(6, 2)), ("weather-dot", new WidgetSize(4, 2)), ("music-vinyl", new WidgetSize(2, 2)), ("music-vinyl-wide", new WidgetSize(6, 2)) }) {
                canvas.Children.Clear(); var host = Host(canvas, specimen.Item1, specimen.Item2, data, WidgetMaterial.Dark, 24, 48);
                Caption(canvas, "实际 WPF 尺寸重排 · 固定验收数据 " + specimen.Item1 + " " + specimen.Item2, 24, 15);
                await Task.Delay(120); window.UpdateLayout(); Capture(host, specimen.Item1 + "-" + specimen.Item2.Columns + "x" + specimen.Item2.Rows);
                Check(Tree<FrameworkElement>(host).All(e => double.IsFinite(e.ActualWidth) && double.IsFinite(e.ActualHeight)), "finite layout " + specimen);
            }
            var states = new[] {
                ("unknown", data with { Media = null, Weather = null, Error = "未接入", Source = new() { City = "尚未设置位置" } }),
                ("paused-long", data with { Media = data.Media! with { Title = new string('长', 60), Artist = "长艺人名称 · " + new string('文', 70), Playing = false, CanSeek = false }, Weather = data.Weather! with { Temperature = -22, Code = 71 } }),
                ("midnight", data with { Now = new DateTime(2026, 11, 20, 0, 0, 0), Weather = data.Weather! with { Temperature = 100, Code = 95, IsDay = false } })
            };
            foreach (var state in states) {
                canvas.Children.Clear(); Caption(canvas, "动态状态验收 · " + state.Item1 + "（固定测试数据）", 24, 15);
                var clock = Host(canvas, "clock-dot", new(2, 2), state.Item2, WidgetMaterial.Dark, 24, 48);
                var weather = Host(canvas, "weather-dot", new(2, 2), state.Item2, WidgetMaterial.Dark, 228, 48);
                var player = Host(canvas, "music-vinyl-wide", new(4, 2), state.Item2, WidgetMaterial.Dark, 432, 48);
                await Task.Delay(120); window.UpdateLayout(); Capture(canvas, "state-" + state.Item1);
                Bounds(clock); Bounds(weather); Bounds(player);
                if (state.Item1 is "unknown" or "paused-long") Check(Tree<Slider>(player).All(s => !s.IsEnabled), state.Item1 + " does not invent seek capability");
                foreach (var button in Tree<Button>(player)) Check(button.ActualWidth >= 32 && button.ActualHeight >= 32, "playback hit zone remains usable in " + state.Item1);
                foreach (var text in Tree<TextBlock>(player)) Check(text.TextWrapping == TextWrapping.NoWrap, "long media text remains one bounded line");
            }
            canvas.Children.Clear(); int commands = 0, seeks = 0;
            var active = Host(canvas, "music-vinyl-wide", new(4, 2), data with { MediaCommand = _ => commands++, Seek = _ => seeks++ }, WidgetMaterial.Dark, 24, 48);
            await Task.Delay(120); window.UpdateLayout();
            foreach (var button in Tree<Button>(active).Where(b=>AutomationProperties.GetAutomationId(b) is "widget-music-previous" or "widget-music-toggle" or "widget-music-next")) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(commands == 3, "three real media commands retain separate targets");
            var slider = Tree<Slider>(active).Single(); slider.Value = 140;
            slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Check(seeks == 1, "timeline calls existing seek on commit");
            active.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            await Task.Delay(150); Check(Math.Abs(active.VisualScale - 1) < .001, "hover does not enlarge a widget");
            Check(WidgetGeometry.Get(180, 180, 2.5) == WidgetGeometry.Get(180, 180, 2.5), "existing superellipse geometry remains shared");
        } finally { window.Close(); }
    }
    static void Bounds(WidgetHost host)
    {
        foreach (var leaf in Tree<FrameworkElement>(host).Where(e => e is TextBlock or WidgetDotNumber or WidgetSampleWeatherIcon or Button)) {
            if (!leaf.IsVisible) continue;
            var bounds = leaf.TransformToAncestor(host).TransformBounds(new Rect(leaf.RenderSize));
            Check(bounds.Left >= -.5 && bounds.Top >= -.5 && bounds.Right <= host.ActualWidth + .5 && bounds.Bottom <= host.ActualHeight + .5,
                "text, glyph and hit zone remain inside " + host.Definition.Id);
        }
    }
    static async Task LiveBoard()
    {
        using var media = new MediaControlService(); MediaState? current = null; string? error = null;
        try { await media.Initialize().WaitAsync(TimeSpan.FromSeconds(5)); current = await media.Read().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception e) { error = e.Message; }
        WidgetWeatherSnapshot? weather = null;
        try { weather = await new WidgetWeatherService().ReadAsync(39.9042, 116.4074, default); }
        catch (Exception e) { error = (error == null ? "" : error + " · ") + e.Message; }
        var data = new WidgetRenderData(new(), new() { City = "北京" }, weather, current, Error: error, Preview: true);
        var canvas = new Canvas { Background = WidgetBrushes.Get("#D9E2DD") };
        Caption(canvas, "真实数据 · 实际 WPF 窗口", 24, 16, 19);
        Caption(canvas, "本地时钟 / 北京 Open-Meteo / 当前系统媒体（本预览只读）", 24, 44);
        Host(canvas, "clock-dot", new(2, 2), data, WidgetMaterial.Dark, 24, 88);
        Host(canvas, "weather-dot", new(2, 2), data, WidgetMaterial.Dark, 228, 88);
        Host(canvas, "music-vinyl-wide", new(4, 2), data, WidgetMaterial.Dark, 432, 88);
        var window = Window(canvas, 304);
        window.Top = 240;
        try { await Task.Delay(600); window.UpdateLayout(); Capture(canvas, "live-three-samples-screen"); }
        finally { window.Close(); }
        File.WriteAllText(Path.Combine(output, "live-status.json"), JsonSerializer.Serialize(new { CapturedAt = DateTimeOffset.Now,
            WeatherAvailable = weather != null, WeatherObserved = weather?.ModelTime, MediaSessionAvailable = current != null, Error = error }));
    }
    static void DropChecks()
    {
        var method = typeof(WidgetDesktopLayout).GetMethod("FindDropSpace", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check(method != null, "desktop drop has a specific bounded snapping path");
        foreach (double dpi in new[] { 96d, 144d, 192d }) foreach (double panel in new[] { .75, 1d, 1.25 }) foreach (int columns in new[] { 4, 6 }) {
            var area = new Rect(-4400, 50, 4400, 2200); var item = new WidgetInstance { Size = new(columns, 2), Appearance = new() { PanelScale = panel } };
            double p = WidgetDesktopLayout.Pitch(item, dpi), edge = WidgetDesktopLayout.EdgeInset * dpi / 96;
            double width = item.Size.Width * WidgetDesktopLayout.ContentScale(item) * dpi / 96, height = item.Size.Height * WidgetDesktopLayout.ContentScale(item) * dpi / 96;
            var at = new Point(area.Left + edge + 5 * p, area.Top + edge + 4 * p);
            // A target blocked by an icon still has a free slot in the same row, farther to the left.
            var blocked = new Rect(at.X, at.Y, width, height);
            object?[] args = { item, area, dpi, new[] { blocked }, at, 0, 0 };
            bool found = (bool)method!.Invoke(null, args)!;
            Check(found && (int)args[6]! == 4, $"horizontal drop keeps its row at {dpi} DPI / {panel} / span {columns}");
            foreach (double x in new[] { area.Left - 500, area.Right + 500 }) {
                args = new object?[] { item, area, dpi, Array.Empty<Rect>(), new Point(x, at.Y), 0, 0 };
                found = (bool)method.Invoke(null, args)!;
                double left = area.Left + edge + (int)args[5]! * p;
                Check(found && (int)args[6]! == 4 && left >= area.Left + edge - .01 && left + width <= area.Right - edge + .01, "left/right overshoot snaps inside legal footprint");
            }
            args = new object?[] { item, area, dpi, new[] { area }, at, 0, 0 };
            Check(!(bool)method.Invoke(null, args)!, "full desktop refuses overlap instead of pretending placement succeeded");
        }
    }
    static void ExistingDropBehavior()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!; double dpi = 96;
        var item = new WidgetInstance { Size = new(4, 2) }; double p = WidgetDesktopLayout.Pitch(item, dpi);
        var blocked = new Rect(screen.WorkingArea.Left + 24 + 5 * p, screen.WorkingArea.Top + 24 + 4 * p,
            item.Size.Width * .85, item.Size.Height * .85);
        bool found = WidgetDesktopLayout.FindSpace(item, screen, dpi, new[] { blocked }, out int col, out int row, 5, 4);
        File.WriteAllText(Path.Combine(output, "baseline-drop.json"), JsonSerializer.Serialize(new {
            RequestedColumn = 5, RequestedRow = 4, Found = found, ActualColumn = col, ActualRow = row,
            JumpedToOtherRow = found && row != 4, SameRowFreeColumn = 10,
            Note = "Actual existing FindSpace method; a wide card jumps vertically despite a free horizontal slot."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"REPRO existing requested (5,4) -> ({col},{row})");
    }
}
