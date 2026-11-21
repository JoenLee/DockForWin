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
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class WidgetPlaybackChecks
{
    static int passed, skipped;
    static string output = "";
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static T Find<T>(DependencyObject owner, string id) where T : DependencyObject => WidgetMusicView.Elements<T>(owner).Single(x => AutomationProperties.GetAutomationId(x) == id);
    static IEnumerable<WidgetRecordPlayer> Records(DependencyObject owner) => WidgetMusicView.Elements<WidgetRecordPlayer>(owner);
    internal static int Run(string folder)
    {
        NativeMethods.SetProcessDpiAwarenessContext(-4); output = folder; Directory.CreateDirectory(folder);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            try { await Verify(); File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Passed = passed, Failed = 0, Skipped = skipped, ProcessId = Environment.ProcessId, Assembly = typeof(WidgetHost).Assembly.Location, Capture = "CopyFromScreen", Data = "Explicit acceptance fixtures; audio writes mocked" })); app.Shutdown(); }
            catch (Exception e) { Console.WriteLine(e); File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); app.Shutdown(1); }
        };
        return app.Run();
    }
    static void Screen(FrameworkElement target, string name)
    {
        var at = target.PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(target);
        using var bitmap = new System.Drawing.Bitmap((int)Math.Round(target.ActualWidth * dpi.DpiScaleX), (int)Math.Round(target.ActualHeight * dpi.DpiScaleY));
        using (var g = System.Drawing.Graphics.FromImage(bitmap)) g.CopyFromScreen((int)Math.Round(at.X), (int)Math.Round(at.Y), 0, 0, bitmap.Size);
        bitmap.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
    }
    static async Task Verify()
    {
        var fixture = (WidgetRenderData)typeof(WidgetReferenceChecks).GetMethod("Fixture", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var commands = new List<string>(); double volume = -1; int volumeWrites = 0;
        var data = fixture with { MediaCommand = commands.Add, Audio = new AudioState(38, false), SetVolume = value => { volume = value; volumeWrites++; } };
        var root = new Canvas { Background = WidgetBrushes.Get("#D9E2DD") };
        var caption = new TextBlock { Text = "播放器交互检查 · 固定验收数据 / 音量写入为测试替身", FontSize = 15, Foreground = Brushes.DarkSlateGray };
        Canvas.SetLeft(caption, 24); Canvas.SetTop(caption, 16); root.Children.Add(caption);
        var definition = RetiredWidgetFixtures.Get("music-vinyl-wide"); var host = new WidgetHost(definition, 2.5); host.SetMaterial(WidgetMaterial.Dark); host.SetData(data);
        Canvas.SetLeft(host, 24); Canvas.SetTop(host, 58); root.Children.Add(host);
        var small = new WidgetHost(RetiredWidgetFixtures.Get("music-vinyl"), 2.5); small.SetMaterial(WidgetMaterial.Light); small.SetData(data);
        Canvas.SetLeft(small, 430); Canvas.SetTop(small, 58); root.Children.Add(small);
        var window = new Window { Title = "小组件播放器交互 · 隔离验证", Content = root, Width = 660, Height = 355, Left = 40, Top = 230, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Topmost = true };
        var record = Records(host).Single();
        try {
            window.Show(); await Task.Delay(1000); window.UpdateLayout();
            Check(record.IsSpinning && Math.Abs(record.NeedleAngle - WidgetRecordPlayer.PlayAngle) < .1, "playing inserts the tonearm and rotates the record");
            double first = record.DiscAngle; Screen(root, "playing-a"); await Task.Delay(300); Screen(root, "playing-b");
            Check(Math.Abs(record.DiscAngle - first) > 4, "visible artwork angle changes with real elapsed time");
            var body = WidgetMusicView.Elements<WidgetMusicView>(host).Single(); var oldRoot = VisualTreeHelper.GetChild(body, 0);
            host.SetData(data with { Media = data.Media! with { Position = TimeSpan.FromSeconds(91) } });
            Check(ReferenceEquals(record, Records(host).Single()) && ReferenceEquals(oldRoot, VisualTreeHelper.GetChild(body, 0)), "timeline samples preserve the control tree and disc angle");
            var paused = data with { Media = data.Media! with { Playing = false } }; host.SetData(paused); small.SetData(paused);
            await Task.Delay(900); double stopped = record.DiscAngle; await Task.Delay(220); Screen(root, "paused");
            Check(!record.IsSpinning && Math.Abs(record.DiscAngle - stopped) < .01 && Math.Abs(record.NeedleAngle - WidgetRecordPlayer.ParkAngle) < .1, "pause freezes rotation and parks the tonearm");
            host.SetData(data); await Task.Delay(500); host.SetData(paused); await Task.Delay(70); host.SetData(data); await Task.Delay(1000);
            Check(record.IsSpinning && Math.Abs(record.NeedleAngle - WidgetRecordPlayer.PlayAngle) < .1, "rapid play-pause-play reverses motion safely");
            var next = data with { Media = data.Media! with { Title = "第二首 · 切歌检查", Artwork = data.Media.Artwork!.ToArray() } }; host.SetData(next); await Task.Delay(1600); Screen(root, "track-changed");
            Check(ReferenceEquals(record, Records(host).Single()) && record.IsSpinning && Math.Abs(record.NeedleAngle - WidgetRecordPlayer.PlayAngle) < .1, "track change lifts, replaces art and lowers the same record control");
            foreach (string action in new[] { "previous", "toggle", "next" }) Find<Button>(host, "widget-music-" + action).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Find<Button>(host, "widget-record-needle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(commands.SequenceEqual(new[] { "previous", "toggle", "next", "toggle" }), "transport and tonearm call the current real-command boundary");
            var button = Find<WidgetVolumeButton>(host, "widget-music-volume"); button.Open(); await Task.Delay(180);
            Check(button.IsOpen, "system volume opens an independent popup");
            var slider = (Slider)typeof(WidgetVolumeButton).GetField("_slider", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button)!;
            slider.Value = 61; Check(volume == 61 && volumeWrites == 1, "volume sends only the user value through the audio boundary");
            host.SetData(next with { Audio = new AudioState(61, false) });
            Check(button.IsOpen && ReferenceEquals(record, Records(host).Single()) && volumeWrites == 1, "audio readback preserves popup and does not write the same value back");
            await Task.Delay(180);
            Screen(root, "volume-open"); button.Close(); Check(!button.IsOpen, "volume popup closes cleanly");
            host.SetActive(false); await Task.Delay(150); Check(!record.IsSpinning, "offscreen card releases rendering subscription"); host.SetActive(true); await Task.Delay(250); Check(record.IsSpinning, "visible playing card resumes motion");
            host.SetData(next with { MotionEnabled = false }); await Task.Delay(100); stopped = record.DiscAngle; await Task.Delay(120);
            Check(!record.IsSpinning && Math.Abs(record.DiscAngle - stopped) < .01, "global reduced motion keeps real controls but stops animation");
            var unavailable = next with { Media = null, Audio = null, AudioError = "设备不可用", SetVolume = null, Seek = null, MediaCommand = null }; host.SetData(unavailable); await Task.Delay(200);
            Check(WidgetMusicView.Elements<Button>(host).All(b => !b.IsEnabled) && !Records(host).Single().IsSpinning, "unknown media and audio show no fake working controls");
            foreach (var d in WidgetRegistry.Definitions.Where(d => d.Kind == WidgetKind.Music && FigmaWidgetCatalog.Find(d.Id)==null)) {
                var checkHost = new WidgetHost(d, 2.5); checkHost.SetData(data); Canvas.SetLeft(checkHost, 24); Canvas.SetTop(checkHost, 58);
                root.Children.Clear(); root.Children.Add(checkHost); await Task.Delay(70); window.UpdateLayout();
                var controls = Find<WidgetVolumeButton>(checkHost, "widget-music-volume"); var bounds = controls.TransformToAncestor(checkHost).TransformBounds(new Rect(controls.RenderSize));
                Check(bounds.Left >= -.5 && bounds.Top >= -.5 && bounds.Right <= checkHost.Width + .5 && bounds.Bottom <= checkHost.Height + .5, "volume hit zone fits existing " + d.Id);
            }
        } finally { window.Close(); }
        await Task.Delay(160); Check(!record.IsSpinning, "closing the window releases the record rendering handler");
        try { var state = await Task.Run(SystemControlService.ReadAudio); Check(state.Volume is >= 0 and <= 100, "actual Windows endpoint volume can be read without changing it"); }
        catch (Exception e) { skipped++; Console.WriteLine("SKIP actual audio endpoint: " + e.Message); }
    }
}
