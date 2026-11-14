using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DockForWindows.Models;
using Microsoft.Win32;

namespace DockForWindows.Views;

// The Dock's right-click menus in the manner of Windows 11 (audit W03): a rounded sheet without the old icon gutter,
// rows 32 tall with a soft rounded highlight, separators inset from the edges, light or dark with the system's app
// theme (the tray menu follows the same rule), and opening beside the icon instead of covering it.
internal static class DockMenuStyle
{
    private static bool SystemDark()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int light && light == 0; }
        catch { return false; }
    }

    public static void Apply(ContextMenu menu)
    {
        bool dark = SystemDark();
        Color back = DesignSystem.Hex(dark ? "#2B2E35" : "#FFFFFF"), edge = DesignSystem.Hex(dark ? "#3A3F48" : "#DDE3EA"), ink = DesignSystem.Hex(dark ? "#EEF1F5" : "#18202D");
        Color hover = DesignSystem.Hex(dark ? "#3A3F48" : "#EEF2F7"), line = DesignSystem.Hex(dark ? "#3A3F48" : "#E6EAF0"), muted = DesignSystem.Hex(dark ? "#8E96A3" : "#97A1AF");
        menu.FontFamily = DesignSystem.TextFont; menu.FontSize = 13; menu.Foreground = DesignSystem.Brush(ink); menu.HasDropShadow = false;
        menu.Template = SheetTemplate(back, edge, dark);
        var item = new Style(typeof(MenuItem));
        item.Setters.Add(new Setter(Control.ForegroundProperty, DesignSystem.Brush(ink)));
        item.Setters.Add(new Setter(Control.TemplateProperty, RowTemplate(hover, muted)));
        menu.Resources[typeof(MenuItem)] = item;
        var separator = new Style(typeof(Separator));
        var bar = new FrameworkElementFactory(typeof(Border)); bar.SetValue(FrameworkElement.HeightProperty, 1.0); bar.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 4, 10, 4)); bar.SetValue(Border.BackgroundProperty, DesignSystem.Brush(line));
        separator.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Separator)) { VisualTree = bar }));
        // Separators inside menus are styled through MenuItem.SeparatorStyleKey, not through their type.
        menu.Resources[typeof(Separator)] = separator; menu.Resources[MenuItem.SeparatorStyleKey] = separator;
    }

    // Opens beside the icon it belongs to: above it for a bottom Dock, to the side for a left or right Dock.
    public static void PlaceBeside(ContextMenu menu, DockEdge edge, FrameworkElement? owner = null)
    {
        // ContextMenuService on the owner decides the placement when the menu opens from a right-click.
        menu.Placement = PlacementMode.Custom; if (owner is not null) ContextMenuService.SetPlacement(owner, PlacementMode.Custom);
        menu.CustomPopupPlacementCallback = (popup, target, offset) =>
        {
            const double gap = 10;
            var point = edge switch
            {
                DockEdge.Left => new Point(target.Width + gap, (target.Height - popup.Height) / 2),
                DockEdge.Right => new Point(-popup.Width - gap, (target.Height - popup.Height) / 2),
                _ => new Point((target.Width - popup.Width) / 2, -popup.Height - gap),
            };
            return new[] { new CustomPopupPlacement(point, PopupPrimaryAxis.Horizontal) };
        };
    }

    private static ControlTemplate SheetTemplate(Color back, Color edge, bool dark)
    {
        // A margin around the sheet leaves room for its shadow inside the popup.
        var root = new FrameworkElementFactory(typeof(Grid)); root.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 8, 12, 16));
        var shadow = new FrameworkElementFactory(typeof(Border)); shadow.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); shadow.SetValue(Border.BackgroundProperty, DesignSystem.Brush(back));
        shadow.SetValue(UIElement.EffectProperty, new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Color = dark ? Colors.Black : DesignSystem.Hex("#0B1730"), Opacity = dark ? .5 : .16 });
        var sheet = new FrameworkElementFactory(typeof(Border)); sheet.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); sheet.SetValue(Border.BorderBrushProperty, DesignSystem.Brush(edge)); sheet.SetValue(Border.BorderThicknessProperty, new Thickness(1)); sheet.SetValue(Border.PaddingProperty, new Thickness(5));
        var items = new FrameworkElementFactory(typeof(ItemsPresenter)); sheet.AppendChild(items);
        root.AppendChild(shadow); root.AppendChild(sheet);
        return new ControlTemplate(typeof(ContextMenu)) { VisualTree = root };
    }

    private static ControlTemplate RowTemplate(Color hover, Color muted)
    {
        var row = new FrameworkElementFactory(typeof(Border)) { Name = "Row" };
        row.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); row.SetValue(Border.BackgroundProperty, Brushes.Transparent); row.SetValue(Border.PaddingProperty, new Thickness(12, 0, 22, 0)); row.SetValue(FrameworkElement.MinHeightProperty, 32.0); row.SetValue(FrameworkElement.MinWidthProperty, 168.0);
        var text = new FrameworkElementFactory(typeof(ContentPresenter)); text.SetValue(ContentPresenter.ContentSourceProperty, "Header"); text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); text.SetValue(ContentPresenter.RecognizesAccessKeyProperty, false);
        row.AppendChild(text);
        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = row };
        var over = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true }; over.Setters.Add(new Setter(Border.BackgroundProperty, DesignSystem.Brush(hover), "Row")); template.Triggers.Add(over);
        var off = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; off.Setters.Add(new Setter(Control.ForegroundProperty, DesignSystem.Brush(muted))); template.Triggers.Add(off);
        return template;
    }
}
