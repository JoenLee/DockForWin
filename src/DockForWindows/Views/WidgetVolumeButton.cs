using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DockForWindows.Views;

internal sealed class WidgetVolumeButton : Button
{
    private WidgetRenderData _data;
    private readonly Popup _popup;
    private readonly Border _panel;
    private readonly TextBlock _caption;
    private readonly Slider _slider;
    private bool _updating;
    internal bool IsOpen => _popup.IsOpen;
    internal event Action? PopupClosed;
    internal WidgetVolumeButton(WidgetRenderData data)
    {
        _data = data; Width = Height = 32; MinHeight = 0; Padding = new Thickness(8); Background = Brushes.Transparent; Template = WidgetContent.PlayerButtonTemplate;
        var icon = new Path { Data = Geometry.Parse("M1,6 L5,6 10,2 10,16 5,12 1,12 Z M13,5 C17,7 17,11 13,13"), Width = 16, Height = 16, Stretch = Stretch.Uniform, StrokeThickness = 1.5 };
        icon.SetResourceReference(Shape.FillProperty, "WidgetInk"); icon.SetResourceReference(Shape.StrokeProperty, "WidgetInk"); Content = icon;
        AutomationProperties.SetAutomationId(this, "widget-music-volume");
        _caption = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 4) };
        _caption.SetResourceReference(TextBlock.ForegroundProperty, "WidgetInk");
        _slider = new Slider { Minimum = 0, Maximum = 100, Width = 164, Height = 28, IsMoveToPointEnabled = true, Style = ControlCenterTokens.MediaProgress, SmallChange = 1, LargeChange = 5 };
        _slider.SetResourceReference(ForegroundProperty, "WidgetAccent");
        AutomationProperties.SetAutomationId(_slider, "widget-system-volume");
        var body = new StackPanel(); body.Children.Add(_caption); body.Children.Add(_slider);
        _panel = new Border { Child = body, Padding = new Thickness(14), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), BorderBrush = WidgetBrushes.Get("#40889898") };
        _panel.SetResourceReference(Border.BackgroundProperty, "WidgetBackground");
        _popup = new Popup { Child = _panel, PlacementTarget = this, Placement = PlacementMode.Top, AllowsTransparency = true, StaysOpen = false, VerticalOffset = -6 };
        _popup.Closed += (_, _) => PopupClosed?.Invoke();
        Click += (_, e) => { if (_popup.IsOpen) Close(); else Open(); e.Handled = true; };
        Unloaded += (_, _) => Close();
        _slider.ValueChanged += (_, _) => {
            if (_updating || !_slider.IsEnabled) return;
            _caption.Text = Caption(_slider.Value) + Muted(); _data.SetVolume?.Invoke(_slider.Value);
        };
        _panel.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); Focus(); e.Handled = true; } };
        Update(data);
    }
    private static string Caption(double volume) => (Services.UiText.IsEnglish ? "System volume · " : "系统音量 · ") + $"{volume:0}%";
    private string Muted() => _data.Audio?.Muted == true ? (Services.UiText.IsEnglish ? " · Muted" : " · 已静音") : "";
    internal void Update(WidgetRenderData data)
    {
        _data = data; IsEnabled = data.Audio != null && data.SetVolume != null;
        string caption = data.AudioError ?? (data.Audio == null ? (Services.UiText.IsEnglish ? "System volume unavailable" : "系统音量暂不可用") : Caption(data.Audio.Volume) + Muted());
        ToolTip = caption; AutomationProperties.SetName(this, caption);
        _slider.IsEnabled = IsEnabled;
        if (!_slider.IsMouseCaptureWithin) {
            _updating = true; _slider.Value = data.Audio?.Volume ?? 0; _updating = false;
            _caption.Text = data.Audio == null ? caption : Caption(data.Audio.Volume) + Muted();
        }
    }
    internal void Open()
    {
        if (!IsEnabled) return;
        foreach (string name in new[] { "WidgetBackground", "WidgetInk", "WidgetAccent" }) if (TryFindResource(name) is object value) _panel.Resources[name] = value;
        _popup.IsOpen = true;
    }
    internal void Close() => _popup.IsOpen = false;
}
