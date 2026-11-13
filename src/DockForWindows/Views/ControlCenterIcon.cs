using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Xml.Linq;

namespace DockForWindows.Views;

internal sealed partial class ControlCenterIcon : FrameworkElement
{
    // Official Ionicons v8 Filled SVGs, MIT. Source originals live in Assets/Ionicons.
    private static readonly Dictionary<string,string> Icons=new()
    {
        ["Refresh"]="refresh-circle",["RefreshRate"]="sync-circle",["Performance"]="speedometer",["AudioOutput"]="headset",
        ["Hdr"]="color-filter",["Screenshot"]="scan",["ScreenRecording"]="videocam",["KeepAwake"]="cafe",
        ["Microphone"]="mic",["VoiceInput"]="stats-chart",["Wifi"]="wifi",["Bluetooth"]="bluetooth",
        ["Bell"]="notifications",["NightLight"]="eye",["Airplane"]="airplane",["MobileHotspot"]="radio",["Focus"]="moon",["Projection"]="desktop",["Brightness"]="sunny",["Volume"]="volume-high",
        ["Muted"]="volume-mute",["Battery"]="battery-half",["DarkMode"]="contrast",["PowerSaver"]="flash",
        ["Media"]="musical-notes",["Settings"]="settings",["Chevron"]="caret-forward",["Close"]="close",
        ["Play"]="play",["Pause"]="pause",["Previous"]="play-skip-back",["Next"]="play-skip-forward",["Headphones"]="headset",
        // Settings sidebar and page headers.
        ["Options"]="options",["Grid"]="grid",["Palette"]="color-palette",["Sparkles"]="sparkles",["Desktop"]="desktop",["Folder"]="folder",["Calendar"]="calendar"
    };
    private static readonly Dictionary<string,Geometry> Cache=new();
    private string _glyph;
    private Geometry? _outgoing;
    private int _blendVersion;
    private readonly ScaleTransform _feedbackScale=new(1,1);
    private readonly RotateTransform _feedbackRotation=new();
    private static string Alias(string glyph)=>glyph switch{"\uE711"=>"Close","\uE76C"=>"Chevron","\uE713"=>"Settings","\uE769"=>"Pause","\uE768"=>"Play","\uE892"=>"Previous","\uE893"=>"Next",_=>glyph};
    public static IReadOnlyList<string> AvailableGlyphs=>Icons.Keys.OrderBy(x=>x).ToArray();
    public string Glyph=>_glyph;
    public static readonly DependencyProperty BrushProperty=DependencyProperty.Register(nameof(Brush),typeof(Brush),typeof(ControlCenterIcon),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty BlendProperty=DependencyProperty.Register("Blend",typeof(double),typeof(ControlCenterIcon),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
    internal static readonly DependencyProperty SignalStrengthProperty=DependencyProperty.Register(nameof(SignalStrength),typeof(int),typeof(ControlCenterIcon),new FrameworkPropertyMetadata(-1,FrameworkPropertyMetadataOptions.AffectsRender));
    internal int SignalStrength{get=>(int)GetValue(SignalStrengthProperty);set=>SetValue(SignalStrengthProperty,value);}
    public Brush Brush{get=>(Brush)GetValue(BrushProperty);set=>SetValue(BrushProperty,value);}
    public ControlCenterIcon(string glyph)
    {
        _glyph=Alias(glyph);IsHitTestVisible=false;RenderTransformOrigin=new Point(.5,.5);
        var transforms=new TransformGroup();transforms.Children.Add(_feedbackScale);transforms.Children.Add(_feedbackRotation);transforms.Children.Add(_symbolShift);RenderTransform=transforms;
    }
    private static Geometry? Shape(string glyph)
    {
        if(!Icons.TryGetValue(glyph,out var name))return null;
        if(Cache.TryGetValue(name,out var cached))return cached;
        var resource=Application.GetResourceStream(new Uri("/DockForWindows;component/Assets/Ionicons/"+name+".svg",UriKind.Relative))??throw new InvalidOperationException("Missing official icon: "+name);
        using var stream=resource.Stream;var svg=XDocument.Load(stream);
        var group=new GeometryGroup{FillRule=FillRule.Nonzero};
        foreach(var element in svg.Descendants()){
            double N(string key)=>double.Parse(element.Attribute(key)?.Value??"0",CultureInfo.InvariantCulture);
            var style=(element.Attribute("style")?.Value??"").Split(';',StringSplitOptions.RemoveEmptyEntries).Select(v=>v.Split(':',2)).Where(v=>v.Length==2).ToDictionary(v=>v[0].Trim(),v=>v[1].Trim());
            string V(string key,string fallback)=>element.Attribute(key)?.Value??style.GetValueOrDefault(key,fallback);
            double Number(string key,double fallback)=>double.TryParse(V(key,"").Replace("px",""),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:fallback;
            Geometry? geometry=element.Name.LocalName switch{
                "path"=>Geometry.Parse("F1 "+element.Attribute("d")!.Value),
                "circle"=>new EllipseGeometry(new Point(N("cx"),N("cy")),N("r"),N("r")),
                "ellipse"=>new EllipseGeometry(new Point(N("cx"),N("cy")),N("rx"),N("ry")),
                "rect"=>new RectangleGeometry(new Rect(N("x"),N("y"),N("width"),N("height")),N("rx"),N("ry")),
                "line"=>new LineGeometry(new Point(N("x1"),N("y1")),new Point(N("x2"),N("y2"))),
                "polyline"=>Geometry.Parse("M"+element.Attribute("points")!.Value),
                "polygon"=>Geometry.Parse("F1 M"+element.Attribute("points")!.Value+"Z"),_=>null};
            if(geometry is null)continue;
            if(V("fill","black")!="none"&&element.Name.LocalName is not "line" and not "polyline")group.Children.Add(geometry);
            if(V("stroke","none")!="none"){
                var pen=new Pen(Brushes.Black,Number("stroke-width",1)){StartLineCap=V("stroke-linecap","")=="round"?PenLineCap.Round:V("stroke-linecap","")=="square"?PenLineCap.Square:PenLineCap.Flat,EndLineCap=V("stroke-linecap","")=="round"?PenLineCap.Round:V("stroke-linecap","")=="square"?PenLineCap.Square:PenLineCap.Flat,LineJoin=V("stroke-linejoin","")=="round"?PenLineJoin.Round:PenLineJoin.Miter};
                group.Children.Add(geometry.GetWidenedPathGeometry(pen));
            }
        }
        var bounds=group.Bounds;if(bounds.IsEmpty)throw new InvalidOperationException("Empty official icon: "+name);
        double extent=Math.Max(bounds.Width,bounds.Height),sx=0,sy=0,count=0;
        double cx=bounds.X+bounds.Width/2,cy=bounds.Y+bounds.Height/2;
        if(OpticalMetrics.TryGetValue(name,out var metric)){extent=metric.Extent;cx=metric.X;cy=metric.Y;}
        else{
            // Only new assets/the offline generator need optical measurement; shipped icons use precomputed metrics.
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){
                var point=new Point(bounds.X+(x+.5)*bounds.Width/32,bounds.Y+(y+.5)*bounds.Height/32);
                if(group.FillContains(point)){sx+=point.X;sy+=point.Y;count++;}
            }
            if(count>0){cx+=Math.Clamp(sx/count-cx,-extent*.04,extent*.04);cy+=Math.Clamp(sy/count-cy,-extent*.04,extent*.04);}
        }
        var matrix=Matrix.Identity;matrix.Translate(-cx,-cy);matrix.Scale(1/extent,1/extent);group.Transform=new MatrixTransform(matrix);group.Freeze();Cache[name]=group;return group;
    }
    internal void ChangeGlyph(string glyph,bool animate)
    {
        glyph=Alias(glyph);if(glyph==_glyph)return;
        var next=Shape(glyph);var current=Shape(_glyph);double blend=(double)GetValue(BlendProperty);
        double start=0;
        if(ReferenceEquals(next,_outgoing)){_outgoing=current;start=1-blend;}else _outgoing=current;
        _glyph=glyph;int version=++_blendVersion;BeginAnimation(BlendProperty,null);SetValue(BlendProperty,1d);
        if(!animate||!IsLoaded||!SystemParameters.ClientAreaAnimation||SystemParameters.HighContrast){_outgoing=null;InvalidateVisual();return;}
        var animation=new DoubleAnimation(start,1,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
        animation.Completed+=(_,_)=>{if(version==_blendVersion){_outgoing=null;BeginAnimation(BlendProperty,null);}};BeginAnimation(BlendProperty,animation);
    }
    protected override void OnRender(DrawingContext dc)
    {
        var geometry=Shape(_glyph);if(geometry is null)return;double extent=Math.Min(ActualWidth,ActualHeight)*.88;
        var matrix=new Matrix(extent,0,0,extent,ActualWidth/2,ActualHeight/2);dc.PushTransform(new MatrixTransform(matrix));double blend=(double)GetValue(BlendProperty);
        if(_outgoing is not null){dc.PushOpacity(1-blend);dc.DrawGeometry(Brush,null,_outgoing);dc.Pop();}
        dc.PushOpacity(_outgoing is null?1:blend);DrawSymbol(dc,geometry);dc.Pop();dc.Pop();
    }
}
