using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class WidgetGalleryChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Assembly AppAssembly = typeof(DockSurface).Assembly;
    private static int _passed;
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint value);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extra);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); _passed++; Console.WriteLine("PASS " + message); }
    private static object Field(object owner, string name) {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType) {
            var field = type.GetField(name, Flags | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(owner)!;
        }
        throw new MissingFieldException(name);
    }
    private static object Property(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethod(name, Flags)!.Invoke(owner, args);
    private static IEnumerable<T> Tree<T>(DependencyObject root) where T : DependencyObject {
        if (root is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Tree<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static T Find<T>(DependencyObject root, string id) where T : DependencyObject => Tree<T>(root).Single(item => AutomationProperties.GetAutomationId(item) == id);
    private static void Click(DependencyObject root, string id) => Find<Button>(root, id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Select(object carousel, int index, bool animate = true) => Call(carousel, "Select", index, animate, null);
    private static double Position(object carousel) => (double)Property(carousel, "Position");
    private static int Selection(object carousel) => (int)Property(carousel, "SelectedIndex");
    private static async Task WaitInput(Func<bool> condition,string step)
    {
        var timeout=Stopwatch.StartNew();while(!condition()){
            if(timeout.ElapsedMilliseconds>2000)throw new Exception("Input was not acknowledged: "+step);
            await Task.Delay(15);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
    }
    private static Rect VisibleBounds(object host,Visual ancestor)=>(Rect)Call(host,"VisualBounds",ancestor)!;
    private static void Capture(Window window, string file) {
        var a = window.PointToScreen(new Point()); var b = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
        using var bitmap = new System.Drawing.Bitmap((int)(b.X - a.X), (int)(b.Y - a.Y));
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen((int)a.X, (int)a.Y, 0, 0, bitmap.Size);
        bitmap.Save(file, System.Drawing.Imaging.ImageFormat.Png);
    }
    private static void PureChecks(string directory)
    {
        var options = new DockSettings(); options.Widgets.Events.Add(new() { Title = "原始日程", Start = new DateTime(2028, 2, 29, 9, 0, 0) });
        var copy = options.Copy(); copy.Widgets.Events[0].Title = "草稿"; copy.Widgets.Material = WidgetMaterial.Dark;
        Assert(options.Widgets.Events[0].Title == "原始日程" && options.Widgets.Material == WidgetMaterial.Light, "widget draft is deeply isolated");
        options.ApplyPreferences(copy); Assert(options.Widgets.Events[0].Title == "草稿" && !ReferenceEquals(options.Widgets.Events[0], copy.Widgets.Events[0]), "ApplyPreferences copies widget preferences");
        var invalid = new WidgetOptions { Material = (WidgetMaterial)99, Exponent = double.NaN, WeatherSelection = -10, CalendarSelection = 100, Latitude = double.NaN, Longitude = 0, City = "invalid" };
        invalid.Normalize(); Assert(invalid.Material == WidgetMaterial.Light && invalid.Exponent == 2.5 && invalid.WeatherSelection == 0 && invalid.CalendarSelection == WidgetRegistry.Definitions.Count(d=>d.Kind==WidgetKind.Calendar)-1 && invalid.Latitude == null, "Normalize repairs invalid materials, selections, exponent and location");
        options.Widgets.CardColors["weather-square"]=new(){Background="#abc",Foreground="invalid",Accent="FF00AA"};
        options.Widgets.CardColors["unknown-widget"]=new(){Background="#123456"};options.Normalize();
        Assert(options.Widgets.CardColors["weather-square"].Background=="#AABBCC"&&options.Widgets.CardColors["weather-square"].Foreground==""&&!options.Widgets.CardColors.ContainsKey("unknown-widget"),"card colors normalize and reject invalid/unknown entries");
        var colorCopy=options.Copy();colorCopy.Widgets.CardColors["weather-square"].Background="#112233";
        Assert(options.Widgets.CardColors["weather-square"].Background=="#AABBCC","card palettes are deeply isolated in drafts");
        var typography=new WidgetCardColors{NumberStyle=(WidgetNumberStyle)99,TextWeight=(WidgetTextWeight)99,TextScale=double.NaN,PanelScale=10};typography.Normalize();
        Assert(typography.NumberStyle==WidgetNumberStyle.Standard&&typography.TextWeight==WidgetTextWeight.Automatic&&typography.TextScale==1&&typography.PanelScale==1.25,"typography and panel scales normalize to safe values");
        var store = new SettingsService(Path.Combine(directory, "model-fixture")); store.Save(options); var loaded = store.Load();
        Assert(loaded.Widgets.Events.Single().Title == "草稿" && loaded.Widgets.Material == WidgetMaterial.Dark, "widgets persist through SettingsService");
        Assert(loaded.Widgets.CardColors["weather-square"].Accent=="#FF00AA","per-card palettes persist through SettingsService");
        Assert(Enum.GetValues<WidgetKind>().Length==6&&WidgetRegistry.Definitions.Select(d=>d.Id).Distinct().Count()==WidgetRegistry.Definitions.Count,"six categories share unique registered templates");
        var instance = WidgetRegistry.CreateInstance("weather-forecast", WidgetMaterial.Glass);
        Assert(instance.InstanceId != Guid.Empty && WidgetRegistry.Get(instance.DefinitionId).Size == new WidgetSize(4, 2), "instance contract resolves a registered definition without creating desktop windows");
        var geometryType = AppAssembly.GetType("DockForWindows.Views.WidgetGeometry")!;
        var get = geometryType.GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (double n in new[] { 2.5, 3.0 }) foreach (var size in WidgetRegistry.Definitions.Select(d => d.Size).Distinct()) {
            var geometry = (Geometry)get.Invoke(null, new object[] { size.Width, size.Height, n })!;
            Assert(geometry.IsFrozen && ReferenceEquals(geometry, get.Invoke(null, new object[] { size.Width, size.Height, n })), "frozen geometry cache " + size + " n=" + n);
            double radius = Math.Min(20, Math.Min(size.Width, size.Height) * .19);
            Assert(geometry.FillContains(new Point(radius, 1)) && geometry.FillContains(new Point(1, radius)) &&
                !geometry.FillContains(new Point(.1,.1)) && Math.Abs(geometry.Bounds.Width-size.Width)<.001 && Math.Abs(geometry.Bounds.Height-size.Height)<.001,
                "small continuous corners retain straight card edges " + size + " n=" + n);
        }
        var resist = AppAssembly.GetType("DockForWindows.Views.WidgetCarousel")!.GetMethod("Resist", BindingFlags.Static | BindingFlags.NonPublic)!;
        double first = (double)resist.Invoke(null, new object[] { -100d, 4 })!, last = (double)resist.Invoke(null, new object[] { 100d, 4 })!;
        Assert(first is < 0 and > -.22 && last is > 3 and < 3.22, "bounded drag resistance saturates at both ends");
        var days = (IReadOnlyList<DateTime?>)AppAssembly.GetType("DockForWindows.Views.WidgetContent")!.GetMethod("MonthDays", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { new DateTime(2028, 2, 1) })!;
        Assert(days.Count == 42 && days.Count(d => d.HasValue) == 29 && days[1] == new DateTime(2028, 2, 1), "calendar handles leap years and Monday-first six-row grids");
    }
    private sealed class WeatherFixture : HttpMessageHandler
    {
        internal int Calls; internal bool Block;
        internal const string Forecast = "{\"current\":{\"temperature_2m\":18.5,\"weather_code\":2,\"is_day\":1,\"time\":\"2026-11-11T12:00\"},\"daily\":{\"time\":[\"2026-11-11\",\"2026-11-12\",\"2026-11-13\",\"2026-11-14\",\"2026-11-15\"],\"weather_code\":[2,3,61,0,71],\"temperature_2m_max\":[21,20,19,22,15],\"temperature_2m_min\":[12,10,11,13,7]}}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++; if (Block) await Task.Delay(10000, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.Host.StartsWith("geocoding")
                ? "{\"results\":[{\"name\":\"测试城市\",\"admin1\":\"示例地区\",\"country\":\"测试\",\"latitude\":39.9,\"longitude\":116.4}]}" : Forecast, Encoding.UTF8, "application/json") };
        }
    }
    private static async Task DataChecks()
    {
        using var handler = new WeatherFixture(); using var http = new HttpClient(handler); var service = new WidgetWeatherService(http);
        var locations = await service.SearchAsync("测试", CancellationToken.None); var weather = await service.ReadAsync(39.9, 116.4, CancellationToken.None);
        Assert(locations.Single().Name == "测试城市" && weather.Days.Count == 5 && weather.Temperature == 18.5 && weather.Days[2].Code == 61, "weather parses independent geocoding and forecast responses (fixture)");
        bool rejected = false;
        try { using var broken = JsonDocument.Parse(WeatherFixture.Forecast.Replace("[21,20,19,22,15]", "[21]")); WidgetWeatherService.Parse(broken.RootElement); } catch (FormatException) { rejected = true; }
        Assert(rejected, "incomplete forecasts report failure instead of inventing data");
        rejected = false; try { await service.ReadAsync(double.NaN, 0, CancellationToken.None); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Assert(rejected && handler.Calls == 2, "invalid coordinates never reach the network");
        handler.Block = true; using var cancellation = new CancellationTokenSource(40); bool cancelled = false;
        try { await service.ReadAsync(39.9, 116.4, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        Assert(cancelled, "weather requests support cancellation");
    }
    internal static int LiveWeather(string directory)
    {
        Directory.CreateDirectory(directory);
        try {
            var service = new WidgetWeatherService(); var cities = service.SearchAsync("Beijing", CancellationToken.None).GetAwaiter().GetResult();
            var city = cities.First(); var weather = service.ReadAsync(city.Latitude, city.Longitude, CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(directory, "live-weather.json"), JsonSerializer.Serialize(new { source = "Open-Meteo", city, weather }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS live weather {city} {weather.Temperature}°C {weather.ModelTime:O} {weather.Days.Count} days"); return 0;
        } catch (Exception ex) { Console.Error.WriteLine("LIVE WEATHER UNVERIFIED " + ex.Message); return 1; }
    }
    internal static int Run(string directory, bool preview = false)
    {
        SetProcessDpiAwarenessContext(-4); Directory.CreateDirectory(directory); _passed = 0; int result = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window? window = null; DockWindow? dock = null; WindowTracker? tracker = null; IconService? icons = null;
        app.Startup += async (_, _) => {
            try {
                string? originalStartup = StartupRegistration.ReadCommand();
                if (!preview) { PureChecks(directory); await DataChecks(); }
                icons = new IconService(); var store = new SettingsService(Path.Combine(directory, "isolated-data"));
                var seed = preview && File.Exists(store.SettingsPath) ? store.Load() : new DockSettings { Theme = DockTheme.Glass, InterfaceTheme = InterfaceTheme.Light };
                seed.StartWithWindows = StartupRegistration.ReadCommand() != null;
                var model = new DockViewModel(seed, store, icons);
                tracker = new WindowTracker(app.Dispatcher); dock = new DockWindow(model, tracker);
                var palette = Activator.CreateInstance(AppAssembly.GetType("DockForWindows.Views.Palette")!, new object[] { seed })!;
                window = (Window)Activator.CreateInstance(AppAssembly.GetType("DockForWindows.Views.SettingsWindow")!, new object[] { dock, palette, seed, 9 })!;
                ((CheckBox)Field(window, "_live")).IsChecked = false; window.Topmost = !preview;
                window.Title = preview ? "小组件库 · 独立预览（隔离配置）" : "小组件库 · 交互验证（隔离配置）";
                window.Show(); window.Activate(); await Task.Delay(500);
                if (!preview) SetCursorPos(2, 2);
                if (preview) {
                    Call(window,"ShowSetting","start-with-windows"); await Task.Delay(300);
                    var startup = Find<CheckBox>(window,"start-with-windows"); startup.IsEnabled=false; startup.ToolTip="隔离预览不修改开机启动注册。";
                    Find<ListBox>(window,"settings-navigation").SelectedIndex=2;
                    window.Closed += (_, _) => { dock.Close(); tracker.Dispose(); icons.Dispose(); app.Shutdown(); }; result = 0; return;
                }
                Find<Button>(window, "widget-home-weather").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(350);
                var nav = Find<ListBox>(window, "settings-navigation"); var page = Find<ScrollViewer>(window, "widget-gallery");
                var weather = (FrameworkElement)Property(page, "WeatherCarousel"); var calendar = (FrameworkElement)Property(page, "CalendarCarousel");
                void CalendarInView()=>page.ScrollToVerticalOffset(calendar.TranslatePoint(new Point(),page).Y+page.VerticalOffset-60);
                int weatherLast=WidgetRegistry.Definitions.Count(d=>d.Kind==WidgetKind.Weather)-1;
                var draft = (DockSettings)Field(window, "_draft");
                Assert(page.Content is StackPanel && Tree<FrameworkElement>(page).Count(x => x.GetType().Name == "WidgetCarousel") == 6, "six categories share one vertical page and one reusable carousel type");
                var search = Find<TextBox>(window, "settings-shell-search"); var searchPoint = search.TranslatePoint(new Point(), window);
                Assert(searchPoint.X > 240 && searchPoint.Y < 52 && search.ActualWidth > 100, "global search sits in the top content toolbar");
                search.Text = "天气"; await Task.Delay(50); Assert(nav.SelectedIndex == 2, "global search discovers weather under widgets"); search.Clear();
                Assert(Find<Button>(page, "widget-add-weather").IsEnabled && Find<Button>(page, "widget-add-calendar").IsEnabled, "desktop creation actions are available with an explicit isolated-preview guard");
                Select(weather, 0, false); Select(calendar, 2, false);
                Assert(!(bool)Call(weather, "TryWheel", 120)! && Selection(weather) == 0, "first item never wraps backward");
                page.ScrollToVerticalOffset(80); await Task.Delay(80); double outerOffset = page.VerticalOffset;
                weather.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.MouseWheelEvent });
                await Task.Delay(80);
                Assert(Math.Abs(page.VerticalOffset - outerOffset) < .01 && Selection(weather) == 0, "outward wheel at a boundary stays in the carousel without scrolling the page");
                Assert((bool)Call(weather, "TryWheel", -120)! && Selection(weather) == 1 && Selection(calendar) == 2, "wheel changes only its category");
                Select(weather, weatherLast, false); Assert(!(bool)Call(weather, "TryWheel", -120)! && Selection(weather) == weatherLast, "last item never wraps forward");
                double lastOffset = page.VerticalOffset;
                weather.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent });
                await Task.Delay(70); Assert(page.VerticalOffset == lastOffset && Selection(weather) == weatherLast, "last boundary also consumes the wheel without moving the page");
                var hosts = ((System.Collections.IEnumerable)Property(weather,"Hosts")).Cast<FrameworkElement>().ToArray();
                for(int target=0;target<=weatherLast;target++) {
                    Select(weather,target,false); window.UpdateLayout(); var viewportBounds=(FrameworkElement)Field(weather,"_viewport");
                    var bounds=hosts.Select(h=>VisibleBounds(h,viewportBounds)).ToArray();
                    var gaps=Enumerable.Range(1,bounds.Length-1).Select(i=>bounds[i].Left-bounds[i-1].Right).ToArray();
                    Assert(gaps.All(g=>Math.Abs(g-22)<1),"all card sizes keep a 22 DIP gap within DPI rounding at selection "+target+" ["+string.Join(", ",gaps.Select(g=>g.ToString("0.00")))+"]");
                }
                for (int i = 0; i < 70; i++) { Select(weather, i % 2 == 0 ? weatherLast : 0); if (i % 5 == 0) await Task.Delay(7); }
                Select(weather, 1); await Task.Delay(800); Assert(Math.Abs(Position(weather) - 1) < .000001, "rapid reversals settle at the exact selected position");
                Click(page, "weather-page-3"); await Task.Delay(750); Assert(Selection(weather) == 3 && Math.Abs(Position(weather) - 3) < .000001, "pagination selects a bounded target");
                page.ScrollToTop(); await Task.Delay(120);
                var material = Find<ListBox>(page, "widget-material");
                var host = Find<FrameworkElement>(page, "weather-forecast"); var content = ((Grid)Field(host, "_content")).Children[0]; var outline = (Geometry)Property(host, "Outline");
                Assert(Tree<System.Windows.Shapes.Path>(host).All(p=>p.Stroke==null),"widget surface paths have no card outlines");
                foreach (var index in new[] { 0, 1, 2, 0 }) {
                    material.SelectedIndex = index; await Task.Delay(120);
                    Assert(ReferenceEquals(content, ((Grid)Field(host, "_content")).Children[0]) && ReferenceEquals(outline, Property(host, "Outline")), "material switch preserves content and outline " + index);
                    Assert(ReferenceEquals(((System.Windows.Shapes.Path)Field(host, "_shadow")).Data, ((Grid)Field(host, "_content")).Clip), "shadow and clip share the exact card shape " + index);
                    Capture(window, Path.Combine(directory, "weather-material-" + index + ".png"));
                }
                Find<Expander>(page,"widget-colors-weather").IsExpanded=true;
                await Task.Delay(70);window.UpdateLayout();
                var bg=Find<TextBox>(page,"widget-color-weather-background");var ink=Find<TextBox>(page,"widget-color-weather-foreground");var accent=Find<TextBox>(page,"widget-color-weather-accent");
                var otherHost=Find<FrameworkElement>(page,"weather-square");var otherBackground=otherHost.Resources["WidgetBackground"];
                bg.Text="#1C3550";ink.Text="#FFFFFF";accent.Text="#FFD966";Find<CheckBox>(page,"widget-color-gradient-weather").IsChecked=true;
                Assert(draft.Widgets.CardColors["weather-forecast"].Background=="#1C3550"&&draft.Widgets.CardColors["weather-forecast"].Gradient==true&&!seed.Widgets.CardColors.ContainsKey("weather-forecast"),"HEX and gradient edits stay in the selected card's draft");
                Assert(((SolidColorBrush)host.Resources["WidgetInk"]).Color==Colors.White&&ReferenceEquals(content,((Grid)Field(host,"_content")).Children[0])&&ReferenceEquals(otherBackground,otherHost.Resources["WidgetBackground"]),"custom palette updates one card without rebuilding content or changing neighbors");
                bg.Text="oops";Assert(draft.Widgets.CardColors["weather-forecast"].Background=="#1C3550","invalid colors never replace the last valid draft value");bg.Text="#1C3550";
                Find<ComboBox>(page,"widget-color-preset-weather").SelectedIndex=3;
                Assert(draft.Widgets.CardColors["weather-forecast"].Background=="#F1E3CF","preset applies a complete per-card palette");
                Click(page,"widget-color-reset-weather");Assert(!draft.Widgets.CardColors.ContainsKey("weather-forecast"),"reset restores the selected card's original design colors");
                bg.Text="#1C3550";ink.Text="#FFFFFF";accent.Text="#FFD966";
                Find<CheckBox>(page,"widget-color-gradient-weather").IsChecked=true;
                foreach(var mode in new[]{1,2,0}){material.SelectedIndex=mode;Assert(((SolidColorBrush)host.Resources["WidgetInk"]).Color==Colors.White,"custom palette survives material switch "+mode);
                    if(mode==2){var tint=((System.Windows.Shapes.Path)Field(host,"_tint")).Fill as LinearGradientBrush;Assert(tint!=null&&tint.GradientStops.All(s=>s.Color.A<255)&&((System.Windows.Shapes.Path)Field(host,"_plate")).Fill is ImageBrush,"glass custom gradients retain a transparent tint and the shared background");}}
                Find<ListBox>(page,"widget-appearance-tab-weather").SelectedIndex=1;await Task.Delay(70);window.UpdateLayout();
                Find<ComboBox>(page,"widget-number-style-weather").SelectedIndex=2;
                var weatherViewport=(FrameworkElement)Field(weather,"_viewport");double weatherY=weather.TranslatePoint(new Point(),page).Y+page.VerticalOffset;page.ScrollToVerticalOffset(Math.Max(0,weatherY-70));
                await Task.Delay(160);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                var digits=Tree<FrameworkElement>(host).First(t=>t.GetType().Name=="WidgetText");
                var digitOutline=(Geometry)Property(digits,"OutlineGeometry");Assert(digitOutline.IsFrozen&&!digitOutline.Bounds.IsEmpty,"large outlined digits use a frozen text geometry");
                host.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(350);
                Assert((double)Property(host,"HoverScale")>1.01&&ReferenceEquals(digitOutline,Property(digits,"OutlineGeometry")),"hover gently enlarges the surface without rebuilding text outlines");
                var hoverBounds=hosts.Select(h=>VisibleBounds(h,weatherViewport)).ToArray();Assert(Enumerable.Range(1,hoverBounds.Length-1).All(i=>hoverBounds[i].Left-hoverBounds[i-1].Right>20),"hover keeps neighboring cards separated");
                host.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});await Task.Delay(650);
                Assert(Math.Abs((double)Property(host,"HoverScale")-1)<.000001,"hover leaves return to the original scale");
                Find<Expander>(page,"widget-colors-weather").IsExpanded=false;
                Select(weather,4,false);page.ScrollToVerticalOffset(150);await Task.Delay(150);Capture(window,Path.Combine(directory,"weather-poster.png"));
                Select(weather,5,false);await Task.Delay(100);Capture(window,Path.Combine(directory,"weather-outlook.png"));
                Select(calendar,6,false);CalendarInView();await Task.Delay(120);window.UpdateLayout();
                var clockHost=Find<FrameworkElement>(page,"calendar-clock");Call(page,"RenderCalendar");window.UpdateLayout();
                Assert(Tree<TextBlock>(clockHost).Any(t=>t.Text==DateTime.Now.ToString("HH:mm")),"digital design uses the real local time");
                var combined=Find<FrameworkElement>(page,"calendar-weather");Assert(Tree<TextBlock>(combined).Any(t=>t.Text=="选择天气城市"),"calendar-weather combination never invents unavailable weather");
                Select(calendar,6,false);CalendarInView();await Task.Delay(180);Capture(window,Path.Combine(directory,"calendar-clock.png"));
                Find<Expander>(page,"widget-colors-calendar").IsExpanded=true;await Task.Delay(70);window.UpdateLayout();Find<ListBox>(page,"widget-appearance-tab-calendar").SelectedIndex=1;await Task.Delay(70);window.UpdateLayout();
                var numberStyle=Find<ComboBox>(page,"widget-number-style-calendar");var weight=Find<ComboBox>(page,"widget-text-weight-calendar");
                numberStyle.SelectedIndex=1;weight.SelectedIndex=3;Find<Slider>(page,"widget-text-scale-calendar").Value=110;Find<Slider>(page,"widget-panel-scale-calendar").Value=115;
                var clockText=Tree<TextBlock>(clockHost).First(t=>t.Text.Contains(':')&&t.FontSize>30);
                bool roundLoaded=new Typeface(clockText.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal).TryGetGlyphTypeface(out var roundFace);
                Assert(roundLoaded&&roundFace!=null&&roundFace.FontUri.ToString().Contains("varela",StringComparison.OrdinalIgnoreCase),"rounded digits load the embedded licensed font");
                Assert(clockText.FontWeight==FontWeights.Bold&&Math.Abs(((Control)Tree<FrameworkElement>(clockHost).First(t=>t.GetType().Name=="WidgetText")).FontSize-83.6)<.01&&clockText.FontSize<=83.61&&Math.Abs((double)Property(clockHost,"PanelScale")-1.15)<.001,"per-card weight, text size and panel size update together");
                numberStyle.SelectedIndex=2;double calendarY=calendar.TranslatePoint(new Point(),page).Y+page.VerticalOffset;page.ScrollToVerticalOffset(Math.Max(0,calendarY-70));await Task.Delay(160);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                Capture(window,Path.Combine(directory,"clock-outline.png"));
                numberStyle.SelectedIndex=3;weight.SelectedIndex=1;Find<Slider>(page,"widget-text-scale-calendar").Value=90;
                clockText=Tree<TextBlock>(clockHost).First(t=>t.Text.Contains(':')&&t.FontSize>30);
                Assert(clockText.FontFamily.Source=="Georgia"&&clockText.FontWeight==FontWeights.Light,"artistic digits and light text are selectable");
                Find<ListBox>(page,"widget-appearance-tab-calendar").SelectedIndex=0;await Task.Delay(70);window.UpdateLayout();Find<ComboBox>(page,"widget-color-preset-calendar").SelectedIndex=4;
                Assert(draft.Widgets.CardColors["calendar-clock"].NumberStyle==WidgetNumberStyle.Art&&draft.Widgets.CardColors["calendar-clock"].PanelScale==1.15,"changing a color preset preserves typography and panel size");
                Find<Expander>(page,"widget-colors-calendar").IsExpanded=false;
                page.GetType().GetField("_lastCalendarMinute",Flags)!.SetValue(page,-1L);Call(page,"OnTick",null,EventArgs.Empty);
                Assert((long)Field(page,"_lastCalendarMinute")==DateTime.Now.Ticks/TimeSpan.TicksPerMinute,"one shared minute timer refreshes visible clock and calendar designs");
                Select(calendar,5,false);Assert((bool)Call(page,"WeatherNeeded")!,"visible calendar-weather design participates in the shared weather refresh");
                Select(calendar,4,false);await Task.Delay(100);Capture(window,Path.Combine(directory,"calendar-poster.png"));
                Select(weather,3,false);Select(calendar,2,false);page.ScrollToTop();
                var exponent = Find<ComboBox>(page, "widget-exponent"); exponent.SelectedIndex = 1; Assert(draft.Widgets.Exponent == 3 && !ReferenceEquals(outline, Property(host, "Outline")), "exponent switches cached mathematical outlines"); exponent.SelectedIndex = 0;
                Select(weather, 1, false); page.ScrollToVerticalOffset(160); await Task.Delay(180);
                var viewport = (FrameworkElement)Field(weather, "_viewport");
                var start = viewport.PointToScreen(new Point(viewport.ActualWidth / 2, viewport.ActualHeight / 2));
                GetCursorPos(out var savedCursor);
                try {
                    SetCursorPos((int)start.X, (int)start.Y);await Task.Delay(80); mouse_event(0x0002, 0, 0, 0, 0); await WaitInput(()=>(bool)Field(weather,"_pressed"),"first mouse down");
                    SetCursorPos((int)start.X - 95, (int)start.Y); await WaitInput(()=>(bool)Property(weather,"IsDragging"),"horizontal capture");
                    Assert((bool)Property(weather, "IsDragging") && Mouse.Captured == weather, "real horizontal mouse drag acquires capture");
                    double forward = Position(weather); SetCursorPos((int)start.X + 55, (int)start.Y); await Task.Delay(90);
                    Assert(Position(weather) < forward, "drag reverses continuously before release");
                    mouse_event(0x0004, 0, 0, 0, 0); await Task.Delay(800);
                    Assert(Mouse.Captured != weather && Math.Abs(Position(weather) - Selection(weather)) < .000001, "release frees capture and snaps to the nearest valid item");
                    Select(weather, 3); await Task.Delay(70);
                    var previousStart=start;start=viewport.PointToScreen(new Point(viewport.ActualWidth/2,viewport.ActualHeight/2));
                    Console.WriteLine($"INPUT viewport center {previousStart}->{start}; scroll={page.VerticalOffset:0.00}");
                    SetCursorPos((int)start.X, (int)start.Y);await Task.Delay(80); mouse_event(0x0002, 0, 0, 0, 0); await WaitInput(()=>(bool)Field(weather,"_pressed"),"interrupting mouse down");
                    double pressedPosition = Position(weather); await Task.Delay(70);
                    if(Math.Abs(Position(weather)-pressedPosition)>=.000001)Capture(window,Path.Combine(directory,"input-freeze-failure.png"));
                    Assert(Math.Abs(Position(weather) - pressedPosition) < .000001, $"pressing during animation freezes at the current position before drag starts (pressed={Field(weather,"_pressed")},dragging={Property(weather,"IsDragging")},position={pressedPosition:0.0000}->{Position(weather):0.0000},mouse={Mouse.GetPosition(viewport)},scroll={page.VerticalOffset:0.00})");
                    SetCursorPos((int)start.X - 65, (int)start.Y); await Task.Delay(70); Mouse.Capture(null);
                    mouse_event(0x0004, 0, 0, 0, 0); await Task.Delay(800);
                    Assert(!(bool)Property(weather, "IsDragging") && Mouse.Captured != weather && Math.Abs(Position(weather) - Selection(weather)) < .000001, "lost capture cancels the gesture and settles cleanly");
                } finally { mouse_event(0x0004, 0, 0, 0, 0); SetCursorPos(savedCursor.X, savedCursor.Y); }
                CalendarInView(); await Task.Delay(160);
                Find<Expander>(page, "widget-calendar-editor").IsExpanded = true; await Task.Delay(80); CalendarInView(); await Task.Delay(120);
                Find<TextBox>(page, "widget-event-title").Text = "原型验证日程"; Find<TextBox>(page, "widget-event-time").Text = "bad"; Click(page, "widget-event-add");
                Assert(draft.Widgets.Events.Count == 0, "invalid local event times are rejected");
                Find<TextBox>(page, "widget-event-time").Text = "14:30"; Click(page, "widget-event-add");
                Assert(draft.Widgets.Events.Count == 1 && seed.Widgets.Events.Count == 0, "local calendar edits remain isolated in the settings draft");
                Select(calendar, 3, false); page.ScrollToVerticalOffset(page.ScrollableHeight - 190); await Task.Delay(220); Capture(window, Path.Combine(directory, "calendar-planner.png"));
                Click(page, "widget-month-next"); Click(page, "widget-month-previous"); Click(page, "widget-month-today");
                Assert((DateTime)Field(page, "_selected") == DateTime.Today, "month navigation returns to the real current date");
                Assert(!File.Exists(store.SettingsPath), "preview interactions never write the profile before save");
                Click(window, "save-settings"); await Task.Delay(150); Assert(store.Load().Widgets.Events.Single().Title == "原型验证日程", "original settings save chain persists widget edits to the isolated profile");
                Assert(store.Load().Widgets.CardColors["weather-forecast"].Background=="#1C3550","original save chain persists per-card custom colors");
                Assert(store.Load().Widgets.CardColors["calendar-clock"].NumberStyle==WidgetNumberStyle.Art&&store.Load().Widgets.CardColors["calendar-clock"].PanelScale==1.15,"font and panel preferences persist through the original save chain");
                Assert(StartupRegistration.ReadCommand() == originalStartup, "isolated saves preserve the exact existing startup registration");
                window.Width = 760; window.Height = 540; page.ScrollToTop(); Select(weather, 3, false); await Task.Delay(200);
                var selectedBounds = VisibleBounds(host,viewport);
                Assert(selectedBounds.Left >= -1 && selectedBounds.Right <= viewport.ActualWidth + 1, "largest widget remains complete at minimum window width");
                Capture(window, Path.Combine(directory, "minimum-window.png"));
                window.Width = 1040; window.Height = 780; page.ScrollToVerticalOffset(170); await Task.Delay(180);
                using var process = Process.GetCurrentProcess(); process.Refresh(); long memoryBefore = process.PrivateMemorySize64;
                var cpuBefore = process.TotalProcessorTime; var ui = process.Threads.Cast<ProcessThread>().Single(t => t.Id == GetCurrentThreadId()); var uiBefore = ui.TotalProcessorTime;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) { Select(weather, i % 4); await Task.Delay(30); }
                await Task.Delay(700); watch.Stop(); process.Refresh();
                double cpu = (process.TotalProcessorTime - cpuBefore).TotalSeconds / watch.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
                double uiCpu = (ui.TotalProcessorTime - uiBefore).TotalSeconds / watch.Elapsed.TotalSeconds * 100;
                nav.SelectedIndex = 0; await Task.Delay(320); Assert(!(bool)Property(page, "IsUpdating"), "leaving the gallery stops its single timer");
                nav.SelectedIndex = 2; await Task.Delay(320); Assert((bool)Property(page, "IsUpdating"), "returning reuses the gallery and restarts one timer");
                for (int i = 0; i < 16; i++) { nav.SelectedIndex = i % 2 == 0 ? 0 : 2; await Task.Delay(10); }
                await Task.Delay(350); Assert(((Panel)Field(window, "_pages")).Children.Count == 1, "rapid navigation leaves one active page");
                Capture(window, Path.Combine(directory, "gallery-final.png"));
                double dpi = VisualTreeHelper.GetDpi(window).DpiScaleX; long privateAfter = process.PrivateMemorySize64, workingSet = process.WorkingSet64;
                window.Close(); await Task.Delay(120);
                Assert(!(bool)Property(page, "IsUpdating") && Mouse.Captured != weather, "closing releases timer and mouse capture after WPF unload dispatch");
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = _passed, dpi,
                    samplingSeconds = watch.Elapsed.TotalSeconds, processCpuPercent = cpu, uiThreadCorePercent = uiCpu, privateBytesBefore = memoryBefore,
                    privateBytesAfter = privateAfter, workingSetBytes = workingSet,
                    actualPresentedFps = (double?)null, presentedFrameMeanMs = (double?)null, presentedFrameP95Ms = (double?)null, presentedFrameP99Ms = (double?)null, gpuPercent = (double?)null,
                    notes = "WPF real mouse + isolated save checks. CPU is interval average; no forced GC. No rendering callback rate is used as presented FPS." }, new JsonSerializerOptions { WriteIndented = true }));
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); result = 1; }
            finally { if (!preview || result != 0) { window?.Close(); dock?.Close(); tracker?.Dispose(); icons?.Dispose(); app.Shutdown(result); } }
        };
        app.Run(); return result;
    }
    internal static int Measure(string directory)
    {
        SetProcessDpiAwarenessContext(-4); Directory.CreateDirectory(directory); int result = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var icons = new IconService(); var seed = new DockSettings { Theme = DockTheme.Glass, InterfaceTheme = InterfaceTheme.Light, StartWithWindows = StartupRegistration.ReadCommand() != null };
        var model = new DockViewModel(seed, new SettingsService(Path.Combine(directory, "isolated-data")), icons);
        using var tracker = new WindowTracker(app.Dispatcher); var dock = new DockWindow(model, tracker); Window? window = null;
        app.Startup += async (_, _) => {
            try {
                var palette = Activator.CreateInstance(AppAssembly.GetType("DockForWindows.Views.Palette")!, new object[] { seed })!;
                window = (Window)Activator.CreateInstance(AppAssembly.GetType("DockForWindows.Views.SettingsWindow")!, new object[] { dock, palette, seed, 9 })!;
                ((CheckBox)Field(window, "_live")).IsChecked = false; window.Topmost = true; window.Show(); window.Activate(); SetCursorPos(2, 2);
                await Task.Delay(400);
                var gallery = Find<ScrollViewer>(window, "widget-gallery"); var weather = (FrameworkElement)Property(gallery, "WeatherCarousel");
                var calendar = (FrameworkElement)Property(gallery, "CalendarCarousel");
                var live = Path.Combine(Path.GetDirectoryName(directory)!, "live-weather", "live-weather.json");
                if (File.Exists(live)) {
                    using var json = JsonDocument.Parse(File.ReadAllText(live));
                    ((DockSettings)Field(window, "_draft")).Widgets.City = json.RootElement.GetProperty("city").GetProperty("Name").GetString()!;
                    gallery.GetType().GetField("_weather", Flags)!.SetValue(gallery, JsonSerializer.Deserialize<WidgetWeatherSnapshot>(json.RootElement.GetProperty("weather")));
                    Call(gallery, "RenderWeather");Call(gallery,"RenderCalendar"); ((TextBlock)Field(gallery, "_weatherStatus")).Text = "性能验证使用已获取的 Open-Meteo 真实天气快照；网络请求不计入动画场景。";
                }
                Find<Expander>(gallery, "widget-city-editor").IsExpanded = false;
                File.WriteAllText(Path.Combine(directory, "target.pid"), Environment.ProcessId.ToString());
                for (int i = 0; i < 240 && !File.Exists(Path.Combine(directory, "start.signal")); i++) await Task.Delay(250);
                if (!File.Exists(Path.Combine(directory, "start.signal"))) throw new TimeoutException("No profiling start signal within 60 seconds.");
                var scenarios = new List<object>();
                using var process = Process.GetCurrentProcess();
                foreach (var materialIndex in new[] { 0, 1, 2 }) {
                    Find<ListBox>(gallery, "widget-material").SelectedIndex = materialIndex; Select(weather, 3, false);
                    gallery.ScrollToVerticalOffset(112); await Task.Delay(400);
                    Capture(window, Path.Combine(directory, $"weather-live-{materialIndex}.png"));
                    process.Refresh(); long privateBefore = process.PrivateMemorySize64; var cpu = process.TotalProcessorTime;
                    using var ui = process.Threads.Cast<ProcessThread>().Single(t => t.Id == GetCurrentThreadId()); var uiCpu = ui.TotalProcessorTime;
                    long startQpc = Stopwatch.GetTimestamp(); var watch = Stopwatch.StartNew();
                    for (int i = 0; i < 100; i++) { Select(weather, i % 4); await Task.Delay(150); }
                    await Task.Delay(700); watch.Stop(); process.Refresh(); long endQpc = Stopwatch.GetTimestamp();
                    scenarios.Add(new { material = ((WidgetMaterial)materialIndex).ToString(), startQpc, endQpc, durationSeconds = watch.Elapsed.TotalSeconds,
                        cpuPercent = (process.TotalProcessorTime - cpu).TotalSeconds / watch.Elapsed.TotalSeconds / Environment.ProcessorCount * 100,
                        uiThreadCorePercent = (ui.TotalProcessorTime - uiCpu).TotalSeconds / watch.Elapsed.TotalSeconds * 100,
                        privateBytesBefore = privateBefore, privateBytesAfter = process.PrivateMemorySize64, workingSetBytes = process.WorkingSet64 });
                    Console.WriteLine("MEASURED " + ((WidgetMaterial)materialIndex));
                }
                Find<ListBox>(gallery, "widget-material").SelectedIndex = 0; Select(calendar, 3, false);
                Find<Expander>(gallery, "widget-calendar-editor").IsExpanded = false; gallery.ScrollToBottom(); await Task.Delay(250);
                Capture(window, Path.Combine(directory, "calendar-final.png"));
                Find<ListBox>(window, "settings-navigation").SelectedIndex = 0; await Task.Delay(350); Capture(window, Path.Combine(directory, "dashboard-final.png"));
                File.WriteAllText(Path.Combine(directory, "scenarios.json"), JsonSerializer.Serialize(new { pid = Environment.ProcessId, qpcFrequency = Stopwatch.Frequency, scenarios,
                    notes = "Three 15-second rapid-interruption scenes; average process/UI CPU and memory. Presented FPS is derived separately from PID-scoped ETW, never from WPF callbacks." }, new JsonSerializerOptions { WriteIndented = true }));
                result = 0;
            } catch (Exception ex) { Console.Error.WriteLine("MEASURE FAIL " + ex); }
            finally { window?.Close(); dock.Close(); await Task.Delay(120); app.Shutdown(result); }
        };
        app.Run(); return result;
    }
}

