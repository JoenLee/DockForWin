using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DockForWindows.Views;

internal sealed partial class ControlCenterIcon
{
    private readonly TranslateTransform _symbolShift=new();
    private SymbolLayer[]? _symbolLayers;private string? _layerGlyph;
    private sealed class SymbolLayer:Animatable
    {
        internal Action? Refresh;internal ScaleTransform Transform=new(1,1);
        internal static readonly DependencyProperty AmountProperty=DependencyProperty.Register("Amount",typeof(double),typeof(SymbolLayer),new PropertyMetadata(1d,(o,_)=>((SymbolLayer)o).Refresh?.Invoke()));
        internal double Amount=>(double)GetValue(AmountProperty);
        protected override Freezable CreateInstanceCore()=>new SymbolLayer();
    }
    private static void Keys(Animatable owner,DependencyProperty property,params (int Ms,double Value)[] values)
    {
        var frames=new DoubleAnimationUsingKeyFrames();
        // Start from the currently displayed value when another click interrupts this effect.
        frames.KeyFrames.Add(new SplineDoubleKeyFrame((double)owner.GetValue(property),KeyTime.FromTimeSpan(TimeSpan.Zero),new KeySpline(.2,0,.2,1)));
        foreach(var key in values)frames.KeyFrames.Add(new SplineDoubleKeyFrame(key.Value,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(key.Ms)),new KeySpline(.2,0,.2,1)));
        if(ControlMotion.RefreshRate>1)Timeline.SetDesiredFrameRate(frames,(int)Math.Ceiling(ControlMotion.RefreshRate));owner.BeginAnimation(property,frames,HandoffBehavior.SnapshotAndReplace);
    }
    internal void Feedback(bool hover=false)
    {
        if(hover||!SystemParameters.ClientAreaAnimation||SystemParameters.HighContrast)return;
        if(_glyph is "Wifi" or "Brightness" or "Volume"){
            var geometry=(GeometryGroup)Shape(_glyph)!;
            if(_layerGlyph!=_glyph){_layerGlyph=_glyph;_symbolLayers=new SymbolLayer[geometry.Children.Count];for(int i=0;i<_symbolLayers.Length;i++){var bounds=geometry.Children[i].Bounds;_symbolLayers[i]=new(){Refresh=InvalidateVisual,Transform=new ScaleTransform(1,1,bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2)};}}
            for(int i=0;i<_symbolLayers!.Length;i++){
                if(_glyph=="Volume"&&i==0||_glyph=="Brightness"&&i==_symbolLayers.Length-1)continue;
                int delay=(_glyph=="Brightness"?i%4:i)*60;
                Keys(_symbolLayers[i],SymbolLayer.AmountProperty,(delay, _symbolLayers[i].Amount),(delay+75,.35),(delay+230,1));
            }
            return;
        }
        _feedbackRotation.CenterY=_glyph=="Bell"?-ActualHeight*.3:0;double dip=_glyph=="Airplane"?3:0;
        Keys(_symbolShift,TranslateTransform.XProperty,(100,dip),(360,0));
        double angle=_glyph switch{"Focus"=>-8,"Bell"=>8,"Refresh" or "RefreshRate"=>20,"Settings"=>8,_=>0};
        Keys(_feedbackRotation,RotateTransform.AngleProperty,(90,angle),(190,_glyph=="Bell"?-5:angle*.3),(285,_glyph=="Bell"?2:0),(390,0));
        double low=_glyph=="Bluetooth"?.90:.96,high=_glyph=="Bluetooth"?1.07:1.025;
        foreach(var property in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})Keys(_feedbackScale,property,(70,low),(180,high),(360,1));
    }
    private void DrawSymbol(DrawingContext dc,Geometry geometry)
    {
        if(_glyph=="Wifi"&&SignalStrength>=0&&geometry is GeometryGroup signal){dc.PushTransform(signal.Transform);for(int i=0;i<signal.Children.Count;i++){dc.PushOpacity(SignalStrength>=(i==0?1:i==1?40:70)?1:.2);dc.DrawGeometry(Brush,null,signal.Children[i]);dc.Pop();}dc.Pop();return;}
        if(_symbolLayers is null||_layerGlyph!=_glyph||geometry is not GeometryGroup group||Array.TrueForAll(_symbolLayers,layer=>layer.Amount==1)){dc.DrawGeometry(Brush,null,geometry);return;}
        dc.PushTransform(group.Transform);
        for(int i=0;i<group.Children.Count;i++){
            var part=group.Children[i];double amount=_symbolLayers[i].Amount;
            bool fixedPart=_glyph=="Volume"&&i==0||_glyph=="Brightness"&&i==group.Children.Count-1;
            if(fixedPart){dc.DrawGeometry(Brush,null,part);continue;}
            double scale=1-(1-amount)*.045;var transform=_symbolLayers[i].Transform;transform.ScaleX=transform.ScaleY=scale;
            dc.PushOpacity(.55+.45*amount);dc.PushTransform(transform);
            dc.DrawGeometry(Brush,null,part);dc.Pop();dc.Pop();
        }
        dc.Pop();
    }
}
