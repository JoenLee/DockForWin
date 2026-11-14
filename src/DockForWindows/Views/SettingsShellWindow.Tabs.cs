using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;

namespace DockForWindows.Views;

// The window's top strip: four text tabs on the left with a short accent pill under the chosen one, minimise and
// close on the right, a hairline below. The strip is also the window's caption, so it drags the window.
internal partial class SettingsShellWindow
{
    internal const double TabBarHeight = 52;
    private static readonly (string Title, string Key)[] Tabs = { ("程序坞", "appearance"), ("控制中心", "control-center"), ("小组件", "widgets"), ("通用", "general") };
    private readonly List<(Button Button, TextBlock Text)> _tabs = new();
    private readonly Border _tabMarker = new() { Height = 3, Width = 0, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false, RenderTransform = new TranslateTransform(), Margin = new Thickness(0, 0, 0, 5) };
    private Grid _tabStrip = null!; private int _selectedTab = -1; private readonly Border _tabLine = new() { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false, SnapsToDevicePixels = true, Opacity = 0 };
    // Sections 0–4 are the Dock's own pages; the control center, widgets, and everything general follow.
    private static int TabFor(int section) => section switch { >= 0 and <= 4 => 0, 7 => 1, 9 => 2, _ => 3 };

    private FrameworkElement BuildTabBar()
    {
        var bar = new Grid();
        var strip = _tabStrip = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(DesignSystem.PageInset - 12, 0, 0, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal }; strip.Children.Add(row);
        for (int i = 0; i < Tabs.Length; i++)
        {
            int index = i;
            var text = new TextBlock { Text = Tabs[i].Title, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
            var button = new Button { Content = text, Template = TabTemplate(), Padding = new Thickness(12, 0, 12, 0), Height = TabBarHeight, Cursor = Cursors.Hand, FocusVisualStyle = null };
            AutomationProperties.SetAutomationId(button, "settings-tab-" + Tabs[i].Key); AutomationProperties.SetName(button, Tabs[i].Title);
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            button.Click += (_, _) => NavigateTab(index);
            row.Children.Add(button); _tabs.Add((button, text));
        }
        _tabMarker.SetResourceReference(Border.BackgroundProperty, "SettingsAccent"); strip.Children.Add(_tabMarker);
        bar.Children.Add(strip);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        controls.Children.Add(WindowButton("最小化", "settings-shell-window-minimize", "M0.5,5.5 H10.5", () => WindowState = WindowState.Minimized));
        controls.Children.Add(WindowButton("关闭", "settings-shell-window-close", "M0.5,0.5 L10.5,10.5 M10.5,0.5 L0.5,10.5", Close));
        bar.Children.Add(controls);
        // Like a macOS toolbar, the hairline under the tabs only appears once the page has scrolled under it.
        _tabLine.SetResourceReference(Border.BackgroundProperty, "SettingsLine"); bar.Children.Add(_tabLine);
        return bar;
    }
    // A tab always lands on its own top page, also when one of its detail pages is showing.
    private void NavigateTab(int index)
    {
        string key = Tabs[index].Key;
        if (_pages.CurrentKey == key) return;
        _pages.Navigate(key);
    }
    private void SelectTab(int index, bool animate)
    {
        if (index != _selectedTab)
        {
            _selectedTab = index;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var text = _tabs[i].Text; bool on = i == index;
                text.SetResourceReference(TextBlock.ForegroundProperty, on ? "SettingsAccent" : "SettingsInk");
                text.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }
        // Measure after the weight change has been laid out.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => PlaceTabMarker(animate));
    }
    private void PlaceTabMarker(bool animate)
    {
        if (_selectedTab < 0 || _selectedTab >= _tabs.Count || !_tabStrip.IsLoaded) return;
        var (button, text) = _tabs[_selectedTab];
        if (text.ActualWidth <= 0) return;
        double x = text.TranslatePoint(new Point(), _tabStrip).X, width = text.ActualWidth;
        bool motion = animate && _tabMarker.Width > 0 && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        double seconds = motion ? .26 : 0;
        SettingsSwitchMotion.Move((TranslateTransform)_tabMarker.RenderTransform, TranslateTransform.XProperty, x, seconds);
        if (seconds > 0) _tabMarker.BeginAnimation(WidthProperty, new DoubleAnimation(width, TimeSpan.FromSeconds(seconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else { _tabMarker.BeginAnimation(WidthProperty, null); _tabMarker.Width = width; }
    }
    // Text only; hovering lays a soft rounded wash behind the label.
    private static ControlTemplate TabTemplate()
    {
        var wash = new FrameworkElementFactory(typeof(Border)) { Name = "Wash" };
        wash.SetValue(Border.BackgroundProperty, Brushes.Transparent); wash.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        wash.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 11, 0, 11));
        wash.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); wash.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = wash };
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; over.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SettingsHover"), "Wash")); template.Triggers.Add(over);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true }; pressed.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SettingsPressed"), "Wash")); template.Triggers.Add(pressed);
        return template;
    }
    private Button WindowButton(string name, string id, string glyph, Action action)
    {
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(glyph), StrokeThickness = 1.3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 11, Height = 11, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "SettingsMuted");
        var button = new Button { Content = path, Template = LinkTemplate(), Width = 38, Height = 32, Padding = new Thickness(5, 10, 5, 10), Margin = new Thickness(2, 0, 0, 0), ToolTip = name, FocusVisualStyle = null };
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, name); WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        if (id.EndsWith("close", StringComparison.Ordinal))
        {
            // Close turns Windows red under the pointer, with a white cross, so it is never mistaken for minimise.
            button.Template = CloseTemplate();
            button.MouseEnter += (_, _) => path.Stroke = Brushes.White;
            button.MouseLeave += (_, _) => path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "SettingsMuted");
        }
        button.Click += (_, _) => action(); return button;
    }
    // Shows the tab bar's hairline while the visible page is scrolled away from its top.
    private void UpdateTabLine(bool animate)
    {
        var page = _pages.Children.OfType<FrameworkElement>().FirstOrDefault(p => p.Visibility == Visibility.Visible);
        double target = page is ScrollViewer scroller && scroller.VerticalOffset > 1 ? 1 : 0;
        if (Math.Abs(_tabLine.Opacity - target) < .01 && _tabLine.HasAnimatedProperties == false) return;
        if (animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast) _tabLine.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromSeconds(.18)));
        else { _tabLine.BeginAnimation(OpacityProperty, null); _tabLine.Opacity = target; }
    }
    private static ControlTemplate CloseTemplate()
    {
        var wash = new FrameworkElementFactory(typeof(Border)) { Name = "Wash" };
        wash.SetValue(Border.BackgroundProperty, Brushes.Transparent); wash.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); wash.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = wash };
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; over.Setters.Add(new Setter(Border.BackgroundProperty, DesignSystem.Brush(DesignSystem.Hex("#E81123")), "Wash")); template.Triggers.Add(over);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true }; pressed.Setters.Add(new Setter(Border.BackgroundProperty, DesignSystem.Brush(DesignSystem.Hex("#C50F1F")), "Wash")); template.Triggers.Add(pressed);
        return template;
    }
}
