using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal sealed class Palette
{
    public bool Dark { get; }
    public Brush Text { get; }
    public Brush Secondary { get; }
    public Brush Surface { get; }
    public Brush Line { get; }
    public Brush WindowBackground { get; }
    public InterfaceColors Interface { get; }
    public Brush SettingsCard => SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush(Interface.Card);
    public Brush Sidebar => Brush(Interface.Sidebar);
    public Brush AccentText => Brush(Interface.OnAccent);
    public Brush Indicator { get; }
    public double CornerRadius { get; }
    public double BorderThickness { get; }
    public bool ShadowEnabled { get; }
    public double ShadowOpacity { get; }
    public double ShadowBlur { get; }
    public double GlassIntensity { get; }
    public Brush Accent { get; }
    public bool ReducedMotion => !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast;
    public Palette(DockSettings settings)
    {
        bool systemDark = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            systemDark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch { }
        bool transparency = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            transparency = key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
        }
        catch { }
        if (SystemParameters.IsRemoteSession) transparency = false;
        var visuals = ThemeService.Resolve(settings, systemDark, transparency);
        Dark = settings.InterfaceTheme switch { InterfaceTheme.Dark => true, InterfaceTheme.Light => false, _ => systemDark };
        Interface = InterfaceStyles.Resolve(settings.InterfaceStyle,Dark) with{Accent=settings.InterfaceAccent,Selection=settings.InterfaceAccent};
        Accent = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brush(Interface.Accent);
        CornerRadius = visuals.CornerRadius; BorderThickness = visuals.BorderThickness;
        ShadowEnabled = visuals.ShadowEnabled; ShadowOpacity = visuals.ShadowOpacity; ShadowBlur = visuals.ShadowBlur;
        GlassIntensity = settings.Theme == DockTheme.Glass && !SystemParameters.HighContrast ? settings.GlassIntensity : 0;
        if (SystemParameters.HighContrast)
        {
            Text = SystemColors.WindowTextBrush; Secondary = Text; Surface = SystemColors.WindowBrush;
            Line = Text; WindowBackground = Surface;
            Indicator = Text; ShadowEnabled = false;
        }
        else
        {
            Text = Brush(Interface.Text);
            Secondary = Brush(Interface.Muted);
            Surface = new SolidColorBrush(visuals.Background);
            Line = new SolidColorBrush(visuals.Border);
            Indicator = new SolidColorBrush(visuals.Indicator);
            WindowBackground = Brush(Interface.Background);
        }
    }
    public static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    public Button Button(string text, RoutedEventHandler action, bool primary = false)
    {
        var button = new Button
        {
            Content = text, Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(0, 0, 8, 0),
            Background = primary ? Accent : SettingsCard, Foreground = primary ? AccentText : Text,
            BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 13, MinHeight = 40, FontWeight = FontWeights.Medium
        };
        var border = new FrameworkElementFactory(typeof(GlassButtonChrome)){Name="GlassChrome"};
        border.SetBinding(GlassButtonChrome.SurfaceBrushProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(GlassButtonChrome.EdgeBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(GlassButtonChrome.EdgeThicknessProperty,new System.Windows.Data.Binding("BorderThickness"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(16, 9, 16, 9));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)){VisualTree=border};
        foreach(var (property,value) in new[]{(System.Windows.Controls.Button.IsMouseOverProperty,.14),(System.Windows.Controls.Button.IsPressedProperty,.28),(System.Windows.Controls.Button.IsKeyboardFocusedProperty,.20)})
        {var trigger=new Trigger{Property=property,Value=true};trigger.Setters.Add(new Setter(GlassButtonChrome.InteractionProperty,value,"GlassChrome"));template.Triggers.Add(trigger);}
        var disabled=new Trigger{Property=System.Windows.Controls.Button.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.5d,"GlassChrome"));template.Triggers.Add(disabled);
        button.Template=template;
        button.Click += action;
        return button;
    }
}
