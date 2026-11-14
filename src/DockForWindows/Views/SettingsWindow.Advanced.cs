using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DockForWindows.Views;

// 程序坞 › 高级设置 as one master–detail page (audit P03-01): the seven categories on the left, the chosen one's controls
// on the right. There is no router page any more and no going back and forth between categories. Every former
// detail key ("settings-section-0-1", "icons", …) still navigates here and selects its category, so ShowSetting
// routes, automation ids and saved navigation keep working.
internal sealed partial class SettingsWindow
{
    private sealed record AdvancedCategory(string Key, string Title, string Group, string Glyph, ScrollViewer Page, int Order);
    private readonly List<AdvancedCategory> _advanced = new();
    private readonly Dictionary<string, Button> _advancedButtons = new(StringComparer.Ordinal);
    private Grid? _advancedPane; private Border _advancedHost = null!; private Border _advancedMarker = null!; private StackPanel _advancedList = null!;
    private string? _advancedSelected;

    // Line glyphs on a 16×16 grid, drawn with the same 1.3 px stroke.
    private static readonly Dictionary<string, (string Group, string Glyph, int Order)> AdvancedLayout = new(StringComparer.Ordinal)
    {
        ["底板外观"] = ("外观", "M3,5.5 H13 A2,2 0 0 1 15,7.5 V9.5 A2,2 0 0 1 13,11.5 H3 A2,2 0 0 1 1,9.5 V7.5 A2,2 0 0 1 3,5.5 Z", 0),
        ["液态玻璃"] = ("外观", "M8,1.8 C8,1.8 3.4,7.4 3.4,10.2 A4.6,4.6 0 0 0 12.6,10.2 C12.6,7.4 8,1.8 8,1.8 Z M5.9,10.4 A2.2,2.2 0 0 0 7.6,12.4", 1),
        ["边缘与阴影"] = ("外观", "M2.5,2.5 H10.5 V10.5 H2.5 Z M5.5,13.5 H13.5 V5.5", 2),
        ["图标与倒影"] = ("外观", "M2,2 H7 V7 H2 Z M9,2 H14 V7 H9 Z M2,9 H7 V14 H2 Z M9,9 H14 V14 H9 Z", 3),
        ["动画与交互"] = ("行为", "M1,9 C3,4.5 5.5,4.5 7.5,8 C9.5,11.5 12,11.5 15,7", 4),
        ["位置与隐藏"] = ("行为", "M1.5,2.5 H14.5 V11.5 H1.5 Z M5.5,9.5 H10.5 M6,14 H10", 5),
        ["应用与文件"] = ("行为", "M4.5,1.5 H13 V10 H4.5 Z M1.5,4.5 V13 H10", 6),
    };

    // Registers one category: its key is routed by PageHost to the shared master–detail page.
    private void RegisterAdvancedPage(string key, int section, string title, ScrollViewer page)
    {
        _settingsPages.Add(key, page);
        RegisterSettingsPage(key, section, title, true, BuildAdvancedPane);
        foreach (var control in WalkSettings(page)) { string id = AutomationProperties.GetAutomationId(control); if (id.Length > 0) _controlRoutes[id] = key; }
        string? originalTitle = AdvancedLayout.Keys.FirstOrDefault(key => key == title || LocalizedText.Get(key) == title);
        (string Group, string Glyph, int Order) layout = originalTitle is not null ? AdvancedLayout[originalTitle] : ("行为", "M2,8 H14", 99);
        _advanced.Add(new(key, title, LocalizedText.Get(layout.Group), layout.Glyph, page, layout.Order));
    }

    private FrameworkElement BuildAdvancedPane()
    {
        if (_advancedPane is not null) return _advancedPane;
        var ordered = _advanced.OrderBy(c => c.Order).ToArray();
        var pane = _advancedPane = new Grid { Margin = new Thickness(DesignSystem.PageInset - 10, 6, 0, 0) };
        pane.ColumnDefinitions.Add(new() { Width = new GridLength(176) }); pane.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        AutomationProperties.SetAutomationId(pane, "dock-advanced-pane"); AutomationProperties.SetName(pane, LocalizedText.Get("高级设置"));

        // Left: the categories, grouped by what they change. The chosen one sits on a raised pill that glides.
        var listHost = new Grid { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 0) };
        _advancedMarker = new Border { Height = 34, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(.75), VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, RenderTransform = new TranslateTransform(), Opacity = 0 };
        _advancedMarker.SetResourceReference(Border.BackgroundProperty, "SettingsRaised"); _advancedMarker.SetResourceReference(Border.BorderBrushProperty, "SettingsRaisedEdge");
        _advancedMarker.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Direction = 270, Color = Design.Dark ? Colors.Black : DesignSystem.Hex("#0B1730"), Opacity = Design.Dark ? .35 : .1 };
        listHost.Children.Add(_advancedMarker);
        _advancedList = new StackPanel(); listHost.Children.Add(_advancedList);
        string? group = null;
        foreach (var category in ordered)
        {
            if (category.Group != group)
            {
                group = category.Group;
                var name = new TextBlock { Text = group, FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(10, _advancedList.Children.Count == 0 ? 0 : 14, 0, 4) };
                name.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); _advancedList.Children.Add(name);
            }
            _advancedList.Children.Add(AdvancedRow(category));
        }
        pane.Children.Add(listHost);

        // Right: the chosen category's own page, as it was, now one click from any other category.
        _advancedHost = new Border(); Grid.SetColumn(_advancedHost, 1); pane.Children.Add(_advancedHost);
        pane.Loaded += (_, _) => PlaceAdvancedMarker(false);
        SelectAdvanced(_advancedSelected ?? ordered.FirstOrDefault()?.Key, false);
        return pane;
    }

    private Button AdvancedRow(AdvancedCategory category)
    {
        var glyph = new Path { Data = Geometry.Parse(category.Glyph), StrokeThickness = 1.3, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 16, Height = 16, Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        glyph.SetResourceReference(Shape.StrokeProperty, "SettingsMuted");
        var text = new TextBlock { Text = category.Title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal }; content.Children.Add(glyph); content.Children.Add(text);
        var button = new Button { Content = content, Template = LinkTemplate(), Height = 34, Padding = new Thickness(2, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = System.Windows.Input.Cursors.Hand, Tag = (glyph, text) };
        AutomationProperties.SetAutomationId(button, "advanced-category-" + category.Key); AutomationProperties.SetName(button, category.Title);
        button.Click += (_, _) => SelectAdvanced(category.Key, true);
        _advancedButtons[category.Key] = button; return button;
    }

    // Shows a category on the right without touching the navigation history (the page itself is one history entry).
    private void SelectAdvanced(string? key, bool animate)
    {
        if (key is null || _advancedPane is null) return;
        var category = _advanced.FirstOrDefault(c => c.Key == key); if (category is null) return;
        _advancedSelected = key;
        if (!ReferenceEquals(_advancedHost.Child, category.Page))
        {
            if (category.Page.Parent is Panel oldPanel) oldPanel.Children.Remove(category.Page);
            else if (category.Page.Parent is Border oldBorder) oldBorder.Child = null;
            if (category.Page.Content is FrameworkElement body) body.Margin = new Thickness(20, 0, DesignSystem.PageInset, 28);
            category.Page.Visibility = Visibility.Visible; category.Page.IsHitTestVisible = true; category.Page.Opacity = 1; category.Page.RenderTransform = Transform.Identity;
            _advancedHost.Child = category.Page; category.Page.ScrollToTop();
            if (animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            {
                category.Page.Opacity = 0; var shift = new TranslateTransform(0, 6); category.Page.RenderTransform = shift;
                category.Page.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(.16)));
                SettingsSwitchMotion.Move(shift, TranslateTransform.YProperty, 0, .2);
            }
        }
        foreach (var (k, button) in _advancedButtons)
        {
            if (button.Tag is not (Path glyph, TextBlock text)) continue; bool on = k == key;
            text.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal; text.SetResourceReference(TextBlock.ForegroundProperty, on ? "SettingsInk" : "SettingsMuted");
            glyph.SetResourceReference(Shape.StrokeProperty, on ? "SettingsAccentInk" : "SettingsMuted");
        }
        PlaceAdvancedMarker(animate);
    }

    private void PlaceAdvancedMarker(bool animate)
    {
        if (_advancedSelected is null || !_advancedButtons.TryGetValue(_advancedSelected, out var button) || !button.IsLoaded) return;
        double y = button.TranslatePoint(new Point(), _advancedList).Y;
        _advancedMarker.Opacity = 1;
        SettingsSwitchMotion.Move((TranslateTransform)_advancedMarker.RenderTransform, TranslateTransform.YProperty, y, animate && SystemParameters.ClientAreaAnimation ? .24 : 0);
    }
}
