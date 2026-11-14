using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DockForWindows.Models;
using Forms = System.Windows.Forms;

namespace DockForWindows.Views;

// 程序坞: a low strip of the person's own desktop with the Dock on it, in the material being chosen, then two quiet
// groups of choices — the material (液态玻璃/浅色/深色, 质感, 色调) and the Dock itself (size, corners, hiding).
// Every detailed value stays one link away under 高级设置.
internal sealed partial class SettingsWindow
{
    private readonly Grid _stage = new() { ClipToBounds = true };
    private readonly Rectangle _stageWall = new() { IsHitTestVisible = false };
    private readonly ImageBrush _stageBrush = new() { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
    private Rect _stageCover = Rect.Empty;
    private SettingsSegmentedControl _material = null!, _position = null!; private FrameworkElement _qualityCell = null!;
    // Quick controls on the 程序坞 page, each mirroring a detailed control by id (or writing the draft directly).
    private readonly List<Action> _quickSync = new();
    private readonly DockSurface _stageDock = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly DockSurface _stageCenter = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Width = 196, Height = 172, Margin = new Thickness(0, 18, 18, 0) };
    private readonly StackPanel _stageIcons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
    private readonly Grid _stageCenterContent = new() { Width = 196, Height = 172, Margin = new Thickness(0, 18, 18, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private CheckBox _liquid = null!; private Slider _quality = null!;
    private const double StageHeight = 150;
    private readonly Grid _stageDockGroup=new();
    private readonly Viewbox _stageFit=new(){Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Margin=new Thickness(20,26,20,18),VerticalAlignment=VerticalAlignment.Bottom};

    private FrameworkElement BuildAppearanceInspector(Func<int, string?> detailKey,FrameworkElement advanced)
    {
        var page = new StackPanel { Margin = new Thickness(DesignSystem.PageInset, 18, DesignSystem.PageInset, 18) };

        if(_live.Parent is Panel previewParent)previewParent.Children.Remove(_live);_live.Margin=new Thickness(0,0,0,18);page.Children.Add(_live);

        _material = Segmented(new[] { LocalizedText.IsEnglish ? "Glass" : LocalizedText.Get("液态玻璃"), LocalizedText.Get("浅色"), LocalizedText.Get("深色") }, 0); _material.SelectedIndex = MaterialIndex();
        AutomationProperties.SetAutomationId(_material, "dock-material"); AutomationProperties.SetName(_material, LocalizedText.Get("材质"));
        _material.SelectionChanged += (_, _) => MaterialPicked();
        _position = Segmented(new[] { LocalizedText.Get("底部"), LocalizedText.Get("左侧"), LocalizedText.Get("右侧") }, (int)_draft.Edge);
        AutomationProperties.SetAutomationId(_position, "dock-position"); AutomationProperties.SetName(_position, LocalizedText.Get("位置"));
        _position.SelectionChanged += (_, _) => { if (!_ready || _updating || _position.SelectedIndex < 0) return; if (Setting<ComboBox>("dock-edge") is { } edge) edge.SelectedIndex = _position.SelectedIndex; };
        _quickSync.Add(() => _position.SelectedIndex = (int)_draft.Edge);
        // 质感: from milk glass (磨砂) to clear water-light glass (水光); the renderer values follow it.
        _liquid = new CheckBox { IsChecked = _draft.LiquidGlass }; AutomationProperties.SetAutomationId(_liquid, "liquid-glass"); AutomationProperties.SetName(_liquid, LocalizedText.Get("液态玻璃"));
        _quality = new Slider { Minimum = 0, Maximum = 1, Value = _draft.GlassQuality, VerticalAlignment = VerticalAlignment.Center }; AutomationProperties.SetAutomationId(_quality, "glass-quality"); AutomationProperties.SetName(_quality, LocalizedText.Get("质感"));
        _quality.ValueChanged += (_, _) => { if (!_ready || _updating) return; _draft.GlassQuality = _quality.Value; MaterialChanged(); };
        var qualityValue = new TextBlock(); _quality.ValueChanged += (_, _) => qualityValue.Text = _quality.Value < .34 ? LocalizedText.Get("磨砂") : _quality.Value > .66 ? LocalizedText.Get("水光") : LocalizedText.Get("适中");
        qualityValue.Text = _quality.Value < .34 ? LocalizedText.Get("磨砂") : _quality.Value > .66 ? LocalizedText.Get("水光") : LocalizedText.Get("适中");
        _quickSync.Add(() => _quality.Value = _draft.GlassQuality);

        // One table, two columns, every cell the same anatomy: label, control, value.
        var table = new StackPanel();
        void Pair(FrameworkElement left, FrameworkElement right)
        {
            if (table.Children.Count > 0) table.Children.Add(Hairline());
            var row = new Grid();
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(41) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(left);
            var divider = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Center, SnapsToDevicePixels = true }; divider.SetResourceReference(Border.BackgroundProperty, "SettingsLine"); Grid.SetColumn(divider, 1); row.Children.Add(divider);
            Grid.SetColumn(right, 2); row.Children.Add(right);
            table.Children.Add(row);
        }
        Pair(Cell(LocalizedText.Get("底板主题"), _material), Cell(LocalizedText.Get("位置"), _position));
        Pair(_qualityCell = Cell(LocalizedText.Get("质感"), _quality, qualityValue), CornerCell());
        Pair(Mirror(LocalizedText.Get("大小"), "icon-size", "dock-icon-size", v => $"{v:0}"), Mirror(LocalizedText.Get("间距"), "icon-spacing", "dock-spacing", v => $"{v:0}"));
        Pair(Mirror(LocalizedText.Get("放大"), "magnification", "dock-magnification", v => $"{v:0.00}×"), Switch(LocalizedText.Get("动画"), "animations", "dock-animations", () => _draft.Animations));
        Pair(Switch(LocalizedText.Get("自动隐藏"), "auto-hide", "dock-auto-hide", () => _draft.AutoHide), Switch(LocalizedText.Get("最近应用"), "recent-apps", "dock-recent", () => _draft.ShowRecentApps));
        var card = new SquircleBorder { CornerRadius = new CornerRadius(14), Exponent = 2.5, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 16, 0, 0), Child = table, BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BorderBrushProperty, "SettingsGroupEdge");
        card.SetResourceReference(Border.BackgroundProperty, "SettingsCard");
        page.Children.Add(card);page.Children.Add(advanced);

        SyncDockInspector();
        _stage.SizeChanged += (_, _) => RefreshStage();
        _stage.Loaded += (_, _) => RefreshStage();

        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        AutomationProperties.SetName(scroll, LocalizedText.Get("外观与主题"));
        return scroll;
    }
    private static Border Hairline() { var line = new Border { Height = 1, SnapsToDevicePixels = true }; line.SetResourceReference(Border.BackgroundProperty, "SettingsLine"); return line; }
    // A cell: label (fixed column), the control, and an optional muted value on the right.
    private static Grid Cell(string title, FrameworkElement control, TextBlock? value = null)
    {
        if(control is SettingsSegmentedControl){control.Width=double.NaN;control.MinWidth=0;control.HorizontalAlignment=HorizontalAlignment.Stretch;}
        var grid = new Grid { Height = LocalizedText.IsEnglish ? 54 : 46 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(LocalizedText.IsEnglish ? 86 : 64) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(value is null ? 0 : 50) });
        grid.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, MaxWidth = LocalizedText.IsEnglish ? 80 : 64, FontSize = LocalizedText.IsEnglish ? 12 : 13.5, VerticalAlignment = VerticalAlignment.Center });
        control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control, 1); grid.Children.Add(control);
        if (value is not null) { System.Windows.Documents.Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular); value.FontSize = 12.5; value.TextAlignment = TextAlignment.Right; value.VerticalAlignment = VerticalAlignment.Center; value.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); Grid.SetColumn(value, 2); grid.Children.Add(value); }
        return grid;
    }
    // A slider that drives the detailed control with the same id, so both always agree and the value is saved once.
    private FrameworkElement Mirror(string title, string source, string id, Func<double, string> format)
    {
        var detail = _ranges[source];
        var slider = new Slider { Minimum = detail.Minimum, Maximum = detail.Maximum, TickFrequency = detail.TickFrequency, IsSnapToTickEnabled = true, Value = detail.Value };
        AutomationProperties.SetAutomationId(slider, id); AutomationProperties.SetName(slider, title);
        var value = new TextBlock { Text = format(slider.Value) };
        slider.ValueChanged += (_, _) => { value.Text = format(slider.Value); if (!_ready || _updating) return; detail.Value = slider.Value; };
        _quickSync.Add(() => slider.Value = detail.Value);
        return Cell(title, slider, value);
    }
    // 圆角 writes the draft itself: the detailed slider would also turn a light or dark Dock into 自定义.
    private FrameworkElement CornerCell()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Value = _draft.Appearance.CornerRadius };
        AutomationProperties.SetAutomationId(slider, "dock-corner"); AutomationProperties.SetName(slider, LocalizedText.Get("圆角"));
        var value = new TextBlock { Text = $"{slider.Value:0}%" };
        slider.ValueChanged += (_, _) =>
        {
            value.Text = $"{slider.Value:0}%"; if (!_ready || _updating) return;
            _draft.Appearance.CornerRadius = slider.Value; _updating = true; _ranges["corner-radius"].Value = slider.Value; _updating = false;
            _glassOnlyPreview = false; QueuePreview(); RefreshStage();
        };
        _quickSync.Add(() => slider.Value = _draft.Appearance.CornerRadius);
        return Cell(LocalizedText.Get("圆角"), slider, value);
    }
    // A switch cell that flips the detailed switch with the same id.
    private FrameworkElement Switch(string title, string source, string id, Func<bool> read)
    {
        var toggle = new CheckBox { Content = title, IsChecked = read(), Height = 46 };
        AutomationProperties.SetAutomationId(toggle, id); AutomationProperties.SetName(toggle, title);
        void Changed() { if (!_ready || _updating) return; if (Setting<CheckBox>(source) is { } detail) detail.IsChecked = toggle.IsChecked; }
        toggle.Checked += (_, _) => Changed(); toggle.Unchecked += (_, _) => Changed();
        _quickSync.Add(() => toggle.IsChecked = read());
        return toggle;
    }
    private readonly Dictionary<string, FrameworkElement> _settingLookup = new();
    private T? Setting<T>(string id) where T : FrameworkElement
    {
        if (_settingLookup.TryGetValue(id, out var known)) return known as T;
        foreach (var root in _settingsPages.Values.Distinct())
            foreach (var item in WalkSettings(root))
                if (item is T found && AutomationProperties.GetAutomationId(found) == id) { _settingLookup[id] = found; return found; }
        return null;
    }
    // A text link with a chevron, quiet until hovered.
    private static Button QuietLink(string title, string id, Action action)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        var text = new TextBlock { Text = title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center }; text.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); content.Children.Add(text);
        var chevron = new Path { Data = Geometry.Parse("M1,1 L5,5 L1,9"), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 6, Height = 10, Margin = new Thickness(7, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        chevron.SetResourceReference(Shape.StrokeProperty, "SettingsMuted"); content.Children.Add(chevron);
        var button = new Button { Content = content, Template = LinkTemplate(), Padding = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, title); button.Click += (_, _) => action(); return button;
    }
    private int MaterialIndex() => _draft.Theme switch { DockTheme.Glass => 0, DockTheme.Light => 1, DockTheme.Dark => 2, _ => -1 };
    // 液态玻璃 derives the renderer values from 质感; 浅色 and 深色 are the plain themes.
    private void MaterialPicked()
    {
        if (!_ready || _updating || _material.SelectedIndex < 0 || _material.SelectedIndex == MaterialIndex()) return;
        if (_material.SelectedIndex == 0) { _updating = true; _liquid.IsChecked = true; _updating = false; _draft.LiquidGlass = true; MaterialChanged(); }
        else _theme.SelectedIndex = _material.SelectedIndex == 1 ? (int)DockTheme.Light : (int)DockTheme.Dark;
        SyncDockInspector(); RefreshStage();
    }
    // Brings the inspector in line with the draft (after the detailed pages or a material change touched it).
    private void SyncDockInspector()
    {
        if (_material is null) return;
        bool was = _updating; _updating = true;
        try
        {
            _material.SelectedIndex = MaterialIndex(); _liquid.IsChecked = _draft.LiquidGlass;
            foreach (var sync in _quickSync) sync();
            _qualityCell.IsEnabled = Glass; _qualityCell.Opacity = Glass ? 1 : .45;
        }
        finally { _updating = was; }
    }
    // One-line link: title, the summary quietly to the right, a chevron.
    private static FrameworkElement Compact(string title, string summary, string id, Action action)
    {
        var grid = new Grid { Height = 36 }; grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = title, FontSize = DesignSystem.Body, VerticalAlignment = VerticalAlignment.Center });
        var hint = new TextBlock { Text = summary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 6, 0) }; hint.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); Grid.SetColumn(hint, 1); grid.Children.Add(hint);
        var chevron = new Path { Data = Geometry.Parse("M1,1 L5.5,5.5 L1,10"), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 7, Height = 11, VerticalAlignment = VerticalAlignment.Center }; chevron.SetResourceReference(Shape.StrokeProperty, "SettingsTertiary"); Grid.SetColumn(chevron, 2); grid.Children.Add(chevron);
        var button = new Button { Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, Template = LinkTemplate(), Padding = new Thickness(0), Margin = new Thickness(-8, 0, -8, 0) };
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, title); button.Click += (_, _) => action(); return button;
    }
    private FrameworkElement MiniRange(string title, string id, double min, double max, double value, string format, Action<double> write)
    {
        var grid = new Grid { Height = 34 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(40) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(38) });
        grid.Children.Add(new TextBlock { Text = title, FontSize = DesignSystem.Body, VerticalAlignment = VerticalAlignment.Center });
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, VerticalAlignment = VerticalAlignment.Center }; AutomationProperties.SetAutomationId(slider, id); AutomationProperties.SetName(slider, title); Grid.SetColumn(slider, 1); grid.Children.Add(slider);
        var number = new TextBlock { Text = value.ToString(format), FontSize = 12, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; number.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); Grid.SetColumn(number, 2); grid.Children.Add(number);
        slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString(format); if (!_ready || _updating) return; write(slider.Value); _glassOnlyPreview = false; QueuePreview(); RefreshStage(); };
        return grid;
    }
    // Liquid/frosted, 质感 or tint changed: derive the renderer values, preview the Dock, restyle this window too.
    private void MaterialChanged()
    {
        if (!_ready || _updating) return;
        _draft.LiquidGlass = _liquid.IsChecked == true; GlassMaterial.Apply(_draft);
        RefreshAppearanceControls(); _glassOnlyPreview = true; QueuePreview(); RefreshStage();
    }

    // ---- stage ----
    private void BuildStageCenter()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new() { Width = new GridLength(8) }); grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new() { Height = new GridLength(78) }); grid.RowDefinitions.Add(new() { Height = new GridLength(8) }); grid.RowDefinitions.Add(new() { Height = new GridLength(26) }); grid.RowDefinitions.Add(new() { Height = new GridLength(8) }); grid.RowDefinitions.Add(new() { Height = new GridLength(26) });
        var circles = new UniformGrid(); circles.Rows = 2; circles.Columns = 2;
        foreach (var (glyph, on) in new[] { ("Wifi", true), ("Bluetooth", true), ("Airplane", false), ("Focus", false) })
        {
            var dot = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = on ? DesignSystem.Brush(DesignSystem.Hex("#3895FF")) : new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), Child = new ControlCenterIcon(glyph) { Width = 15, Height = 15, Brush = Brushes.White } };
            circles.Children.Add(dot);
        }
        grid.Children.Add(Tile(circles));
        var media = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
        media.Children.Add(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left, Background = new LinearGradientBrush(Color.FromRgb(255, 138, 101), Color.FromRgb(106, 27, 154), 45) });
        media.Children.Add(new TextBlock { Text = LocalizedText.Get("正在播放"), FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 6, 0, 0) });
        var tile = Tile(media); Grid.SetColumn(tile, 2); grid.Children.Add(tile);
        for (int i = 0; i < 2; i++)
        {
            var bar = new Grid { Height = 26 }; bar.Children.Add(new Border { CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)) });
            bar.Children.Add(new Border { CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Left, Width = i == 0 ? 104 : 76 });
            bar.Children.Add(new ControlCenterIcon(i == 0 ? "Brightness" : "Volume") { Width = 13, Height = 13, Brush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(9, 0, 0, 0) });
            Grid.SetRow(bar, 2 + i * 2); Grid.SetColumnSpan(bar, 3); grid.Children.Add(bar);
        }
        _stageCenterContent.Children.Add(grid);
        static Border Tile(UIElement child) => new() { CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), BorderThickness = new Thickness(0, .75, 0, 0), Padding = new Thickness(7), Child = child };
    }
    private void RefreshStage()
    {
        if (!_ready || !_stage.IsLoaded) return;
        var handle = new WindowInteropHelper(this).Handle;
        var frost = handle == 0 ? null : WallpaperFrost.Get(handle);
        // The desktop picture itself when it can be read, screen-aligned; the bundled picture otherwise.
        _stageBrush.ImageSource = frost?.Sharp ?? (ImageSource)_wallpaper; _stageCover = Rect.Empty; // a still picture: the whole desktop, centred
        RenderOptions.SetBitmapScalingMode(_stageBrush, BitmapScalingMode.HighQuality); _stageWall.Fill = _stageBrush;
        var p = new Palette(_draft); bool glass = Glass; var tint = GlassTint.Resolve(_draft);
        foreach (var surface in new[] { _stageDock, _stageCenter })
        {
            surface.Fill = p.Surface; surface.Stroke = p.Line; surface.StrokeWidth = glass ? 0 : p.BorderThickness; surface.UseGlass = glass;
            surface.GlassIntensity = p.GlassIntensity; surface.GlassOpacity = _draft.GlassOpacity; surface.GlassFrost = _draft.GlassFrost; surface.GlassDistortion = _draft.GlassDistortion;
            surface.GlassHighlight = _draft.GlassHighlight; surface.GlassHighlightWidth = _draft.GlassHighlightWidth; surface.GlassSideDarkness = _draft.GlassSideDarkness; surface.GlassTint = _draft.GlassTint; surface.GlassTintColor = tint;
            surface.InvalidateVisual();
        }
        _stageDock.Radius = p.CornerRadius; _stageCenter.Radius = 26;
        _stageDock.Effect = p.ShadowEnabled ? new DropShadowEffect { BlurRadius = p.ShadowBlur, ShadowDepth = 4, Opacity = p.ShadowOpacity } : null;
        _stageCenter.Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = .22 };
        _stageIcons.Children.Clear(); var sample = _draft.Copy();
        var items = _dock.Model.DisplayItems.Where(i => i.IsPinned).Take(8).ToArray();
        foreach (var item in items) _stageIcons.Children.Add(new DockTile(item, p, sample) { IsHitTestVisible = false, Focusable = false });
        _stageIcons.Orientation=_draft.Edge==DockEdge.Bottom?Orientation.Horizontal:Orientation.Vertical;
        foreach(FrameworkElement tile in _stageIcons.Children)tile.Margin=_draft.Edge==DockEdge.Bottom?new Thickness(sample.Spacing/2,0,sample.Spacing/2,0):new Thickness(0,sample.Spacing/2,0,sample.Spacing/2);
        _stageFit.HorizontalAlignment=_draft.Edge==DockEdge.Left?HorizontalAlignment.Left:_draft.Edge==DockEdge.Right?HorizontalAlignment.Right:HorizontalAlignment.Center;
        _stageFit.VerticalAlignment=_draft.Edge==DockEdge.Bottom?VerticalAlignment.Bottom:VerticalAlignment.Center;
        _stageFit.MaxWidth=Math.Max(1,_stage.ActualWidth-40);_stageFit.MaxHeight=StageHeight-44;
        Dispatcher.BeginInvoke(RefreshStageBackdrop, DispatcherPriority.Render);
    }
    // Maps the stage's screen rectangle into the picture, then gives each glass object the piece of the picture
    // behind it (padded so the lens can bend light in from beyond its edge) at its own pixel size.
    private void RefreshStageBackdrop()
    {
        if (!_ready || !_stage.IsLoaded || _stage.ActualWidth <= 0 || _stage.ActualHeight <= 0 || _stageBrush.ImageSource is not BitmapSource picture || PresentationSource.FromVisual(_stage) is null) return;
        var origin = _stage.PointToScreen(new Point()); var far = _stage.PointToScreen(new Point(_stage.ActualWidth, _stage.ActualHeight));
        Rect Map(Point a, Point b) => _stageCover.IsEmpty
            ? Cover(picture, b.X - a.X, b.Y - a.Y)
            : new Rect((a.X - _stageCover.X) / _stageCover.Width * picture.PixelWidth, (a.Y - _stageCover.Y) / _stageCover.Height * picture.PixelHeight,
                Math.Max(1, (b.X - a.X) / _stageCover.Width * picture.PixelWidth), Math.Max(1, (b.Y - a.Y) / _stageCover.Height * picture.PixelHeight));
        _stageBrush.Viewbox = Map(origin, far);
        var dpi = VisualTreeHelper.GetDpi(_stage); int pad = (int)Math.Ceiling(24 * dpi.DpiScaleX);
        foreach (var surface in new[] { _stageDock, _stageCenter })
        {
            if (surface.ActualWidth <= 0 || PresentationSource.FromVisual(surface) is null) continue;
            var a = surface.PointToScreen(new Point()); var b = surface.PointToScreen(new Point(surface.ActualWidth, surface.ActualHeight));
            surface.CapturePixelScale=new DpiScale(Math.Max(.01,(b.X-a.X)/surface.ActualWidth),Math.Max(.01,(b.Y-a.Y)/surface.ActualHeight));
            a.Offset(-pad, -pad); b.Offset(pad, pad);
            int w = Math.Max(1, (int)Math.Ceiling(b.X - a.X)), h = Math.Max(1, (int)Math.Ceiling(b.Y - a.Y));
            var brush = new ImageBrush(picture) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
            if (_stageCover.IsEmpty) { var whole = Map(origin, far); brush.Viewbox = new Rect(whole.X + (a.X - origin.X) / (far.X - origin.X) * whole.Width, whole.Y + (a.Y - origin.Y) / (far.Y - origin.Y) * whole.Height, whole.Width * w / (far.X - origin.X), whole.Height * h / (far.Y - origin.Y)); }
            else brush.Viewbox = Map(a, b);
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze();
            surface.BackdropScreenBounds = new Rect(a.X, a.Y, w, h); surface.Backdrop = bitmap;
        }
    }
    // The bundled picture, cropped to cover a w×h area (UniformToFill), in its own pixels.
    private static Rect Cover(BitmapSource picture, double w, double h)
    {
        double scale = Math.Max(w / picture.PixelWidth, h / picture.PixelHeight), cw = w / scale, ch = h / scale;
        return new Rect((picture.PixelWidth - cw) / 2, (picture.PixelHeight - ch) / 2, cw, ch);
    }
}
