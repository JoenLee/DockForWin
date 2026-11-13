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

// 外观与主题 as an inspector: the person's own desktop as a stage in the middle, with the Dock and a Control Center
// rendered in the material being chosen, and a narrow inspector on the right holding the few choices that matter —
// liquid or frosted glass, light or dark, how far toward 磨砂 or 水光, the Dock's shape, and a tint. The detailed
// renderer values stay one click away under 高级调节.
internal sealed partial class SettingsWindow
{
    private readonly Grid _stage = new() { ClipToBounds = true };
    private readonly Image _stageWall = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly DockSurface _stageDock = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly DockSurface _stageCenter = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Width = 196, Height = 172, Margin = new Thickness(0, 18, 18, 0) };
    private readonly StackPanel _stageIcons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
    private readonly Grid _stageCenterContent = new() { Width = 196, Height = 172, Margin = new Thickness(0, 18, 18, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly List<(Border Ring, string Hex)> _tintChoices = new();
    private CheckBox _liquid = null!; private Slider _quality = null!;

    private FrameworkElement BuildAppearanceInspector(Func<int, string?> detailKey)
    {
        var page = new Grid { Margin = new Thickness(12, 0, 12, 12) };
        page.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        page.ColumnDefinitions.Add(new() { Width = new GridLength(12) });
        page.ColumnDefinitions.Add(new() { Width = new GridLength(276) });

        // Stage: the real desktop picture, the Dock in the chosen material with the person's pinned apps, and a
        // small Control Center in the same glass.
        var frame = new Border { CornerRadius = new CornerRadius(14), Child = _stage };
        frame.SizeChanged += (_, _) => { var clip = new RectangleGeometry(new Rect(frame.RenderSize), 14, 14); clip.Freeze(); frame.Clip = clip; };
        AutomationProperties.SetAutomationId(_stage, "appearance-stage");
        _stage.Children.Add(_stageWall);
        var label = new TextBlock { Text = "你的桌面", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(14, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Direction = 270, Opacity = .45 } };
        _stage.Children.Add(label);
        _stage.Children.Add(_stageCenter); _stage.Children.Add(_stageCenterContent); BuildStageCenter();
        _stage.Children.Add(_stageDock); _stage.Children.Add(_stageIcons);
        var shadow = new Border { CornerRadius = new CornerRadius(14), Background = Brushes.Black, Opacity = .12, Margin = new Thickness(0, 2, 0, -2), Effect = new BlurEffect { Radius = 10 }, IsHitTestVisible = false };
        var stageHost = new Grid(); stageHost.Children.Add(shadow); stageHost.Children.Add(frame); page.Children.Add(stageHost);

        // Inspector.
        var card = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(16, 6, 16, 14) };
        card.SetResourceReference(Border.BackgroundProperty, "SettingsCard"); card.SetResourceReference(Border.BorderBrushProperty, "SettingsGroupEdge");
        Grid.SetColumn(card, 2); page.Children.Add(card);
        var dock = new DockPanel { LastChildFill = true }; card.Child = dock;

        var tints = new StackPanel(); DockPanel.SetDock(tints, Dock.Bottom); dock.Children.Add(tints);
        tints.Children.Add(Caption("色调", new Thickness(0, 12, 0, 8)));
        var row = new WrapPanel(); tints.Children.Add(row);
        foreach (var (name, hex) in GlassMaterial.Tints) row.Children.Add(TintSwatch(name, hex));
        row.Children.Add(TintSwatch("自定义", null));

        // Everything above the tints scrolls on its own when the window is short; the tints stay pinned at the bottom.
        var top = new StackPanel(); dock.Children.Add(new ScrollViewer { Content = top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, -10, 0), Padding = new Thickness(0, 0, 10, 0) });
        top.Children.Add(Caption("材质", new Thickness(0, 10, 0, 0)));
        _liquid = new CheckBox { Content = "液态玻璃", IsChecked = _draft.LiquidGlass }; AutomationProperties.SetAutomationId(_liquid, "liquid-glass"); AutomationProperties.SetName(_liquid, "液态玻璃");
        _liquid.Checked += (_, _) => MaterialChanged(); _liquid.Unchecked += (_, _) => MaterialChanged(); top.Children.Add(_liquid);
        var mode = Segmented(new[] { "跟随系统", "浅色", "深色" }, (int)_draft.InterfaceTheme); mode.Width = double.NaN; mode.HorizontalAlignment = HorizontalAlignment.Stretch; mode.Margin = new Thickness(0, 2, 0, 12);
        AutomationProperties.SetAutomationId(mode, "appearance-mode"); AutomationProperties.SetName(mode, "外观");
        mode.SelectionChanged += (_, _) =>
        {
            if (!_ready || mode.SelectedIndex < 0 || (int)_draft.InterfaceTheme == mode.SelectedIndex) return;
            _draft.InterfaceTheme = (InterfaceTheme)mode.SelectedIndex; _draft.ControlCenter.ColorMode = (ControlCenterColorMode)mode.SelectedIndex;
            Dispatcher.BeginInvoke(() => _dock.ReopenSettings(_draft, CurrentSettingsSection));
        };
        top.Children.Add(mode);
        top.Children.Add(new TextBlock { Text = "质感", FontSize = DesignSystem.Body });
        _quality = new Slider { Minimum = 0, Maximum = 1, Value = _draft.GlassQuality, Margin = new Thickness(0, 4, 0, 0) }; AutomationProperties.SetAutomationId(_quality, "glass-quality"); AutomationProperties.SetName(_quality, "质感");
        _quality.ValueChanged += (_, _) => { if (!_ready || _updating) return; _draft.GlassQuality = _quality.Value; MaterialChanged(); };
        top.Children.Add(_quality);
        var ends = new Grid { Margin = new Thickness(0, 2, 0, 4) }; var frost = Caption("磨砂", new Thickness()); var water = Caption("水光", new Thickness()); water.HorizontalAlignment = HorizontalAlignment.Right; ends.Children.Add(frost); ends.Children.Add(water); top.Children.Add(ends);

        top.Children.Add(Rule());
        top.Children.Add(Caption("程序坞", new Thickness(0, 12, 0, 2)));
        top.Children.Add(MiniRange("圆角", "dock-corner", 0, 100, _draft.Appearance.CornerRadius, "0'%'", v => _draft.Appearance.CornerRadius = v));
        top.Children.Add(MiniRange("大小", "dock-icon-size", 32, 72, _draft.IconSize, "0", v => _draft.IconSize = Math.Round(v)));
        top.Children.Add(Rule());
        foreach (var (title, summary, section) in new[] { ("高级调节", "折射、高光、暗角", 1), ("底板与颜色", "主题、透明度、底色", 0), ("边缘与阴影", "边框、阴影、运行点", 2) })
            if (detailKey(section) is { } key) top.Children.Add(Compact(title, summary, "appearance-" + key, () => NavigateSettingsPage(key)));
        top.Children.Add(Compact("图标与倒影", "尺寸、间距、倒影", "appearance-icons", () => NavigateSettingsPage("icons")));

        RefreshTintChoices();
        _stage.SizeChanged += (_, _) => RefreshStageBackdrop();
        _stage.Loaded += (_, _) => RefreshStage();
        WallpaperFrost.Changed += RefreshStage; Closed += (_, _) => WallpaperFrost.Changed -= RefreshStage;
        LocationChanged += (_, _) => { if (_ready) Dispatcher.BeginInvoke(RefreshStageBackdrop, DispatcherPriority.Background); };
        // The page fills the pane (no outer scrolling): the stage keeps the whole desktop in view.
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AutomationProperties.SetName(scroll, "外观与主题");
        return scroll;
    }
    private static TextBlock Caption(string text, Thickness margin) { var t = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = margin }; t.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); return t; }
    private static Border Rule() { var line = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 0) }; line.SetResourceReference(Border.BackgroundProperty, "SettingsLine"); return line; }
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
    // A tint swatch: a glassy dot of the colour; the chosen one wears an accent ring. "自定义" opens a colour picker.
    private FrameworkElement TintSwatch(string name, string? hex)
    {
        Brush fill;
        if (hex is null) { var wheel = new LinearGradientBrush(new GradientStopCollection { new(Color.FromRgb(255, 107, 107), 0), new(Color.FromRgb(255, 217, 61), .25), new(Color.FromRgb(107, 203, 119), .5), new(Color.FromRgb(77, 150, 255), .75), new(Color.FromRgb(199, 125, 255), 1) }, 45); wheel.Freeze(); fill = wheel; }
        else if (hex.Length == 0) fill = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(233, 236, 241), 45);
        else { var c = (Color)ColorConverter.ConvertFromString(hex); fill = new LinearGradientBrush(Color.FromArgb(255, (byte)Math.Min(255, c.R + 18), (byte)Math.Min(255, c.G + 18), (byte)Math.Min(255, c.B + 18)), c, 45); }
        var dot = new Ellipse { Width = 24, Height = 24, Fill = fill, Stroke = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), StrokeThickness = .75, Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Direction = 270, Opacity = .15 } };
        var ring = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(2), Child = dot };
        var button = new Button { Content = ring, Style = (Style)FindResource("SettingsToolButton"), Width = 36, Height = 36, Margin = new Thickness(0, 0, 4, 0), ToolTip = name };
        AutomationProperties.SetAutomationId(button, "glass-tint-" + (hex is null ? "custom" : hex.Length == 0 ? "none" : hex.TrimStart('#').ToLowerInvariant())); AutomationProperties.SetName(button, name);
        _tintChoices.Add((ring, hex ?? "*"));
        button.Click += (_, _) =>
        {
            string value = hex ?? "";
            if (hex is null)
            {
                using var dialog = new Forms.ColorDialog { FullOpen = true };
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            }
            _draft.GlassTintColor = value; RefreshTintChoices(); MaterialChanged();
        };
        return button;
    }
    private void RefreshTintChoices()
    {
        bool preset = GlassMaterial.Tints.Any(t => string.Equals(t.Hex, _draft.GlassTintColor, StringComparison.OrdinalIgnoreCase));
        var accent = TryFindResource("SettingsAccent") as Brush ?? Brushes.DodgerBlue;
        foreach (var (ring, hex) in _tintChoices)
            ring.BorderBrush = (hex == "*" ? !preset : string.Equals(hex, _draft.GlassTintColor, StringComparison.OrdinalIgnoreCase)) ? accent : Brushes.Transparent;
    }
    // Liquid/frosted, 质感 or tint changed: derive the renderer values, preview the Dock, restyle this window too.
    private void MaterialChanged()
    {
        if (!_ready || _updating) return;
        _draft.LiquidGlass = _liquid.IsChecked == true; GlassMaterial.Apply(_draft);
        RefreshAppearanceControls(); _glassOnlyPreview = true; QueuePreview();
        ApplySettingsPalette(_ui); RefreshStage();
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
    private void RefreshStage()
    {
        if (!_ready || !_stage.IsLoaded) return;
        var handle = new WindowInteropHelper(this).Handle;
        var frost = handle == 0 ? null : WallpaperFrost.Get(handle);
        _stageWall.Source = frost?.Clear ?? (ImageSource)_wallpaper;
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
        _stageIcons.Children.Clear(); var sample = _draft.Copy(); sample.IconSize = 40;
        var items = _dock.Model.DisplayItems.Where(i => i.IsPinned).Take(8).ToArray();
        foreach (var item in items) _stageIcons.Children.Add(new DockTile(item, p, sample) { IsHitTestVisible = false, Focusable = false });
        _stageDock.Width = Math.Max(140, items.Length * 52 + 16); _stageDock.Height = 66; _stageDock.Margin = new Thickness(0, 0, 0, 22); _stageIcons.Margin = new Thickness(0, 0, 0, 30);
        Dispatcher.BeginInvoke(RefreshStageBackdrop, DispatcherPriority.Render);
    }
    // Both glass objects refract the stage picture: render it once at the stage's pixel size, screen-aligned.
    private void RefreshStageBackdrop()
    {
        if (!_ready || !_stage.IsLoaded || _stage.ActualWidth <= 0 || _stage.ActualHeight <= 0 || _stageWall.Source is null) return;
        var dpi = VisualTreeHelper.GetDpi(_stage); int w = Math.Max(1, (int)Math.Ceiling(_stage.ActualWidth * dpi.DpiScaleX)), h = Math.Max(1, (int)Math.Ceiling(_stage.ActualHeight * dpi.DpiScaleY));
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(new ImageBrush(_stageWall.Source) { Stretch = Stretch.UniformToFill }, null, new Rect(0, 0, _stage.ActualWidth, _stage.ActualHeight));
        var bitmap = new RenderTargetBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze();
        var point = _stage.PointToScreen(new Point());
        foreach (var surface in new[] { _stageDock, _stageCenter }) { surface.BackdropScreenBounds = new Rect(point.X, point.Y, w, h); surface.Backdrop = bitmap; }
    }
}
