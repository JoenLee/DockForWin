using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
namespace DockForWindows.Views;
internal sealed partial class SettingsWindow
{
    private sealed record AdvancedCategory(string Key, ScrollViewer Page);
    private readonly List<AdvancedCategory> _advanced = new();
    private Grid? _advancedPane;
    private Border _advancedHost = null!;
    private string? _advancedSelected;
    private void RegisterAdvancedPage(string key, int section, string title, ScrollViewer page)
    {
        _settingsPages.Add(key, page);
        RegisterSettingsPage(key, section, title, true, BuildAdvancedPane);
        foreach (var control in WalkSettings(page)) {
            string id = AutomationProperties.GetAutomationId(control);
            if (id.Length > 0) _controlRoutes[id] = key;
        }
        _advanced.Add(new(key, page));
    }
    private FrameworkElement BuildAdvancedPane()
    {
        if (_advancedPane is not null) return _advancedPane;
        _advancedPane = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        AutomationProperties.SetAutomationId(_advancedPane, "dock-advanced-pane");
        _advancedHost = new Border();
        _advancedPane.Children.Add(_advancedHost);
        SelectAdvanced(_advancedSelected ?? _advanced.FirstOrDefault()?.Key, false);
        return _advancedPane;
    }
    private void SelectAdvanced(string? key, bool animate)
    {
        if (key is null || _advancedPane is null) return;
        var category = _advanced.FirstOrDefault(c => c.Key == key);
        if (category is null || ReferenceEquals(_advancedHost.Child, category.Page)) return;
        _advancedSelected = key;
        if (category.Page.Parent is Panel panel) panel.Children.Remove(category.Page);
        else if (category.Page.Parent is Border border) border.Child = null;
        if (category.Page.Content is FrameworkElement body) {
            body.Margin = new Thickness(DesignSystem.PageInset, 0, DesignSystem.PageInset, 28);
            SettingsPageHost.TrimTrailingSpace(body);
        }
        category.Page.Visibility = Visibility.Visible;
        category.Page.IsHitTestVisible = true;
        category.Page.Opacity = 1;
        category.Page.RenderTransform = Transform.Identity;
        _advancedHost.Child = category.Page;
        category.Page.ScrollToTop();
    }
}
