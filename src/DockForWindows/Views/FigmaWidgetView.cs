using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using Path = System.Windows.Shapes.Path;

namespace DockForWindows.Views;

// Original Figma layers, local assets and native controls share the existing widget host.
internal sealed partial class FigmaWidgetView : Grid
{
    private static readonly Dictionary<string, JsonElement> Scenes = LoadScenes();
    private static readonly Dictionary<string, BitmapSource> Images = new();
    private static readonly FontFamily Inter = new(new Uri("pack://application:,,,/DockForWindows;component/"), "./Assets/FigmaWidgets/fonts/#Inter");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private readonly Dictionary<string, JsonElement> _nodes;
    private readonly Dictionary<string, Canvas> _layers = new();
    private readonly Dictionary<string, TextBlock> _text = new();
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly Dictionary<string, Slider> _seeks = new();
    private readonly List<(FrameworkElement Original,FrameworkElement Play)> _toggleGlyphs = new();
    private readonly List<(JsonElement Node, RotateTransform Transform, Point Center, double BaseAngle, int Zone)> _hands = new();
    private readonly DispatcherTimer _timer;
    private readonly FigmaWidgetStyle _style;
    private readonly Canvas _scene;
    private readonly Grid _backdrop;
    private readonly JsonElement _surface;
    private readonly Rect _bounds;
    private WidgetRenderData _data;
    private bool _active = true, _subscribed, _syncing;
    private double _sampleAt;
    private double? _wheelVolume;
    private WidgetRecordPlayer? _record;
    private string _appearanceKey="";
    internal WidgetDefinition Definition { get; }
    internal WidgetRenderData Data => _data;
    internal bool IsTicking => _timer.IsEnabled;
    internal int LayerCount => _layers.Count;

    private static Dictionary<string, JsonElement> LoadScenes()
    {
        using var stream = Application.GetResourceStream(new Uri("/DockForWindows;component/Assets/FigmaWidgets/scenes.json", UriKind.Relative))!.Stream;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray().ToDictionary(s => S(s, "key"), s => s.Clone());
    }
    private static string S(JsonElement n, string key, string fallback = "") => n.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
    private static double D(JsonElement n, string key, double fallback = 0) => n.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
    private static bool B(JsonElement n, string key, bool fallback = false) => n.TryGetProperty(key, out var v) ? v.ValueKind == JsonValueKind.True : fallback;
    private static Rect Bounds(JsonElement n, string key = "b") => n.TryGetProperty(key, out var b) && b.ValueKind == JsonValueKind.Object
        ? new(D(b, "x"), D(b, "y"), Math.Max(0, D(b, "width")), Math.Max(0, D(b, "height"))) : new(0, 0, D(n, "w"), D(n, "h"));
    internal FigmaWidgetView(WidgetDefinition definition, WidgetRenderData data)
    {
        Definition = definition; _data = data; _style = FigmaWidgetCatalog.Find(definition.Id)!; _sampleAt = ControlMotion.Now;
        var scene = Scenes[_style.Scene];
        _nodes = scene.GetProperty("nodes").EnumerateArray().ToDictionary(n => S(n, "id"));
        var root = _nodes[S(scene, "id")]; _bounds = Bounds(root);
        _scene = Build(root, true);
        _backdrop = new Grid();
        var surface=root;
        if(!root.TryGetProperty("f",out var rootFills)||rootFills.GetArrayLength()==0)surface=_nodes.Values.FirstOrDefault(n=>D(n,"w")==D(root,"w")&&D(n,"h")==D(root,"h")&&n.TryGetProperty("f",out var f)&&f.GetArrayLength()>0);
        _surface=surface;_backdrop.Visibility=Visibility.Collapsed;Children.Add(_backdrop);
        Children.Add(new Viewbox { Child = _scene, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        AddInteractions(); PrepareToggleGlyphs(); PrepareHands(); PrepareRecord();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher); _timer.Stop();
        Loaded += (_, _) => { if(_record!=null){_record.ArtworkApplied-=RefreshMusic;_record.ArtworkApplied+=RefreshMusic;_record.NeedleSettled-=ImportedNeedleSettled;_record.NeedleSettled+=ImportedNeedleSettled;}Refresh();Relayout(true); SyncActivity(); };
        Unloaded += (_, _) => { if(_record!=null){_record.ArtworkApplied-=RefreshMusic;_record.NeedleSettled-=ImportedNeedleSettled;}StopActivity(); ClosePopups(); };
        IsVisibleChanged += (_, _) => SyncActivity();
        SizeChanged+=(_,_)=>Relayout(true);Update(data);Relayout(true);
    }
    private IEnumerable<JsonElement> ChildrenOf(JsonElement n) => _nodes.Values.Where(c => S(c, "p") == S(n, "id"));
    private Canvas Build(JsonElement n, bool root = false)
    {
        var box = Bounds(n); var layer = new Canvas { Width = Math.Max(.001, box.Width), Height = Math.Max(.001, box.Height), Opacity = D(n, "o", 1), IsHitTestVisible = true };
        _layers[S(n, "id")] = layer;
        if (n.TryGetProperty("v", out var visible) && visible.ValueKind == JsonValueKind.False) layer.Visibility = Visibility.Collapsed;
        bool fillImage = n.TryGetProperty("f", out var paints) && paints.EnumerateArray().Any(p => S(p, "t") == "IMAGE");
        var asset = S(n, "asset");
        if (asset.Length > 0 && !fillImage) {
            var render = Bounds(n, "r"); if (render.Width == 0 || render.Height == 0) render = box;
            var source=LoadImage(asset);
            var image = new Image { Source = source, Width = render.Width, Height = render.Height, Stretch = Stretch.Fill, IsHitTestVisible = false };
            if(n.TryGetProperty("matrix",out var matrix)) {
                double a=matrix[0].GetDouble(),b=matrix[1].GetDouble(),c=matrix[3].GetDouble(),d=matrix[4].GetDouble();
                layer.Width=D(n,"w");layer.Height=D(n,"h");image.Width=D(n,"assetWidth");image.Height=D(n,"assetHeight");
                Canvas.SetLeft(image,D(n,"assetLeft"));Canvas.SetTop(image,D(n,"assetTop"));
                layer.RenderTransform=new MatrixTransform(a,c,b,d,matrix[2].GetDouble()-box.X,matrix[5].GetDouble()-box.Y);
            } else { Canvas.SetLeft(image, render.X - box.X); Canvas.SetTop(image, render.Y - box.Y); }
            layer.Children.Add(image);
        } else Paint(layer, n, root);
        if (S(n, "t") == "TEXT") {
            var text = new TextBlock { Text = S(n, "text"), FontFamily = Inter, FontSize = D(n, "fs", 12), FontWeight = FontWeight.FromOpenTypeWeight((int)D(n, "fw", 400)),
                Foreground = FirstPaint(n), Width = box.Width, Height = Math.Max(box.Height + 2, D(n, "fs", 12) * 1.3), TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = S(n, "align") switch { "CENTER" => TextAlignment.Center, "RIGHT" => TextAlignment.Right, _ => TextAlignment.Left }, IsHitTestVisible = false };
            text.TextTrimming=_data.DesignReference?TextTrimming.None:TextTrimming.CharacterEllipsis;
            if(n.TryGetProperty("lh",out var line)&&S(line,"unit")=="PIXELS"){text.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;text.LineHeight=D(line,"value",text.FontSize*1.2);}
            ApplyTracking(text,n);
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Ideal);
            _text[S(n, "id")] = text; layer.Children.Add(text);
        }
        bool expanded = asset.Length == 0 || fillImage || B(n, "assetExpanded");
        if (expanded) foreach (var child in ChildrenOf(n)) {
            if (B(child, "skip")) continue;
            var content = Build(child); var bounds = Bounds(child); Canvas.SetLeft(content, bounds.X - box.X); Canvas.SetTop(content, bounds.Y - box.Y); layer.Children.Add(content);
        }
        if (!root && S(n, "t") is "FRAME" or "COMPONENT" or "INSTANCE" && !n.TryGetProperty("clip", out _))
            layer.Clip = new RectangleGeometry(new Rect(0, 0, layer.Width, layer.Height), D(n, "cr"), D(n, "cr"));
        return layer;
    }
    private static BitmapSource LoadImage(string file)
    {
        if (Images.TryGetValue(file, out var result)) return result;
        var image = new BitmapImage(new Uri("pack://application:,,,/DockForWindows;component/Assets/FigmaWidgets/assets/" + file)); image.Freeze();
        if (Images.Count >= 256) Images.Clear(); Images[file] = image; return image;
    }
    private static Color Color(JsonElement c, double alpha = 1) => System.Windows.Media.Color.FromArgb((byte)Math.Round(D(c, "a", 1) * alpha * 255), (byte)Math.Round(D(c, "r") * 255), (byte)Math.Round(D(c, "g") * 255), (byte)Math.Round(D(c, "b") * 255));
    private static Brush? Brush(JsonElement paint)
    {
        if (paint.TryGetProperty("v", out var v) && v.ValueKind == JsonValueKind.False) return null;
        double opacity = D(paint, "o", 1); string kind = S(paint, "t");
        if (kind == "SOLID") { var solid = new SolidColorBrush(Color(paint.GetProperty("c"), opacity)); solid.Freeze(); return solid; }
        if (kind is not ("GRADIENT_LINEAR" or "GRADIENT_RADIAL")) return null;
        GradientBrush gradient; var matrix = paint.GetProperty("m");
        double a = matrix[0][0].GetDouble(), b = matrix[0][1].GetDouble(), tx = matrix[0][2].GetDouble(), c = matrix[1][0].GetDouble(), d = matrix[1][1].GetDouble(), ty = matrix[1][2].GetDouble();
        var transform = new Matrix(a, c, b, d, tx, ty); if (transform.HasInverse) transform.Invert();
        if (kind == "GRADIENT_LINEAR") gradient = new LinearGradientBrush { StartPoint = transform.Transform(new Point(0, .5)), EndPoint = transform.Transform(new Point(1, .5)) };
        else { var center = transform.Transform(new Point(.5, .5)); var x = transform.Transform(new Point(1, .5)); var y = transform.Transform(new Point(.5, 1)); gradient = new RadialGradientBrush { Center = center, GradientOrigin = center, RadiusX = Math.Max(.001, (x - center).Length), RadiusY = Math.Max(.001, (y - center).Length) }; }
        foreach (var stop in paint.GetProperty("g").EnumerateArray()) gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb((byte)Math.Round(stop[4].GetDouble() * opacity * 255), (byte)Math.Round(stop[1].GetDouble() * 255), (byte)Math.Round(stop[2].GetDouble() * 255), (byte)Math.Round(stop[3].GetDouble() * 255)), stop[0].GetDouble()));
        gradient.ColorInterpolationMode = ColorInterpolationMode.SRgbLinearInterpolation; gradient.Freeze(); return gradient;
    }
    private static Brush FirstPaint(JsonElement n) => n.TryGetProperty("f", out var paints) ? paints.EnumerateArray().Select(Brush).LastOrDefault(b => b != null) ?? Brushes.Transparent : Brushes.Transparent;
    private static void Paint(Panel panel, JsonElement n, bool root)
    {
        if (S(n, "t") == "TEXT") return;
        double w = D(n, "w"), h = D(n, "h"); bool ellipse = S(n, "t") == "ELLIPSE";
        double radius = root ? 0 : SourceCorner(n);
        if (n.TryGetProperty("f", out var fills)) foreach (var fill in fills.EnumerateArray()) {
            Brush? brush;
            if (S(fill, "t") == "IMAGE" && S(n, "asset").Length > 0) brush = new ImageBrush(LoadImage(S(n, "asset"))) { Stretch = Stretch.UniformToFill };
            else brush = Brush(fill);
            if (brush == null) continue;
            Shape shape = ellipse ? new Ellipse() : new Path{Data=WidgetGeometry.Get(Math.Max(.001,w),Math.Max(.001,h),2.5,Math.Clamp(radius,0,Math.Min(w,h)/2))};
            shape.Fill = brush; shape.IsHitTestVisible = false;
            if (panel is Canvas) { shape.Width = Math.Max(.001, w); shape.Height = Math.Max(.001, h); }
            panel.Children.Add(shape);
        }
        if (n.TryGetProperty("s", out var strokes) && strokes.GetArrayLength() > 0) {
            if(B(n,"topStroke")){var border=new Border{Width=w,Height=h,BorderBrush=Brush(strokes[0]),BorderThickness=new Thickness(0,D(n,"sw",1),0,0),IsHitTestVisible=false};panel.Children.Add(border);return;}
            Shape shape = ellipse ? new Ellipse() : new Path{Data=WidgetGeometry.Get(Math.Max(.001,w),Math.Max(.001,h),2.5,Math.Clamp(radius,0,Math.Min(w,h)/2))};
            shape.Stroke = Brush(strokes[0]); shape.StrokeThickness = D(n, "sw", 1); shape.IsHitTestVisible = false;
            if (panel is Canvas) { shape.Width = Math.Max(.001, w); shape.Height = Math.Max(.001, h); } panel.Children.Add(shape);
        }
    }
    private IEnumerable<JsonElement> Ancestors(JsonElement n)
    {
        while (_nodes.TryGetValue(S(n, "p"), out var parent)) { yield return parent; n = parent; }
    }
    private void ApplyTracking(TextBlock text,JsonElement n)
    {
        double spacing=0;
        if(n.TryGetProperty("ls",out var ls))spacing=D(ls,"value")*(S(ls,"unit")=="PERCENT"?text.FontSize/100:1);
        text.Width=Math.Max(.1,D(n,"w")+Math.Max(2,-spacing*Math.Max(0,text.Text.Length-1)));
        text.TextEffects=new TextEffectCollection();
        if(Math.Abs(spacing)>.001)for(int i=1;i<text.Text.Length;i++)text.TextEffects.Add(new TextEffect{PositionStart=i,PositionCount=1,Transform=new TranslateTransform(spacing*i,0)});
    }
    private void SetText(TextBlock label,string text,JsonElement n)
    {
        if(label.Text==text)return;label.Text=text;ApplyTracking(label,n);_layoutDirty=true;
    }
    private void MatchTextSlot(TextBlock label,JsonElement n)
    {
        _layoutDirty=true;
    }
    private void PrepareToggleGlyphs()
    {
        if(_data.DesignReference)return;
        foreach(var n in _nodes.Values.Where(n=>S(n,"n") is "pause" or "Pause glyph"||S(n,"n")=="Glyph"&&Ancestors(n).Any(a=>S(a,"n")=="PlayPause"))){
            var box=Bounds(n);if(!_layers.TryGetValue(S(n,"id"),out var layer)||layer.Children.OfType<Image>().FirstOrDefault() is not Image original)continue;
            Brush ink=FirstPaint(n);if(ink==Brushes.Transparent)ink=ChildrenOf(n).Select(FirstPaint).FirstOrDefault(b=>b!=Brushes.Transparent)??Brushes.White;
            FrameworkElement play;
            if(_style.Id=="music-figma-record")play=new Image{Source=LoadImage("record/62790.svg.png"),Width=box.Width,Height=box.Height,Stretch=Stretch.Fill,IsHitTestVisible=false};
            else{play=new Path{Data=Geometry.Parse("M0,0 L10,6 0,12 Z"),Fill=ink,Width=box.Width*.5,Height=box.Height*.6,Stretch=Stretch.Uniform,IsHitTestVisible=false,Tag="figma-play"};Canvas.SetLeft(play,box.Width*.3);Canvas.SetTop(play,box.Height*.2);}
            layer.Children.Add(play);_toggleGlyphs.Add((original,play));
        }
    }
    private string ActionFor(JsonElement n)
    {
        string name = S(n, "n").ToLowerInvariant();
        if (name.Contains("previous hit") || name is "previous control" or "prev" or "previousbutton" or "previous") return "previous";
        if (name.Contains("next hit") || name == "next control" || name == "next" || name == "nextbutton") return "next";
        if (name.Contains("toggle hit") || name is "toggle control" or "pause button" or "playpausebutton" or "playpause" || name == "pause" && !Ancestors(n).Any(a => S(a, "n") == "Pause button")) return "toggle";
        if (name.Contains("menu hit") || name == "queue") return "history";
        if (name == "heart") return "favorite";
        if (name == "shuffle" || name == "repeat") return name;
        if (name == "listening destination") return "output";
        return "";
    }
    private Button Button(JsonElement n, string action)
    {
        var box = Bounds(n); var button = new Button { Width = Math.Max(box.Width, 24), Height = Math.Max(box.Height, 24), MinHeight = 0, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Template = WidgetContent.PlayerButtonTemplate, Cursor = Cursors.Hand };
        AutomationProperties.SetAutomationId(button, "figma-" + action + "-" + S(n, "id")); AutomationProperties.SetName(button, action);
        button.Click += (_, e) => { e.Handled = true; Execute(action, button); }; _buttons[S(n, "id")] = button; return button;
    }
    private void AddInteractions()
    {
        foreach (var n in _nodes.Values) {
            string id = S(n, "id"); if (!_layers.TryGetValue(id, out var layer)) continue;
            string action = ActionFor(n);
            if (_style.Kind == WidgetKind.Music && action.Length > 0) layer.Children.Add(Button(n, action));
            string name = S(n, "n");
            if(_style.Kind==WidgetKind.Music&&name.StartsWith("Interactive ")&&name.Contains("wheel")){
                AutomationProperties.SetAutomationId(layer,"figma-volume-wheel");
                layer.PreviewMouseWheel+=(_,e)=>{if(_data.Audio==null||_data.SetVolume==null)return;_wheelVolume=Math.Clamp((_wheelVolume??_data.Audio.Volume)+e.Delta/120d*2,0,100);_data.SetVolume(_wheelVolume.Value);e.Handled=true;};
                layer.ToolTip="滚轮调节系统音量 · MENU 查看最近播放";
            }
            if (_style.Kind == WidgetKind.Music && (name.StartsWith("Playback progress") && !name.Contains("Elapsed") || name.StartsWith("Track progress") && D(n, "h") <= 4 || name == "Track")) {
                var slider = new Slider { Width = Math.Max(1, D(n, "w")), Height = 24, Minimum = 0, Maximum = 1, Opacity = 0, IsMoveToPointEnabled = true, SmallChange = 5, LargeChange = 15, ToolTip = "拖动调整播放进度" };
                var trackBounds = Bounds(n); Canvas.SetTop(slider, trackBounds.Y - _bounds.Y - 10); Canvas.SetLeft(slider, trackBounds.X - _bounds.X); AutomationProperties.SetAutomationId(slider, "widget-music-seek"); AutomationProperties.SetName(slider, "播放进度");
                slider.ValueChanged += (_, _) => { if (!_syncing && (slider.IsMouseCaptureWithin || slider.IsKeyboardFocusWithin)) _data.Seek?.Invoke(TimeSpan.FromSeconds(slider.Value)); };
                _scene.Children.Add(slider); _seeks[id] = slider;
                foreach (var hit in _nodes.Values.Where(x => S(x, "n").StartsWith("Seek to"))) if (_layers.TryGetValue(S(hit, "id"), out var target)) target.IsHitTestVisible = false;
            }
        }
        if (_style.Kind == WidgetKind.Clock && _style.Id.Contains("alarm")) _scene.Children.Add(Button(_nodes.Values.First(), "alarm"));
    }
    private void Execute(string action, Button anchor)
    {
        if (_data.Preview && _data.MediaCommand == null && action is not ("alarm" or "output" or "history")) return;
        try {
            switch (action) {
                case "alarm": Open("ms-clock:"); break;
                case "output": Open("ms-settings:sound"); break;
                case "favorite": _data.ToggleFavorite?.Invoke(); break;
                case "history": var menu = new ContextMenu(); menu.Items.Add(new MenuItem { Header = "本应用最近播放", IsEnabled = false });
                    foreach (var song in _data.Options.MusicHistory.Take(8)) menu.Items.Add(new MenuItem { Header = song.Title + " · " + song.Artist, IsEnabled = false });
                    if (_data.Options.MusicHistory.Count == 0) menu.Items.Add(new MenuItem { Header = "暂无记录", IsEnabled = false });
                    menu.Items.Add(new Separator()); var launch = new MenuItem { Header = "打开当前播放器", IsEnabled = !string.IsNullOrWhiteSpace(_data.Media?.SourceApp) }; launch.Click += (_, _) => Open("shell:AppsFolder\\" + _data.Media!.SourceApp); menu.Items.Add(launch); anchor.ContextMenu = menu; menu.IsOpen = true; break;
                default: _data.MediaCommand?.Invoke(action); break;
            }
        } catch (Exception e) { ToolTip = e.Message; }
    }
    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    private void ClosePopups() { foreach (var button in _buttons.Values) if (button.ContextMenu != null) button.ContextMenu.IsOpen = false; }
    internal void Update(WidgetRenderData data)
    {
        if (!ReferenceEquals(data.Media, _data.Media)) _sampleAt = ControlMotion.Now;
        if(data.Audio?.Volume!=_data.Audio?.Volume)_wheelVolume=null;
        _data = data; _record?.Update(data); Refresh(); SyncActivity();
    }
    internal void SetActive(bool active) { _active = active; _record?.SetActive(active); SyncActivity(); if (!active) ClosePopups(); }
    private void SyncActivity()
    {
        bool run = _active && IsLoaded && IsVisible;
        _record?.SetActive(run);
        if (run) _timer.Start(); else _timer.Stop();
        bool animate = run && _style.Kind == WidgetKind.Clock && _hands.Count > 0 && _data.Now == null && _data.MotionEnabled && SystemParameters.ClientAreaAnimation;
        if (animate && !_subscribed) { CompositionTarget.Rendering += TickHands; _subscribed = true; }
        else if (!animate && _subscribed) { CompositionTarget.Rendering -= TickHands; _subscribed = false; }
    }
    private void StopActivity() { _timer.Stop(); if (_subscribed) { CompositionTarget.Rendering -= TickHands; _subscribed = false; } _record?.SetActive(false); }
    private DateTimeOffset Utc => _data.Now is DateTime fixedNow ? new DateTimeOffset(DateTime.SpecifyKind(fixedNow, DateTimeKind.Utc)) : DateTimeOffset.UtcNow;
    private TimeZoneInfo Zone(int index = -1)
    {
        string id = index < 0 ? _data.Source.TimeZone : _data.Source.WorldZones.ElementAtOrDefault(index) ?? _data.Source.TimeZone;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; } catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
    private void PrepareHands()
    {
        var pins = _nodes.Values.Where(n => S(n, "n").Contains("pin", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var n in _nodes.Values.Where(n => S(n, "n").Contains("hand", StringComparison.OrdinalIgnoreCase) && !S(n, "n").Contains("pin", StringComparison.OrdinalIgnoreCase))) {
            if (!_layers.TryGetValue(S(n, "id"), out var layer) || pins.Length == 0) continue;
            var bounds = Bounds(n); var pin = pins.MinBy(p => (new Point(Bounds(p).X + Bounds(p).Width / 2, Bounds(p).Y + Bounds(p).Height / 2) - new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)).Length);
            var pinBounds = Bounds(pin); var center = new Point(pinBounds.X + pinBounds.Width / 2 - bounds.X, pinBounds.Y + pinBounds.Height / 2 - bounds.Y);
            int zone = _style.Id.Contains("world") ? Math.Clamp((int)((pinBounds.X - _bounds.X) / (_bounds.Width / 4)), 0, 3) : -1;
            double baseHour = zone switch { 1 => 12, 2 => 17, 3 => 2, _ => _style.Id.Contains("city") ? 17 : 9 };
            string name = S(n, "n"); double angle = name.StartsWith("Hour") ? (baseHour % 12 + 41d / 60) * 30 : name.StartsWith("Minute") ? 41 * 6 : 20 * 6;
            var rotate = new RotateTransform(0, center.X, center.Y); layer.RenderTransform = rotate; _hands.Add((n, rotate, center, angle, zone));
        }
    }
    private void TickHands(object? sender, EventArgs e) => RefreshHands();
    private void RefreshHands()
    {
        foreach (var hand in _hands) {
            var time = TimeZoneInfo.ConvertTime(Utc, Zone(hand.Zone)); double seconds = time.Second + time.Millisecond / 1000d;
            string name = S(hand.Node, "n"); double angle = name.StartsWith("Hour") ? (time.Hour % 12 + time.Minute / 60d + seconds / 3600) * 30 : name.StartsWith("Minute") ? (time.Minute + seconds / 60) * 6 : seconds * 6;
            hand.Transform.Angle = angle - hand.BaseAngle;
        }
    }
    private void PrepareRecord()
    {
        if (!_style.Id.Contains("figma-record") || _data.DesignReference) return;
        var discNode = _nodes.Values.First(n => S(n, "n") == "VinylRecord"); var armNode = _nodes.Values.First(n => S(n, "n") == "Tonearm");
        var disc = _layers[S(discNode, "id")]; var arm = _layers[S(armNode, "id")]; var discBox = Bounds(discNode); var armBox = Bounds(armNode);
        ((Panel)disc.Parent).Children.Remove(disc); ((Panel)arm.Parent).Children.Remove(arm);
        Canvas.SetLeft(disc, 0); Canvas.SetTop(disc, 0);
        var needle = new Canvas { Width = discBox.Width, Height = discBox.Height }; Canvas.SetLeft(arm, armBox.X - discBox.X); Canvas.SetTop(arm, armBox.Y - discBox.Y); needle.Children.Add(arm);
        var mount=Bounds(_nodes.Values.First(n=>S(n,"n")=="TonearmMount"));
        var rest=Scenes["record-3-801"].GetProperty("nodes").EnumerateArray().First(n=>S(n,"n")=="Tonearm").GetProperty("matrix");var playing=armNode.GetProperty("matrix");
        double park=(Math.Atan2(rest[3].GetDouble(),rest[0].GetDouble())-Math.Atan2(playing[3].GetDouble(),playing[0].GetDouble()))*180/Math.PI;
        _record = new WidgetRecordPlayer(_data, discBox.Width); _record.UseImportedVisuals(disc, needle, new Point(mount.X+mount.Width/2-discBox.X,mount.Y+mount.Height/2-discBox.Y),park);
        Canvas.SetLeft(_record, discBox.X - _bounds.X); Canvas.SetTop(_record, discBox.Y - _bounds.Y); _scene.Children.Add(_record);
        ImportedNeedleSettled(_data.Media?.Playing==true);
    }
    private void ImportedNeedleSettled(bool playing)
    {
        if(_record==null)return;var original=_nodes.Values.First(n=>S(n,"n")=="Tonearm");
        var pose=playing?original:Scenes["record-3-801"].GetProperty("nodes").EnumerateArray().First(n=>S(n,"n")=="Tonearm");
        var image=_layers[S(original,"id")].Children.OfType<Image>().First();image.Source=LoadImage(S(pose,"asset"));
    }
    private void Refresh()
    {
        if (_data.DesignReference) return;
        ToolTip = _data.Error;
        if (_style.Kind == WidgetKind.Music) RefreshMusic();
        else if (_style.Kind == WidgetKind.Clock) RefreshClock();
        else RefreshWeather();
        RefreshAppearance();Relayout();SyncTimelineGeometry();
    }
    private void RefreshAppearance()
    {
        DependencyObject? current=this;WidgetHost? host=null;
        while(current!=null){if(current is WidgetHost found){host=found;break;}current=VisualTreeHelper.GetParent(current);}
        if(host==null)return;var colors=host.CardAppearance;
        string key=$"{colors.Background}|{colors.Foreground}|{colors.Accent}|{colors.GradientEnd}|{colors.Gradient}|{colors.TextScale}|{colors.TextWeight}|{colors.Opacity}";
        if(key==_appearanceKey)return;_appearanceKey=key;
        foreach(var pair in _text){var n=_nodes[pair.Key];pair.Value.Foreground=ThemeAppearance.TryColor(colors.Foreground,out var fg)?WidgetBrushes.Get(fg):FirstPaint(n);pair.Value.FontSize=D(n,"fs",12)*colors.TextScale;
            pair.Value.FontWeight=colors.TextWeight switch{WidgetTextWeight.Light=>FontWeights.Light,WidgetTextWeight.Normal=>FontWeights.Normal,WidgetTextWeight.Bold=>FontWeights.Bold,_=>FontWeight.FromOpenTypeWeight((int)D(n,"fw",400))};ApplyTracking(pair.Value,n);MatchTextSlot(pair.Value,n);}
        Brush? background=null;
        if(ThemeAppearance.TryColor(colors.Background,out var bg)){background=WidgetBrushes.Surface(bg,colors.Gradient??false);if(ThemeAppearance.TryColor(colors.GradientEnd,out var end))background=new LinearGradientBrush(WidgetBrushes.Get(bg).Color,WidgetBrushes.Get(end).Color,45);}
        _backdrop.Children.Clear();if(_surface.ValueKind!=JsonValueKind.Undefined)Paint(_backdrop,_surface,true);
        foreach(var shape in _backdrop.Children.OfType<Shape>()){if(background!=null&&shape.Fill!=null)shape.Fill=background;shape.Opacity=colors.Opacity;}
        foreach(var n in _nodes.Values.Where(n=>D(n,"w")==_bounds.Width&&D(n,"h")==_bounds.Height))if(_layers.TryGetValue(S(n,"id"),out var layer))foreach(var shape in layer.Children.OfType<Shape>()){
            if(shape.Fill!=null){if(background!=null)shape.Fill=background;else if(n.TryGetProperty("f",out var fills))shape.Fill=fills.EnumerateArray().Select(Brush).FirstOrDefault(b=>b!=null);}shape.Opacity=colors.Opacity;
        }
        foreach(var n in _nodes.Values.Where(n=>S(n,"n") is "Solar track" or "Daylight arc" or "Solar arc" or "Sun indicator" or "Sun halo" or "Sun position"))if(_layers.TryGetValue(S(n,"id"),out var layer)&&layer.Children.OfType<Image>().FirstOrDefault() is Image image){
            foreach(var old in layer.Children.OfType<Rectangle>().Where(x=>Equals(x.Tag,"figma-accent")).ToArray())layer.Children.Remove(old);
            image.Visibility=Visibility.Visible;
            if(ThemeAppearance.TryColor(colors.Accent,out var accent)){var overlay=new Rectangle{Width=image.Width,Height=image.Height,Fill=WidgetBrushes.Get(accent),OpacityMask=new ImageBrush(image.Source){Stretch=Stretch.Fill},IsHitTestVisible=false,Tag="figma-accent"};Canvas.SetLeft(overlay,Canvas.GetLeft(image));Canvas.SetTop(overlay,Canvas.GetTop(image));layer.Children.Add(overlay);image.Visibility=Visibility.Hidden;}
        }
    }
    private static string Time(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
    private void RefreshMusic()
    {
        var media = _data.Media; bool known = media?.Duration > TimeSpan.Zero;
        var at = known ? MediaControlService.PositionAt(media!.Position, media.Duration, media.Playing, media.PlaybackRate, ControlMotion.Now - _sampleAt) : TimeSpan.Zero;
        foreach (var pair in _text) {
            var n = _nodes[pair.Key]; string name = S(n, "n"), original = S(n, "text"); bool next = Ancestors(n).Any(a => S(a, "n").Contains("Next track"));
            string? text = name switch {
                "Song title" => media?.Title ?? "尚未播放", "Artist name" or "Artist information" => media?.Artist ?? "打开支持系统媒体控制的播放器",
                "Playback status" or "Status" => media == null ? "NO PLAYER" : media.Playing ? "NOW PLAYING" : "PAUSED",
                "Elapsed time" => known ? Time(at) : "—:—", "Duration" => known ? Time(media!.Duration) : "—:—", _ => null };
            if (original is "Soft Focus" or "Stillwater" or "Weightless") text = media?.Title ?? "尚未播放";
            if (original is "Horizon FM" or "The Paper Kites") text = next ? _data.Options.MusicHistory.Skip(1).FirstOrDefault()?.Artist ?? "—" : media?.Artist ?? "等待系统媒体";
            if (original == "NOW PLAYING") text = media == null ? "NO PLAYER" : media.Playing ? "NOW PLAYING" : "PAUSED";
            if (original is "1:42" or "1:24") text=known?Time(at):"—:—";
            if (original.StartsWith("−")) text = known ? "−" + Time(media!.Duration - at) : "—:—";
            if (original == "On the horizon — EP") text = string.IsNullOrEmpty(media?.Album) ? "专辑信息未知" : media.Album;
            if (original.Contains("Lossless")) text = "音质未知";
            if (original == "Living Room") text = _data.OutputName ?? "系统音频输出";
            if (original == "UP NEXT") text = "RECENTLY PLAYED";
            if (original == "Bloom") text = _data.Options.MusicHistory.Skip(1).FirstOrDefault()?.Title ?? "暂无播放记录";
            if (text != null) SetText(pair.Value,text,n);
        }
        _syncing = true;
        foreach (var slider in _seeks.Values) { slider.IsEnabled = known && media!.CanSeek && _data.Seek != null; slider.Maximum = known ? media!.Duration.TotalSeconds : 1; if (!slider.IsMouseCaptureWithin && !(slider.IsKeyboardFocusWithin&&(Keyboard.IsKeyDown(Key.Left)||Keyboard.IsKeyDown(Key.Right)||Keyboard.IsKeyDown(Key.Home)||Keyboard.IsKeyDown(Key.End)))) slider.Value = at.TotalSeconds; }
        _syncing = false;
        foreach(var glyph in _toggleGlyphs){bool playing=media?.Playing==true;glyph.Original.Visibility=playing?Visibility.Visible:Visibility.Hidden;glyph.Play.Visibility=playing?Visibility.Hidden:Visibility.Visible;}
        foreach (var n in _nodes.Values) {
            string id = S(n, "id"), name = S(n, "n"); if (!_layers.TryGetValue(id, out var layer)) continue;
            if (name.StartsWith("Elapsed progress") || name is "ProgressFill" or "Elapsed") {
                double width = known && _nodes.TryGetValue(S(n, "p"), out var p) ? Math.Max(.001, D(p, "w") * at.TotalSeconds / media!.Duration.TotalSeconds) : .001;
                layer.Width = width; foreach (var shape in layer.Children.OfType<Shape>()) shape.Width = width;
            }
            if(name=="Playhead"&&_nodes.TryGetValue(S(n,"p"),out var progressParent)){
                var track=ChildrenOf(progressParent).FirstOrDefault(c=>S(c,"n")=="Track");
                if(track.ValueKind!=JsonValueKind.Undefined){Canvas.SetLeft(layer,Bounds(track).X-Bounds(progressParent).X+(known?D(track,"w")*at.TotalSeconds/media!.Duration.TotalSeconds:0)-D(n,"w")/2);layer.Visibility=known?Visibility.Visible:Visibility.Hidden;}
            }
            if (name.Contains("album", StringComparison.OrdinalIgnoreCase) && (n.TryGetProperty("f", out var fills) && fills.EnumerateArray().Any(f => S(f, "t") == "IMAGE") || name == "AlbumArtwork")) {
                bool history = name.Contains("Next track"); byte[]? bytes = name=="AlbumArtwork"&&_record!=null?_record.AppliedArtwork:media?.Artwork;
                if (history) { var item = _data.Options.MusicHistory.Skip(1).FirstOrDefault(); bytes = item == null ? null : _data.HistoryArt?.GetValueOrDefault(item.ArtworkKey); }
                if (!ReferenceEquals(layer.Tag, bytes) || bytes == null && layer.Tag == null && !layer.Children.OfType<Border>().Any()) {
                    layer.Tag = bytes; var art = (Border)WidgetContent.PlayerArt(bytes, Math.Max(1, Math.Min(layer.Width, layer.Height))); art.CornerRadius = new CornerRadius(name == "AlbumArtwork" ? layer.Width / 2 : D(n, "cr", 12));
                    foreach (var old in layer.Children.Cast<UIElement>().Where(c => c is Shape shape&&shape.Fill is ImageBrush || c is Image || c is Border border&&AutomationProperties.GetAutomationId(border)=="widget-music-art" || name == "AlbumArtwork").ToArray()) layer.Children.Remove(old);
                    layer.Children.Insert(0, art);
                }
            }
            if (_buttons.TryGetValue(id, out var button)) {
                var action = ActionFor(n); button.IsEnabled = action switch { "previous" => media?.CanPrevious == true&&_data.MediaCommand!=null, "next" => media?.CanNext == true&&_data.MediaCommand!=null, "toggle" => media?.CanPlay == true&&_data.MediaCommand!=null, "shuffle" => media?.CanShuffle == true&&_data.MediaCommand!=null, "repeat" => media?.CanRepeat == true&&_data.MediaCommand!=null, "favorite" => media != null && _data.ToggleFavorite != null, _ => true };
                layer.Opacity=button.IsEnabled?D(n,"o",1):.4;
                if(action=="favorite")layer.Opacity=_data.IsFavorite?1:.65;
                if(action=="shuffle"&&button.IsEnabled)layer.Opacity=media?.Shuffle==true?1:.55;
                if(action=="repeat"&&button.IsEnabled)layer.Opacity=media?.Repeat is Windows.Media.MediaPlaybackAutoRepeatMode.List or Windows.Media.MediaPlaybackAutoRepeatMode.Track?1:.55;
                button.ToolTip = action switch { "favorite" => _data.IsFavorite ? "取消本应用收藏" : "收藏到本应用", "history" => "查看本应用最近播放；系统不提供跨应用播放队列", "output" => "打开 Windows 声音设置", _ => action };
            }
        }
    }
    private void RefreshClock()
    {
        var local = TimeZoneInfo.ConvertTime(Utc, Zone()); var textNodes = _nodes.Values.Where(n => n.TryGetProperty("text", out _)).ToArray();
        bool world = _style.Id.Contains("world"), city = _style.Id.Contains("city"), alarm = _style.Id.Contains("alarm");
        foreach (var pair in _text) {
            var n = _nodes[pair.Key]; string original = S(n, "text"); string? value = null;
            int index = world ? Math.Clamp((int)((Bounds(n).X - _bounds.X) / (_bounds.Width / 4)), 0, 3) : -1;
            var zone = Zone(index); var here = TimeZoneInfo.ConvertTime(Utc, zone);
            if (original.Contains(':') && !original.Contains("·")) value = alarm ? "—:—" : here.ToString("H:mm", English);
            if (original == "FRIDAY") value = local.ToString("dddd", English).ToUpperInvariant();
            if (original == "November 20") value = local.ToString("MMMM d", English);
            if (original == "Friday, November 20") value = local.ToString("dddd, MMMM d", English);
            if (original == "Cupertino · PST") value = DocumentClockView.CityName(Zone()) + " · " + DocumentClockView.ZoneLabel(Zone(),local);
            if (original is "CUPERTINO" or "LONDON") value = DocumentClockView.CityName(Zone()).ToUpperInvariant();
            if (original is "SFO" or "NYC" or "LON" or "TYO") value = DocumentClockView.CityCode(zone);
            if (original is "Today" or "Tomorrow" or "Yesterday") { int days = (here.Date - local.Date).Days; value = alarm ? "点击打开 Windows 时钟" : days == 0 ? "Today" : days == 1 ? "Tomorrow" : days == -1 ? "Yesterday" : here.ToString("MMM d", English); }
            if (original.Contains("ahead of Cupertino")) { double hours = (local.Offset - TimeZoneInfo.ConvertTime(Utc, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")).Offset).TotalHours; value = $"{Math.Abs(hours):0.#} hours {(hours >= 0 ? "ahead of" : "behind")} Cupertino"; }
            if (original == "GMT") value = DocumentClockView.ZoneLabel(zone,here);
            if (alarm && original.StartsWith("In ")) value = "系统闹钟不可读取";
            if (alarm && original == "AM") value = "";
            if (value != null) SetText(pair.Value,value,n);
        }
        RefreshHands();
    }
    private void RefreshWeather()
    {
        var weather = _data.Weather; var solar = weather == null ? null : WidgetWeatherService.SolarAt(weather, Utc);
        var current = solar?.LocalTime ?? Utc; var today = weather?.Days.FirstOrDefault(d => d.Date == DateOnly.FromDateTime(current.DateTime)) ?? weather?.Days.FirstOrDefault();
        string sunrise = solar?.Sunrise?.ToString("h:mm tt", English) ?? "—:—", sunset = solar?.Sunset?.ToString("h:mm tt", English) ?? "—:—";
        string next = solar?.Next?.ToString("h:mm tt", English) ?? "—:—";
        foreach (var pair in _text) {
            var n = _nodes[pair.Key]; string name = S(n, "n"); string value = name switch {
                "Location name" or "Location" => string.IsNullOrWhiteSpace(_data.Source.City) ? "选择天气城市" : _data.Source.City,
                "Temperature" => weather == null ? "—°" : $"{weather.Temperature:0}°",
                "Conditions" => weather == null ? _data.Error ?? "正在获取天气…" : WidgetWeatherService.Description(weather.Code),
                "High and low" => today == null ? "H:—°  L:—°" : $"H:{today.High:0}°  L:{today.Low:0}°",
                "Sunrise value" or "Sunrise time" => sunrise, "Sunset value" or "Sunset time" => sunset,
                "Next event" => solar?.IsSunrise == true ? "Sunrise" : "Sunset",
                "Event title" => solar?.IsSunrise == true ? "Next sunrise" : "Next sunset",
                "Event time" or "Next event time" => next,
                "Date" => current.ToString("MMM d", English).ToUpperInvariant(),
                "Time remaining" => solar?.Next is DateTimeOffset at ? $"{(int)(at - current).TotalHours}h {(at - current).Minutes:00}m" : "—h —m",
                "Countdown label" => solar?.Next == null ? "日出日落暂不可用" : solar.IsSunrise ? "until sunrise" : "until sunset", _ => S(n, "text") };
            SetText(pair.Value,value,n);
        }
        if (solar?.DayProgress is double fraction) foreach (var n in _nodes.Values.Where(n => S(n, "n") is "Sun indicator" or "Sun halo")) {
            if (!_layers.TryGetValue(S(n, "id"), out var layer)) continue;
            var box = Bounds(n); double centerX = box.X + box.Width / 2 - _bounds.X, centerY = box.Y + box.Height / 2 - _bounds.Y;
            layer.RenderTransform = new TranslateTransform(218 + 424 * fraction - centerX, 218 - 78 * Math.Sin(Math.PI * fraction) - centerY);
        }
        if(solar?.DayProgress is double progress)foreach(var n in _nodes.Values.Where(n=>S(n,"n")=="Sun position"))if(_layers.TryGetValue(S(n,"id"),out var layer)){
            var box=Bounds(n);layer.RenderTransform=new TranslateTransform(64+512*progress-(box.X+box.Width/2-_bounds.X),264-165*Math.Sin(Math.PI*progress)-(box.Y+box.Height/2-_bounds.Y));
        }
    }
}
