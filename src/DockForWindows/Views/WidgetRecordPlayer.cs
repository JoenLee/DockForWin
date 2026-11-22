using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DockForWindows.Services;

namespace DockForWindows.Views;

// Animation for the existing native vinyl widget. No Figma design or asset is assumed here.
internal sealed class WidgetRecordPlayer : Grid
{
    internal const double ParkAngle = -22, PlayAngle = 7;
    private readonly Grid _disc = new();
    private readonly Panel _needle;
    private readonly RotateTransform _spin = new(), _arm = new(ParkAngle);
    private readonly Button _pivot;
    private readonly bool _sleeve;
    private readonly bool _detailed, _metal;
    private readonly double _discSize;
    private WidgetRenderData _data;
    private bool _imported;
    private double _parkAngle=ParkAngle,_playAngle=PlayAngle;
    private string _appliedTrack;
    private byte[]? _appliedArt;
    private bool _active = true, _subscribed, _changing, _lastPlaying, _lastMotion;
    private double _lastTime;
    private long _revision;
    internal double DiscAngle => _spin.Angle;
    internal double NeedleAngle => _arm.Angle;
    internal bool IsSpinning => _subscribed;
    internal bool HasSleeve => _sleeve;
    internal WidgetRenderData Data => _data;
    internal byte[]? AppliedArtwork=>_appliedArt;
    internal event Action? ArtworkApplied;
    internal event Action<bool>? NeedleSettled;

    internal WidgetRecordPlayer(WidgetRenderData data, double size, bool sleeve = false, bool detailed = false, bool metal = false)
    {
        _data = data; _sleeve = sleeve; _detailed=detailed; _metal=metal; _needle=detailed?new Canvas{Width=size,Height=size}:new Grid(); Width = Height = size;
        ClipToBounds = !detailed; _discSize = sleeve ? size * .8 : size;
        _disc.Width = _disc.Height = _discSize;
        _disc.RenderTransformOrigin = new Point(.5, .5); _disc.RenderTransform = _spin;
        if (sleeve) { _disc.HorizontalAlignment = HorizontalAlignment.Right; _disc.VerticalAlignment = VerticalAlignment.Top; _disc.Margin = new Thickness(0, size * .04, 0, 0); }
        Children.Add(_disc); _appliedTrack = Track(data); _appliedArt = data.Media?.Artwork; PaintDisc();
        if (sleeve) Children.Add(Cover(data.Media?.Artwork));
        double left = sleeve ? size - _discSize : 0, top = sleeve ? size * .04 : 0;
        double px = left + (detailed&&!metal?1.15:.88) * _discSize, py = top + .085 * _discSize;
        _arm.CenterX = px; _arm.CenterY = py; _needle.RenderTransform = _arm; _needle.IsHitTestVisible = false;
        var arm = new Polyline { Points = new PointCollection { new(px, py), new(px - .045 * _discSize, py + .31 * _discSize), new(px - .20 * _discSize, py + .49 * _discSize) }, StrokeThickness = Math.Max(1.5, size * .018), StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        arm.SetResourceReference(Shape.StrokeProperty, "WidgetInk"); if(_detailed) {
            var curve=new System.Windows.Shapes.Path {Data=Geometry.Parse($"M{px},{py} L{px},{py+.26*_discSize} Q{px},{py+.34*_discSize} {px-.065*_discSize},{py+.41*_discSize} L{px-.42*_discSize},{py+.59*_discSize}"),Stroke=new LinearGradientBrush(Colors.White,Color.FromRgb(132,153,136),0),StrokeThickness=size*.018,StrokeEndLineCap=PenLineCap.Round};
            if(_metal)curve.Data=Geometry.Parse($"M{px},{py} L{px-.07*_discSize},{py+.64*_discSize}");
            _needle.Children.Add(curve);
            var cartridge=new Rectangle{Width=size*.075,Height=size*.14,RadiusX=2,RadiusY=2,Fill=WidgetBrushes.Get(_metal?"#AEB1AB":"#314532"),Stroke=WidgetBrushes.Get("#9AA68E"),StrokeThickness=.7,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(px-(_metal?.11:.46)*_discSize,py+(_metal?.60:.52)*_discSize,0,0),RenderTransform=new RotateTransform(_metal?10:34)};
            _needle.Children.Add(cartridge);
        } else _needle.Children.Add(arm); Children.Add(_needle);
        var baseCircle = new Ellipse { Width = detailed?size*.115:10, Height = detailed?size*.115:10, Stroke = WidgetBrushes.Get("#708E9A9A"), StrokeThickness = 1.5 };
        if(detailed)baseCircle.Fill=new RadialGradientBrush(Color.FromRgb(223,232,216),Color.FromRgb(133,156,135));else baseCircle.SetResourceReference(Shape.FillProperty, "WidgetAccent");
        double bx = detailed?px-16:Math.Clamp(px - 16, 0, Math.Max(0, size - 32)), by = Math.Clamp(py - 16, 0, Math.Max(0, size - 32));
        var pivotFace = new Canvas { Width = 32, Height = 32 }; Canvas.SetLeft(baseCircle, px - bx - baseCircle.Width/2); Canvas.SetTop(baseCircle, py - by - baseCircle.Height/2); pivotFace.Children.Add(baseCircle);
        if(detailed) { foreach(double fraction in new[]{.76,.61}){var ring=new Ellipse{Width=baseCircle.Width*fraction,Height=baseCircle.Height*fraction,Stroke=WidgetBrushes.Get("#D9E2D1"),StrokeThickness=.7,Fill=WidgetBrushes.Get("#4090A18B")};Canvas.SetLeft(ring,px-bx-ring.Width/2);Canvas.SetTop(ring,py-by-ring.Height/2);pivotFace.Children.Add(ring);} }
        _pivot = new Button { Width = 32, Height = 32, MinHeight = 0, Content = pivotFace, Template = WidgetContent.PlayerButtonTemplate, Padding = new Thickness(0), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(bx, by, 0, 0) };
        AutomationProperties.SetAutomationId(_pivot, "widget-record-needle");
        _pivot.Click += (_, e) => { if (_pivot.IsEnabled) _data.MediaCommand?.Invoke("toggle"); e.Handled = true; }; if(detailed){var layer=new Canvas{Width=size,Height=size};_pivot.Margin=new Thickness(0);Canvas.SetLeft(_pivot,bx);Canvas.SetTop(_pivot,by);layer.Children.Add(_pivot);Children.Add(layer);}else Children.Add(_pivot);
        Loaded += (_, _) => { SystemParameters.StaticPropertyChanged += PreferenceChanged; ApplyState(true); };
        Unloaded += (_, _) => { SystemParameters.StaticPropertyChanged -= PreferenceChanged; _revision++; _changing = false; StopSpin(); ControlMotion.Stop(_arm, RotateTransform.AngleProperty, _arm.Angle); };
        IsVisibleChanged += (_, _) => { if (IsVisible && IsLoaded) ApplyState(true); else { StopSpin(); ControlMotion.Stop(_arm, RotateTransform.AngleProperty, _arm.Angle); _revision++; _changing = false; } };
        UpdatePivot();
    }
    internal void UseImportedVisuals(FrameworkElement record,FrameworkElement needle,Point pivot,double parkAngle)
    {
        _imported=true;_playAngle=0;_parkAngle=parkAngle;ClipToBounds=false;
        _disc.Children.Clear();_disc.Children.Add(record);_needle.Children.Clear();_needle.Children.Add(needle);
        _arm.CenterX=pivot.X;_arm.CenterY=pivot.Y;_pivot.Visibility=Visibility.Collapsed;
        ControlMotion.Stop(_arm,RotateTransform.AngleProperty,_data.Media?.Playing==true?_playAngle:_parkAngle);
    }
    private static string Track(WidgetRenderData data) => (data.Media?.Title ?? "") + "\u001f" + (data.Media?.Artist ?? "");
    private bool Motion => _active && IsLoaded && IsVisible && _data.MotionEnabled && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    private Border Cover(byte[]? art)
    {
        var cover = (Border)WidgetContent.PlayerArt(art, Width * .60); cover.CornerRadius = new CornerRadius(8);
        cover.HorizontalAlignment = HorizontalAlignment.Left; cover.VerticalAlignment = VerticalAlignment.Bottom;
        cover.BorderThickness = new Thickness(1); cover.BorderBrush = WidgetBrushes.Get("#30FFFFFF"); return cover;
    }
    private void PaintDisc()
    {
        if(_imported)return;
        _disc.Children.Clear();
        if(_detailed) {
            _disc.Children.Add(new ReferenceRecordFace(_metal));
            if(_metal) {var photo=(Border)WidgetContent.PlayerArt(_appliedArt,_discSize*.36);photo.CornerRadius=new CornerRadius(_discSize);_disc.Children.Add(photo);}
            else {var face=new Border{Width=_discSize*.32,Height=_discSize*.32,CornerRadius=new CornerRadius(_discSize),Background=WidgetBrushes.Get("#E49150"),BorderBrush=WidgetBrushes.Get("#6C3E24"),BorderThickness=new Thickness(2)};
                var text=new TextBlock{Text="FORM RECORDS\n"+(_data.Media?.Title??"WAITING")+"\n"+(_data.Media?.Artist??""),FontFamily=new FontFamily("Consolas"),FontSize=_discSize*.025,Foreground=WidgetBrushes.Get("#30271D"),TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(3)};face.Child=text;_disc.Children.Add(face);}
            _disc.Children.Add(new Ellipse{Width=4,Height=4,Fill=WidgetBrushes.Get("#839789"),Stroke=WidgetBrushes.Get("#1D2F25"),StrokeThickness=1});return;
        }
        _disc.Children.Add(new Ellipse { Fill = WidgetBrushes.Get("#151619"), Stroke = WidgetBrushes.Get("#55565C"), StrokeThickness = 1 });
        foreach (double inset in new[] { .07, .13, .20 }) _disc.Children.Add(new Ellipse { Margin = new Thickness(_discSize * inset), Stroke = WidgetBrushes.Get("#36383D"), StrokeThickness = .8 });
        var label = (Border)WidgetContent.PlayerArt(_appliedArt, _discSize * .42); label.CornerRadius = new CornerRadius(_discSize); _disc.Children.Add(label);
        _disc.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = Brushes.White });
        if (_sleeve && Children.Count > 1 && Children[1] is Border) { Children.RemoveAt(1); Children.Insert(1, Cover(_appliedArt)); }
    }
    internal void Update(WidgetRenderData data)
    {
        bool change = data.Media?.Playing != _data.Media?.Playing || data.MotionEnabled != _data.MotionEnabled
            || Track(data) != _appliedTrack || !ReferenceEquals(data.Media?.Artwork, _appliedArt);
        _data = data; UpdatePivot();
        if (IsLoaded) { if (change) ApplyState(false); else SyncSpin(); }
    }
    private void UpdatePivot()
    {
        _pivot.IsEnabled = _data.Media?.CanPlay == true && _data.MediaCommand != null;
        string caption = UiText.IsEnglish ? "Tonearm · play / pause" : "唱针 · 播放 / 暂停";
        _pivot.ToolTip = caption; AutomationProperties.SetName(_pivot, caption);
    }
    internal void SetActive(bool active) { if (_active == active) return; _active = active; ApplyState(true); }
    private void PreferenceChanged(object? sender, PropertyChangedEventArgs e) => ApplyState(true);
    private void ApplyState(bool force)
    {
        bool changed = Track(_data) != _appliedTrack || !ReferenceEquals(_data.Media?.Artwork, _appliedArt);
        bool playing = _data.Media?.Playing == true, motion = Motion;
        if (!force && !changed && playing == _lastPlaying && motion == _lastMotion) { SyncSpin(); return; }
        _lastPlaying = playing; _lastMotion = motion; long version = ++_revision; _changing = false;
        if (!motion) {
            if (changed) ApplyArtwork();
            ControlMotion.Stop(_arm, RotateTransform.AngleProperty, playing ? _playAngle : _parkAngle); StopSpin(); NeedleSettled?.Invoke(playing);return;
        }
        if (changed) {
            _changing = true; StopSpin();
            ControlMotion.To(_arm, RotateTransform.AngleProperty, _parkAngle, completed: () => {
                if (version != _revision || !Motion) return;
                ApplyArtwork();
                ControlMotion.To(_arm, RotateTransform.AngleProperty, playing ? _playAngle : _parkAngle, completed: () => {
                    if (version != _revision) return; _changing = false;NeedleSettled?.Invoke(playing); SyncSpin();
                });
            });
        } else { ControlMotion.To(_arm, RotateTransform.AngleProperty, playing ? _playAngle : _parkAngle,completed:()=>{if(version==_revision)NeedleSettled?.Invoke(playing);}); SyncSpin(); }
    }
    private void ApplyArtwork() { _appliedTrack = Track(_data); _appliedArt = _data.Media?.Artwork; PaintDisc(); ArtworkApplied?.Invoke(); }
    private void SyncSpin()
    {
        bool run = Motion && _data.Media?.Playing == true && !_changing;
        if (run && !_subscribed) { _lastTime = ControlMotion.Now; CompositionTarget.Rendering += Render; _subscribed = true; }
        else if (!run) StopSpin();
    }
    private void StopSpin() { if (!_subscribed) return; CompositionTarget.Rendering -= Render; _subscribed = false; }
    private void Render(object? sender, EventArgs e)
    {
        if (!Motion || _data.Media?.Playing != true) { StopSpin(); return; }
        double now = ControlMotion.Now, dt = Math.Max(0, now - _lastTime); _lastTime = now;
        _spin.Angle = (_spin.Angle + dt * 30) % 360;
    }
}
