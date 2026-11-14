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
    private CheckBox _liquid = null!; private Slider _quality = null!; private FrameworkElement _tintCell = null!, _magnifyCell = null!; private readonly List<(Button Dot, string Hex)> _tintDots = new();
    private const double StageHeight = 150;

    private FrameworkElement BuildAppearanceInspector(Func<int, string?> detailKey)
    {
        var page = new StackPanel { Margin = new Thickness(DesignSystem.PageInset, 18, DesignSystem.PageInset, 18) };

        // The strip: wallpaper, a small caption, the Dock sitting on it.
        var frame = new SquircleBorder { CornerRadius = new CornerRadius(16), Exponent = 2.5, Height = StageHeight, Child = _stage };
        AutomationProperties.SetAutomationId(_stage, "appearance-stage"); AutomationProperties.SetAutomationId(frame, "rounded-stage-frame");
        // The caption is a quiet dark pill, legible on any picture without a drop shadow.
        var caption = new Border { Background = DesignSystem.Brush(Color.FromArgb(92, 0, 0, 0)), CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(12, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Child = new TextBlock { Text = "你的桌面", FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = DesignSystem.Brush(Color.FromArgb(240, 255, 255, 255)) } };
        // A soft floor under the Dock gives it somewhere to stand on bright pictures.
        var floor = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, 0, 0, 0), 0), new(Color.FromArgb(46, 0, 0, 0), 1) }, 90); floor.Freeze();
        var ground = new Border { Background = floor, Height = StageHeight * .45, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        _stage.Children.Add(_stageWall); _stage.Children.Add(ground); _stage.Children.Add(_stageDock); _stage.Children.Add(_stageIcons); _stage.Children.Add(caption);
        // A hairline of light along the stage's edge separates the picture from the canvas.
        var edge = new SquircleBorder { CornerRadius = new CornerRadius(16), Exponent = 2.5, BorderThickness = new Thickness(1), IsHitTestVisible = false, BorderBrush = DesignSystem.Brush(Color.FromArgb(Design.Dark ? (byte)26 : (byte)20, Design.Dark ? (byte)255 : (byte)0, Design.Dark ? (byte)255 : (byte)0, Design.Dark ? (byte)255 : (byte)0)) };
        var stageHost = new Grid { Height = StageHeight }; stageHost.Children.Add(frame); stageHost.Children.Add(edge);
        page.Children.Add(stageHost);

        _material = Segmented(new[] { "液态玻璃", "浅色", "深色" }, 0); _material.SelectedIndex = MaterialIndex();
        AutomationProperties.SetAutomationId(_material, "dock-material"); AutomationProperties.SetName(_material, "材质");
        _material.SelectionChanged += (_, _) => MaterialPicked();
        _position = Segmented(new[] { "底部", "左侧", "右侧" }, (int)_draft.Edge);
        AutomationProperties.SetAutomationId(_position, "dock-position"); AutomationProperties.SetName(_position, "位置");
        _position.SelectionChanged += (_, _) => { if (!_ready || _updating || _position.SelectedIndex < 0) return; if (Setting<ComboBox>("dock-edge") is { } edge) edge.SelectedIndex = _position.SelectedIndex; };
        _quickSync.Add(() => _position.SelectedIndex = (int)_draft.Edge);
        // 质感: from milk glass (磨砂) to clear water-light glass (水光); the renderer values follow it.
        _liquid = new CheckBox { IsChecked = _draft.LiquidGlass }; AutomationProperties.SetAutomationId(_liquid, "liquid-glass"); AutomationProperties.SetName(_liquid, "液态玻璃");
        _quality = new Slider { Minimum = 0, Maximum = 1, Value = _draft.GlassQuality, VerticalAlignment = VerticalAlignment.Center }; AutomationProperties.SetAutomationId(_quality, "glass-quality"); AutomationProperties.SetName(_quality, "质感");
        _quality.ValueChanged += (_, _) => { if (!_ready || _updating) return; _draft.GlassQuality = _quality.Value; MaterialChanged(); };
        var qualityValue = new TextBlock(); _quality.ValueChanged += (_, _) => qualityValue.Text = _quality.Value < .34 ? "磨砂" : _quality.Value > .66 ? "水光" : "适中";
        qualityValue.Text = _quality.Value < .34 ? "磨砂" : _quality.Value > .66 ? "水光" : "适中";
        _quickSync.Add(() => _quality.Value = _draft.GlassQuality);

        // Two cards by intent rather than one table of arbitrary pairs (audit P01-01/06): what the Dock looks like, and
        // how it behaves. Each card names itself; rows share one anatomy (label 64, control, value 52).
        _material.HorizontalAlignment = HorizontalAlignment.Stretch; _material.Width = double.NaN;
        _position.HorizontalAlignment = HorizontalAlignment.Stretch; _position.Width = double.NaN;
        var looks = InspectorCard("外观"); var behaviour = InspectorCard("行为");
        void Add(StackPanel card, FrameworkElement row) { if (card.Children.Count > 1) card.Children.Add(Hairline()); card.Children.Add(row); }
        Add(looks, Cell("材质", _material));
        Add(looks, _qualityCell = Cell("质感", _quality, qualityValue));
        Add(looks, _tintCell = TintCell());
        Add(looks, CornerCell());
        Add(looks, Mirror("大小", "icon-size", "dock-icon-size", v => $"{v:0} px"));
        Add(looks, Mirror("间距", "icon-spacing", "dock-spacing", v => $"{v:0} px"));
        Add(behaviour, Cell("位置", _position));
        Add(behaviour, Switch("动画", "animations", "dock-animations", () => _draft.Animations));
        // 放大 only works while animations are on, so it sits under 动画, indented, and steps back when they are off.
        _magnifyCell = Mirror("放大", "magnification", "dock-magnification", v => $"{v:0.00}×"); _magnifyCell.Margin = new Thickness(16, 0, 0, 0); Add(behaviour, _magnifyCell);
        Add(behaviour, Switch("自动隐藏", "auto-hide", "dock-auto-hide", () => _draft.AutoHide));
        Add(behaviour, Switch("最近应用", "recent-apps", "dock-recent", () => _draft.ShowRecentApps));
        var cards = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        cards.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); cards.ColumnDefinitions.Add(new() { Width = new GridLength(16) }); cards.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var looksCard = CardSurface(looks); var behaviourCard = CardSurface(behaviour); Grid.SetColumn(behaviourCard, 2);
        behaviourCard.VerticalAlignment = VerticalAlignment.Top; cards.Children.Add(looksCard); cards.Children.Add(behaviourCard);
        page.Children.Add(cards);

        // Advanced settings, one click away each: a quiet link plus a chip per category (audit P01-05).
        var advanced = new WrapPanel { Margin = new Thickness(0, 14, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var entry = QuietLink("高级设置", "appearance-advanced", () => NavigateSettingsPage("dock-advanced")); entry.Margin = new Thickness(-8, 0, 10, 0); advanced.Children.Add(entry);
        foreach (var (title, key, id) in new[] { ("底板", detailKey(0), "advanced-chip-base"), ("玻璃", detailKey(1), "advanced-chip-glass"), ("边缘", detailKey(2), "advanced-chip-edge"), ("图标", "icons", "advanced-chip-icons"), ("动画", "motion", "advanced-chip-motion"), ("应用", "apps", "advanced-chip-apps"), ("位置", "placement", "advanced-chip-placement") })
        {
            if (key is null) continue; string route = key;
            advanced.Children.Add(Chip(title, id, () => NavigateSettingsPage(route)));
        }
        page.Children.Add(advanced);
        SyncDockInspector();
        _stage.SizeChanged += (_, _) => RefreshStageBackdrop();
        _stage.Loaded += (_, _) => RefreshStage();
        WallpaperFrost.Changed += RefreshStage; Closed += (_, _) => WallpaperFrost.Changed -= RefreshStage;
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        AutomationProperties.SetName(scroll, "外观与主题");
        return scroll;
    }
    private static Border Hairline() { var line = new Border { Height = 1, SnapsToDevicePixels = true }; line.SetResourceReference(Border.BackgroundProperty, "SettingsLine"); return line; }
    // An inspector card: its name in quiet type on top, then rows.
    private static StackPanel InspectorCard(string title)
    {
        var card = new StackPanel();
        var name = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 12, 0, 2) }; name.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted");
        card.Children.Add(name); return card;
    }
    private static SquircleBorder CardSurface(UIElement content)
    {
        var card = new SquircleBorder { CornerRadius = new CornerRadius(12), Exponent = 2.5, Padding = new Thickness(16, 0, 16, 4), Child = content, BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BorderBrushProperty, "SettingsGroupEdge"); card.SetResourceReference(Border.BackgroundProperty, "SettingsCard"); return card;
    }
    // A quiet pill link to one advanced category.
    private static Button Chip(string title, string id, Action action)
    {
        var text = new TextBlock { Text = title, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center }; text.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted");
        var button = new Button { Content = text, Template = LinkTemplate(), Padding = new Thickness(2, 5, 2, 5), Margin = new Thickness(0, 0, 2, 0), Cursor = System.Windows.Input.Cursors.Hand };
        button.MouseEnter += (_, _) => text.SetResourceReference(TextBlock.ForegroundProperty, "SettingsAccentInk"); button.MouseLeave += (_, _) => text.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted");
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, "高级设置：" + title); button.Click += (_, _) => action(); return button;
    }
    // 色调 for liquid glass: none, ice, lavender, blossom, mint (GlassMaterial.Tints) as small dots (audit P06-11).
    private FrameworkElement TintCell()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (name, hex) in GlassMaterial.Tints)
        {
            string value = hex;
            var swatch = new Grid { Width = 22, Height = 22 };
            var ring = new Ellipse { StrokeThickness = 2, Opacity = 0 }; ring.SetResourceReference(Shape.StrokeProperty, "SettingsAccent");
            var dot = new Ellipse { Width = 16, Height = 16, StrokeThickness = 1 };
            dot.Fill = string.IsNullOrEmpty(hex) ? Brushes.White : DesignSystem.Brush(DesignSystem.Hex(hex)); dot.SetResourceReference(Shape.StrokeProperty, "SettingsControlStroke");
            swatch.Children.Add(dot); swatch.Children.Add(ring);
            if (string.IsNullOrEmpty(hex)) { var slash = new Line { X1 = 5.5, Y1 = 16.5, X2 = 16.5, Y2 = 5.5, StrokeThickness = 1.2 }; slash.SetResourceReference(Shape.StrokeProperty, "SettingsTertiary"); swatch.Children.Add(slash); }
            var button = new Button { Content = swatch, Template = LinkTemplate(), Padding = new Thickness(2), Margin = new Thickness(0, 0, 4, 0), ToolTip = name, Cursor = System.Windows.Input.Cursors.Hand };
            AutomationProperties.SetAutomationId(button, "glass-tint-" + _tintDots.Count); AutomationProperties.SetName(button, "色调 " + name);
            button.Click += (_, _) => { if (!_ready || _updating) return; _draft.GlassTintColor = value; _glassOnlyPreview = true; QueuePreview(); RefreshStage(); SyncTintDots(); };
            _tintDots.Add((button, value)); row.Children.Add(button);
        }
        SyncTintDots();
        return Cell("色调", row);
    }
    private void SyncTintDots()
    {
        foreach (var (dot, hex) in _tintDots)
            if (dot.Content is Grid swatch && swatch.Children[1] is Ellipse ring) ring.Opacity = string.Equals(hex, _draft.GlassTintColor ?? "", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }
    // A cell: label (fixed column), the control, and an optional muted value on the right.
    private static Grid Cell(string title, FrameworkElement control, TextBlock? value = null)
    {
        var grid = new Grid { Height = 46 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(64) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(value is null ? 0 : 50) });
        grid.Children.Add(new TextBlock { Text = title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center });
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
        slider.ValueChanged += (_, _) => { value.Text = format(slider.Value); if (!_ready || _updating) return; detail.Value = slider.Value; if (source is "icon-size" or "icon-spacing") QueueStageRefresh(); };
        _quickSync.Add(() => slider.Value = detail.Value);
        return Cell(title, slider, value);
    }
    // 圆角 writes the draft itself: the detailed slider would also turn a light or dark Dock into 自定义.
    private FrameworkElement CornerCell()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Value = _draft.Appearance.CornerRadius };
        AutomationProperties.SetAutomationId(slider, "dock-corner"); AutomationProperties.SetName(slider, "圆角");
        var value = new TextBlock { Text = $"{slider.Value:0}%" };
        slider.ValueChanged += (_, _) =>
        {
            value.Text = $"{slider.Value:0}%"; if (!_ready || _updating) return;
            _draft.Appearance.CornerRadius = slider.Value; _updating = true; _ranges["corner-radius"].Value = slider.Value; _updating = false;
            _glassOnlyPreview = false; QueuePreview(); RefreshStage();
        };
        _quickSync.Add(() => slider.Value = _draft.Appearance.CornerRadius);
        return Cell("圆角", slider, value);
    }
    // A switch cell that flips the detailed switch with the same id.
    private FrameworkElement Switch(string title, string source, string id, Func<bool> read)
    {
        var toggle = new CheckBox { Content = title, IsChecked = read(), Height = 46 };
        AutomationProperties.SetAutomationId(toggle, id); AutomationProperties.SetName(toggle, title);
        void Changed() { if (!_ready || _updating) return; if (Setting<CheckBox>(source) is { } detail) detail.IsChecked = toggle.IsChecked; Dispatcher.BeginInvoke(SyncDockInspector, DispatcherPriority.Background); }
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
            // Glass-only rows leave the card when the material is not glass, instead of greying out (audit P01-02).
            var glassRows = Glass ? Visibility.Visible : Visibility.Collapsed;
            _qualityCell.Visibility = glassRows; _tintCell.Visibility = glassRows; SyncTintDots();
            foreach (var line in new[] { _qualityCell, _tintCell }) if (line.Parent is Panel card && card.Children.IndexOf(line) is int at && at > 0 && card.Children[at - 1] is Border hairline && hairline.Height == 1) hairline.Visibility = glassRows;
            bool animated = _draft.Animations; _magnifyCell.IsEnabled = animated; _magnifyCell.Opacity = animated ? 1 : .45;
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
        media.Children.Add(new TextBlock { Text = "正在播放", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 6, 0, 0) });
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
    private bool _stageRefreshQueued;
    // Coalesces stage rebuilds while a size slider is being dragged: at most one per dispatcher turn.
    private void QueueStageRefresh()
    {
        if (_stageRefreshQueued) return; _stageRefreshQueued = true;
        Dispatcher.BeginInvoke(() => { _stageRefreshQueued = false; RefreshStage(); }, DispatcherPriority.Background);
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
        // The stage follows the real size and spacing in proportion (48 px icons draw at 40 on the stage).
        _stageIcons.Children.Clear(); var sample = _draft.Copy(); double scale = 40.0 / 48; sample.IconSize = Math.Clamp(_draft.IconSize * scale, 26, 60); sample.Spacing = Math.Clamp(_draft.Spacing * scale, 2, 16);
        var items = _dock.Model.DisplayItems.Where(i => i.IsPinned).Take(8).ToArray();
        foreach (var item in items) _stageIcons.Children.Add(new DockTile(item, p, sample) { IsHitTestVisible = false, Focusable = false });
        double tile = sample.IconSize + sample.Spacing + 4;
        _stageDock.Width = Math.Max(120, items.Length * tile + 16); _stageDock.Height = sample.IconSize + 26; _stageDock.Margin = new Thickness(0, 0, 0, 22); _stageIcons.Margin = new Thickness(0, 0, 0, 30);
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
