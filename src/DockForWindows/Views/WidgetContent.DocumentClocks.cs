using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static partial class WidgetContent
{
    private static FrameworkElement DocumentClock(WidgetDefinition definition, WidgetRenderData data, DocumentClockStyle style)
        => new DocumentClockView(data, style);
}

// Coordinates are measured in the supplied 336px square / 702x336px wide reference.
// A single uniform Viewbox maps them into the existing widget grid; no screen-space offsets.
internal sealed class DocumentClockView : Grid
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private readonly WidgetRenderData _data;
    private readonly DocumentClockStyle _style;
    private readonly Canvas _frame;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<Action<DateTimeOffset>> _updates = new();
    internal bool TimerRunning => _timer.IsEnabled;
    internal Canvas ReferenceFrame => _frame;
    internal DocumentClockView(WidgetRenderData data, DocumentClockStyle style)
    {
        _data = data; _style = style;
        _frame = new Canvas { Width = style.Wide ? 702 : 336, Height = 336 };
        Children.Add(new Viewbox { Child = _frame, Stretch = Stretch.Uniform });
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        switch (style.Content) {
            case DocumentClockContent.Analog: Analog(); break;
            case DocumentClockContent.Digital: Digital(); break;
            case DocumentClockContent.Alarm: Alarm(); break;
            case DocumentClockContent.World: World(); break;
            case DocumentClockContent.City: City(); break;
        }
        _timer.Tick += Tick;
        Loaded += (_, _) => { Refresh(); UpdateTimer(); };
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => UpdateTimer();
        Refresh();
    }
    private DateTimeOffset UtcNow => _data.Now is DateTime fixture
        ? new DateTimeOffset(DateTime.SpecifyKind(fixture, DateTimeKind.Utc)) : DateTimeOffset.UtcNow;
    private void Tick(object? sender, EventArgs e) => Refresh();
    private void Refresh() { var now = UtcNow; foreach (var update in _updates) update(now); }
    private void UpdateTimer() { if (IsLoaded && IsVisible && !_data.Now.HasValue) _timer.Start(); else _timer.Stop(); }
    private TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(_data.Source.TimeZone);
    private static DateTimeOffset In(DateTimeOffset time, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(time, zone);
    private void Place(FrameworkElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); _frame.Children.Add(element); }
    private TextBlock Text(string id, double x, double y, double width, double size, string? color = null,
        FontWeight? weight = null, TextAlignment align = TextAlignment.Left)
    {
        var label = new TextBlock { FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"), FontSize = size,
            FontWeight = weight ?? FontWeights.Normal, Width = width, TextAlignment = align,
            TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        if (color == null) label.SetResourceReference(TextBlock.ForegroundProperty, "WidgetInk"); else label.Foreground = WidgetBrushes.Get(color);
        AutomationProperties.SetAutomationId(label, id); Place(label, x, y); return label;
    }
    private void Analog()
    {
        var face = new DocumentClockDial(Zone, _style.Id.EndsWith("midnight"), 336, true, false, _data.Now, _data.MotionEnabled);
        Place(face, 0, 0); _updates.Add(face.RefreshTime);
        var city = Text("reference-clock-city", 60, 213, 216, 12.5, "#7B7B84", FontWeights.Bold, TextAlignment.Center);
        city.Text = CityName(Zone).ToUpperInvariant();
    }
    private void Digital()
    {
        var day = Text("reference-clock-weekday", 34, 29, 268, 21, "#3B587E", FontWeights.Bold);
        var time = Text("reference-clock-time", 29, 62, 290, 112, weight: FontWeights.Bold);
        time.RenderTransform = new ScaleTransform(.94, 1);
        var date = Text("reference-clock-date", 34, 209, 268, 29.5);
        var city = Text("reference-clock-location", 34, 262, 268, 20, "#4D6C8E");
        _updates.Add(utc => { var local = In(utc, Zone); day.Text = local.ToString("dddd", English).ToUpperInvariant();
            time.Text = local.ToString("H:mm", English); date.Text = local.ToString("MMMM d", English);
            city.Text = CityName(Zone) + " · " + ZoneLabel(Zone, local); });
    }
    private void Alarm()
    {
        Text("reference-alarm-heading", 34, 29, 268, 21, "#934E22", FontWeights.Bold).Text = "NEXT ALARM";
        var time = Text("reference-alarm-time", 24.5, 69, 232, 98, weight: FontWeights.Bold);
        var period = Text("reference-alarm-period", 249, 146, 60, 23, "#934E22", FontWeights.Bold);
        var day = Text("reference-alarm-day", 34, 208, 268, 28);
        var remaining = Text("reference-alarm-remaining", 34, 259, 268, 20, "#934E22");
        // Windows Clock exposes no public read API for its next alarm. Only a clearly identified gallery fixture shows 7:00.
        if (_data.Preview) {
            ToolTip = "设计示例预览；7:00 不代表系统已设置闹钟。";
            time.Text = "7:00"; period.Text = "AM"; day.Text = "Tomorrow";
            _updates.Add(utc => { var local = In(utc, Zone); var left = local.Date.AddDays(1).AddHours(7) - local.DateTime.AddSeconds(-local.Second).AddMilliseconds(-local.Millisecond);
                remaining.Text = $"In {(int)left.TotalHours} hr, {left.Minutes} min"; });
        } else {
            time.Text = "—:—"; day.Text = "未接入系统闹钟"; remaining.Text = "点击打开 Windows 时钟";
            ToolTip = "系统不提供读取下次闹钟的接口；此卡片不会伪造闹钟或设置成功状态。";
        }
        var open = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Width = 336, Height = 336,
            Cursor = System.Windows.Input.Cursors.Hand, Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}'/></ControlTemplate>") };
        AutomationProperties.SetAutomationId(open, "reference-alarm-open"); AutomationProperties.SetName(open, "打开 Windows 时钟");
        open.Click += (_, _) => { if (_data.Preview) return; try { Process.Start(new ProcessStartInfo("ms-clock:") { UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(error.Message, "无法打开 Windows 时钟"); } }; Place(open, 0, 0);
    }
    private void World()
    {
        var zones = _data.Source.WorldZones.Take(4).Select(TimeZoneInfo.FindSystemTimeZoneById).ToArray();
        double pitch = 688d / Math.Max(1, zones.Length);
        for (int i = 0; i < zones.Length; i++) {
            var zone = zones[i]; double center = 7 + pitch * (i + .5);
            Text("reference-world-city-" + i, center - 75, 27, 150, 21, "#B4B4BD", FontWeights.Bold, TextAlignment.Center).Text = CityCode(zone);
            var dial = new DocumentClockDial(zone, false, 136, false, false, _data.Now, _data.MotionEnabled, true); Place(dial, center - 68, 85); _updates.Add(dial.RefreshTime);
            var time = Text("reference-world-time-" + i, center - 78, 232, 156, 33, align: TextAlignment.Center);
            var day = Text("reference-world-day-" + i, center - 78, 280, 156, 20, "#B4B4BD", align: TextAlignment.Center);
            _updates.Add(utc => { var local = In(utc, zone); time.Text = local.ToString("H:mm", English);
                int delta = (local.Date - In(utc, Zone).Date).Days; day.Text = delta == 0 ? "Today" : delta == 1 ? "Tomorrow" : delta == -1 ? "Yesterday" : local.ToString("MMM d", English); });
        }
    }
    private void City()
    {
        Text("reference-city-heading", 37, 36, 350, 20, "#726C61", FontWeights.Bold).Text = CityName(Zone).ToUpperInvariant();
        var time = Text("reference-city-time", 25, 68, 370, 94, weight: FontWeights.Bold);
        time.RenderTransform = new ScaleTransform(.914, 1);
        var date = Text("reference-city-date", 37, 204, 362, 24.4, "#454D56");
        var offset = Text("reference-city-offset", 37, 268, 370, 20.5, "#726C61");
        var dial = new DocumentClockDial(Zone, true, 230, false, true, _data.Now, _data.MotionEnabled); Place(dial, 435, 31); _updates.Add(dial.RefreshTime);
        var zoneLabel = Text("reference-city-zone", 434, 278, 230, 19, "#726C61", FontWeights.Bold, TextAlignment.Center);
        _updates.Add(utc => { var local = In(utc, Zone); time.Text = local.ToString("H:mm", English);
            date.Text = local.ToString("dddd, MMMM d", English); zoneLabel.Text = ZoneLabel(Zone, local);
            var pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            double hours = (local.Offset - In(utc, pacific).Offset).TotalHours;
            offset.Text = hours == 0 ? "Same time as Cupertino" : $"{Math.Abs(hours):0.#} hours {(hours > 0 ? "ahead of" : "behind")} Cupertino"; });
    }
    internal static string CityName(TimeZoneInfo zone) => zone.Id switch {
        "Pacific Standard Time" => "Cupertino", "Eastern Standard Time" => "New York", "GMT Standard Time" => "London",
        "Tokyo Standard Time" => "Tokyo", "China Standard Time" => "Beijing", "UTC" => "UTC", _ => zone.StandardName };
    internal static string CityCode(TimeZoneInfo zone) => zone.Id switch {
        "Pacific Standard Time" => "SFO", "Eastern Standard Time" => "NYC", "GMT Standard Time" => "LON", "Tokyo Standard Time" => "TYO",
        "China Standard Time" => "BJS", "UTC" => "UTC", _ => CityName(zone).ToUpperInvariant() };
    internal static string ZoneLabel(TimeZoneInfo zone, DateTimeOffset local) => zone.Id switch {
        "Pacific Standard Time" => zone.IsDaylightSavingTime(local) ? "PDT" : "PST", "Eastern Standard Time" => zone.IsDaylightSavingTime(local) ? "EDT" : "EST",
        "GMT Standard Time" => zone.IsDaylightSavingTime(local) ? "BST" : "GMT", "Tokyo Standard Time" => "JST", "China Standard Time" => "CST", "UTC" => "UTC", _ => local.ToString("zzz", English) };
}

internal sealed class DocumentClockDial : Grid
{
    private readonly TimeZoneInfo _zone;
    private readonly DateTime? _fixture;
    private readonly RotateTransform _hour = new(), _minute = new(), _second = new();
    private readonly bool _motion, _world, _dark, _city;
    private readonly ReferenceDialFace _face;
    internal bool Animating => _second.HasAnimatedProperties;
    internal double SecondAngle => _second.Angle;
    internal DocumentClockDial(TimeZoneInfo zone, bool dark, double size, bool numbers, bool city,
        DateTime? fixture, bool motion, bool world = false)
    {
        _zone = zone; _fixture = fixture; _motion = motion; _world = world; _dark = dark; _city = city; Width = Height = size;
        var frame = new Grid { Width = 336, Height = 336 };
        _face = new ReferenceDialFace(dark, numbers, city, world); frame.Children.Add(_face);
        double hour = world ? 74 : city ? 79 : 71, minute = world ? 119 : city ? 119 : 111;
        AddHand(frame, _hour, hour, world ? 11 : city ? 9 : 9, false);
        AddHand(frame, _minute, minute, world ? 8 : city ? 6 : 6, false);
        if (!world) AddHand(frame, _second, city ? 126 : 124, city ? 2.4 : 2.5, true);
        var dot = new Ellipse { Width = world ? 19 : 14, Height = world ? 19 : 14, Fill = WidgetBrushes.Get(world || city ? "#FFAC53" : dark ? "#F5F5F7" : "#19191C"), IsHitTestVisible = false };
        frame.Children.Add(dot);
        if (!world && !city) frame.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = WidgetBrushes.Get("#FF8200"), IsHitTestVisible = false });
        Children.Add(new Viewbox { Child = frame, Stretch = Stretch.Uniform } );
        Loaded += (_, _) => Start(); Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => { if (IsLoaded && IsVisible) Start(); else Stop(); };
        Start();
    }
    private void AddHand(Grid frame, RotateTransform rotate, double length, double width, bool second)
    {
        var layer = new Canvas { Width = 336, Height = 336, RenderTransform = rotate, RenderTransformOrigin = new Point(.5, .5), IsHitTestVisible = false };
        var line = new Line { X1 = 168, X2 = 168, Y1 = 168 + (second ? 11 : 5), Y2 = 168 - length,
            StrokeThickness = width, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        if (second) line.Stroke = WidgetBrushes.Get(_city ? "#FFAC53" : "#FF8200");
        else if (_city || _world) line.Stroke = WidgetBrushes.Get(_dark || _city ? "#F5F5F7" : "#19191C");
        else line.SetResourceReference(Shape.StrokeProperty, "WidgetInk");
        layer.Children.Add(line); frame.Children.Add(layer);
    }
    internal void RefreshWorld(DateTimeOffset utc)
    {
        if (!_world) return;
        var local = TimeZoneInfo.ConvertTime(utc, _zone); bool night = local.Hour < 6 || local.Hour >= 17;
        _face.Night = night;
        foreach (var line in Descendants<Line>(this)) line.Stroke = WidgetBrushes.Get(night ? "#F5F5F7" : "#19191C");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
        if (root is T found) yield return found;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    internal void RefreshTime(DateTimeOffset utc)
    {
        RefreshWorld(utc); // Re-anchor to Windows time after clock corrections and resume.
        double seconds = TimeZoneInfo.ConvertTime(utc, _zone).TimeOfDay.TotalSeconds;
        Set(_hour, seconds / 120, 43200); Set(_minute, seconds / 10, 3600); Set(_second, seconds * 6, 60);
    }
    private void Start()
    {
        var utc = _fixture is DateTime fixture ? new DateTimeOffset(DateTime.SpecifyKind(fixture, DateTimeKind.Utc)) : DateTimeOffset.UtcNow;
        var local = TimeZoneInfo.ConvertTime(utc, _zone); RefreshWorld(utc); double seconds = local.TimeOfDay.TotalSeconds;
        Set(_hour, seconds / 120, 43200); Set(_minute, seconds / 10, 3600); Set(_second, seconds * 6, 60);
    }
    private void Set(RotateTransform target, double angle, double duration)
    {
        target.BeginAnimation(RotateTransform.AngleProperty, null); target.Angle = angle % 360;
        if (IsLoaded && IsVisible && !_fixture.HasValue && _motion)
            target.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle % 360, angle % 360 + 360, TimeSpan.FromSeconds(duration)) { RepeatBehavior = RepeatBehavior.Forever });
    }
    private void Stop() { foreach (var hand in new[] { _hour, _minute, _second }) hand.BeginAnimation(RotateTransform.AngleProperty, null); }
}

internal sealed class ReferenceDialFace : FrameworkElement
{
    private readonly bool _dark, _numbers, _city, _world;
    private bool _night;
    internal bool Night { set { if (_night == value) return; _night = value; InvalidateVisual(); } }
    internal ReferenceDialFace(bool dark, bool numbers, bool city, bool world) { _dark = dark; _numbers = numbers; _city = city; _world = world; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Brush ink = _world ? WidgetBrushes.Get(_night ? "#F5F5F7" : "#19191C") : _city ? WidgetBrushes.Get("#F0F3F6") : (Brush?)TryFindResource("WidgetInk") ?? WidgetBrushes.Get(_dark ? "#F5F5F7" : "#19191C");
        if (_world || _city) dc.DrawEllipse(WidgetBrushes.Get(_city ? "#253B4A" : _night ? "#38383E" : "#F5F5F7"), null, new Point(168, 168), 167, 167);
        int count = _world ? 12 : 60;
        for (int i = 0; i < count; i++) {
            bool major = _world || i % 5 == 0;
            double a = i * 2 * Math.PI / count, outer = _world ? 150 : _city ? 154 : 149;
            double length = _world ? 10 : _city ? (major ? 12 : 4) : major ? 13 : 7;
            var color = _world ? WidgetBrushes.Get(_night ? "#ACADB9" : "#858591") : major ? ink : WidgetBrushes.Get(_city ? "#8D9AA4" : _dark ? "#696971" : "#C0BFC9");
            var pen = new Pen(color, _world ? 4.5 : _city ? (major ? 3.5 : 1.6) : major ? 3.4 : 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(pen, new Point(168 + Math.Sin(a) * (outer - length), 168 - Math.Cos(a) * (outer - length)), new Point(168 + Math.Sin(a) * outer, 168 - Math.Cos(a) * outer));
        }
        if (_numbers || _city) for (int i = 1; i <= 12; i++) {
            if (_city && i % 3 != 0) continue;
            double a = i * Math.PI / 6, radius = _city ? 112 : 111;
            var text = new FormattedText(i.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), _city ? 33 : 35, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(168 + Math.Sin(a) * radius - text.Width / 2, 168 - Math.Cos(a) * radius - text.Height / 2 - 5));
        }
    }
}
