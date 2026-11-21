using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal sealed partial class FigmaWidgetView
{
    private readonly Dictionary<string,Size> _layoutSizes=new();
    private readonly Dictionary<string,Rect> _layoutRects=new();
    private readonly Dictionary<string,(double Width,double Height,double Radius,double Exponent)> _outlines=new();
    private bool _layoutDirty=true,_inLayout;
    private double _designScale=1;
    private double _cornerExponent=2.5,_textScale=1;
    private JsonElement RootNode=>_nodes.Values.First();
    internal Size NativeCanvasSize=>new(_scene.Width,_scene.Height);
    private static double SourceCorner(JsonElement n)
    {
        if(n.TryGetProperty("cr",out var radius))return radius.GetDouble();
        var corners=new[]{"topLeftRadius","topRightRadius","bottomLeftRadius","bottomRightRadius"}.Where(k=>n.TryGetProperty(k,out _)).Select(k=>D(n,k)).ToArray();
        return corners.Length==0?0:corners.Min();
    }
    private double Spacing(JsonElement n,double fontSize)
    {
        if(!n.TryGetProperty("ls",out var ls))return 0;
        return D(ls,"value")*(S(ls,"unit")=="PERCENT"?fontSize/100:1);
    }
    private Size TextSize(JsonElement n,double available)
    {
        var label=_text[S(n,"id")];
        var text=new FormattedText(label.Text,English,FlowDirection.LeftToRight,new Typeface(label.FontFamily,label.FontStyle,label.FontWeight,label.FontStretch),label.FontSize,label.Foreground,1);
        double width=text.WidthIncludingTrailingWhitespace+Spacing(n,label.FontSize)*Math.Max(0,label.Text.Length-1)+2;
        return new(Math.Max(.001,Math.Min(Math.Max(1,available),width)),D(n,"h")*label.FontSize/D(n,"fs",label.FontSize));
    }
    private static bool IsAuto(JsonElement n)=>S(n,"layoutMode") is "HORIZONTAL" or "VERTICAL";
    private double BorderInset(JsonElement n)=>B(n,"strokesIncludedInLayout")&&n.TryGetProperty("s",out var strokes)&&strokes.GetArrayLength()>0?D(n,"sw",1):0;
    private IEnumerable<JsonElement> FlowChildren(JsonElement n)=>ChildrenOf(n).Where(c=>S(c,"layoutPositioning")!="ABSOLUTE"&&(!c.TryGetProperty("v",out var v)||v.ValueKind!=JsonValueKind.False)&&_layers.ContainsKey(S(c,"id"))&&!B(c,"skip"));
    private Size MeasureNode(JsonElement n,double availableWidth,double availableHeight,double? forceWidth=null,double? forceHeight=null)
    {
        string id=S(n,"id");
        if(_text.ContainsKey(id)){
            var measured=TextSize(n,availableWidth);_layoutSizes[id]=measured;return measured;
        }
        bool auto=IsAuto(n),horizontal=S(n,"layoutMode")=="HORIZONTAL";
        double width=forceWidth??(S(n,"layoutSizingHorizontal")=="HUG"?availableWidth:Bounds(n).Width),height=forceHeight??Bounds(n).Height;
        double border=BorderInset(n),left=D(n,"paddingLeft")+border,right=D(n,"paddingRight")+border,top=D(n,"paddingTop")+border,bottom=D(n,"paddingBottom")+border;
        double contentWidth=Math.Max(1,width-left-right),contentHeight=Math.Max(1,height-top-bottom);
        var children=(auto?FlowChildren(n):ChildrenOf(n).Where(c=>_layers.ContainsKey(S(c,"id")))).ToArray();
        if(!auto){
            foreach(var child in children){var proposed=AnchoredRect(n,child,new Size(width,height));MeasureNode(child,proposed.Width,proposed.Height,proposed.Width,proposed.Height);}
        }else{
            foreach(var child in children)MeasureNode(child,contentWidth,contentHeight,!horizontal&&S(child,"layoutSizingHorizontal")=="FILL"?contentWidth:null,horizontal&&S(child,"layoutSizingVertical")=="FILL"?contentHeight:null);
            if(horizontal){
                var fills=children.Where(c=>S(c,"layoutSizingHorizontal")=="FILL").ToArray();
                if(fills.Length>0){double fixedWidth=children.Where(c=>S(c,"layoutSizingHorizontal")!="FILL").Sum(c=>_layoutSizes[S(c,"id")].Width);double remaining=Math.Max(1,(contentWidth-fixedWidth-D(n,"itemSpacing")*Math.Max(0,children.Length-1))/fills.Length);foreach(var child in fills)MeasureNode(child,remaining,contentHeight,remaining,null);}
            }
            if(children.Length>0){
                if(forceWidth==null&&S(n,"layoutSizingHorizontal")=="HUG")width=Math.Min(availableWidth,left+right+(horizontal?children.Sum(c=>_layoutSizes[S(c,"id")].Width)+D(n,"itemSpacing")*(children.Length-1):children.Max(c=>_layoutSizes[S(c,"id")].Width)));
                if(forceHeight==null&&S(n,"layoutSizingVertical")=="HUG")height=top+bottom+(horizontal?children.Max(c=>_layoutSizes[S(c,"id")].Height):children.Sum(c=>_layoutSizes[S(c,"id")].Height)+D(n,"itemSpacing")*(children.Length-1));
            }
        }
        var result=new Size(Math.Max(.001,width),Math.Max(.001,height));_layoutSizes[id]=result;return result;
    }
    private Rect AnchoredRect(JsonElement parent,JsonElement child,Size size)
    {
        var box=Bounds(child);var before=Bounds(parent);double x=box.X-before.X,y=box.Y-before.Y,w=box.Width,h=box.Height;
        double dx=size.Width-before.Width,dy=size.Height-before.Height;
        bool hasAsset=S(child,"asset").Length>0;
        bool stretchX=!hasAsset&&w>=before.Width*.5&&Math.Abs(x-(before.Width-x-w))<=2;
        bool stretchY=!hasAsset&&h>=before.Height*.5&&Math.Abs(y-(before.Height-y-h))<=2;
        string name=S(child,"n");
        if(stretchX)w=Math.Max(1,w+dx);
        else if(name.StartsWith("Tonearm")||name=="Mount inset"){}
        else if(_style.Scene.StartsWith("weather-atmosphere")&&name is "Solar arc" or "Soft solar wash" or "Horizon" or "Sun halo" or "Sun indicator" or "Diffused horizon light" or "Soft moonlight")x+=dx;
        else x+=Anchor(x,box.Width,before.Width)*dx;
        if(stretchY)h=Math.Max(1,h+dy);else if(_style.Kind==WidgetKind.Clock&&(S(child,"t")!="TEXT"||S(child,"text").All(char.IsDigit)))y+=dy/2;else y+=Anchor(y,box.Height,before.Height)*dy;
        return new(x,y,w,h);
    }
    private static double Anchor(double start,double length,double total)
    {
        double left=Math.Abs(start),right=Math.Abs(total-start-length),center=Math.Abs(start+length/2-total/2);
        return center<=left&&center<=right?.5:right<left?1:0;
    }
    private void ArrangeNode(JsonElement n,Rect rect,bool root=false)
    {
        string id=S(n,"id");SetLayerRect(n,rect,root);
        var children=ChildrenOf(n).Where(c=>_layers.ContainsKey(S(c,"id"))).ToArray();
        if(!IsAuto(n)){
            foreach(var child in children){var desired=AnchoredRect(n,child,rect.Size);if(_text.ContainsKey(S(child,"id"))){desired.Width=Math.Min(desired.Width,Math.Max(1,rect.Width-desired.X));_layoutSizes[S(child,"id")]=desired.Size;}ArrangeNode(child,desired);}
            return;
        }
        bool horizontal=S(n,"layoutMode")=="HORIZONTAL";double border=BorderInset(n);
        double start=(horizontal?D(n,"paddingLeft"):D(n,"paddingTop"))+border,end=(horizontal?D(n,"paddingRight"):D(n,"paddingBottom"))+border;
        double crossStart=(horizontal?D(n,"paddingTop"):D(n,"paddingLeft"))+border,crossEnd=(horizontal?D(n,"paddingBottom"):D(n,"paddingRight"))+border;
        double length=(horizontal?rect.Width:rect.Height)-start-end,crossLength=(horizontal?rect.Height:rect.Width)-crossStart-crossEnd;
        var flow=FlowChildren(n).ToArray();double gap=D(n,"itemSpacing");
        // A wider native grid preserves the original end insets of the fixed iPod wheel pair.
        if(root&&horizontal&&flow.Length>1&&flow.All(c=>S(c,"layoutSizingHorizontal")!="FILL"))gap+=(rect.Width-_bounds.Width)/(flow.Length-1);
        double used=flow.Sum(c=>horizontal?_layoutSizes[S(c,"id")].Width:_layoutSizes[S(c,"id")].Height)+gap*Math.Max(0,flow.Length-1);
        string align=S(n,"primaryAxisAlignItems");double cursor=start+(align=="MAX"?Math.Max(0,length-used):align=="CENTER"?Math.Max(0,(length-used)/2):0);
        if(align=="SPACE_BETWEEN"&&flow.Length>1)gap=Math.Max(0,(length-(used-gap*(flow.Length-1)))/(flow.Length-1));
        foreach(var child in flow){
            string childId=S(child,"id");var childSize=_layoutSizes[childId];
            if(_text.ContainsKey(childId)){double limit=horizontal?Math.Max(1,length-flow.Where(c=>S(c,"id")!=childId).Sum(c=>_layoutSizes[S(c,"id")].Width)-gap*(flow.Length-1)):crossLength;childSize.Width=Math.Max(.001,Math.Min(childSize.Width,limit));}
            string crossAlign=S(n,"counterAxisAlignItems");double childCross=horizontal?childSize.Height:childSize.Width;
            double cross=crossStart+(crossAlign=="CENTER"?(crossLength-childCross)/2:crossAlign=="MAX"?crossLength-childCross:0);
            var childRect=horizontal?new Rect(cursor,cross,childSize.Width,childSize.Height):new Rect(cross,cursor,childSize.Width,childSize.Height);
            ArrangeNode(child,childRect);cursor+=(horizontal?childSize.Width:childSize.Height)+gap;
        }
        foreach(var child in children.Where(c=>S(c,"layoutPositioning")=="ABSOLUTE"))ArrangeNode(child,AnchoredRect(n,child,rect.Size));
    }
    private void SetLayerRect(JsonElement n,Rect rect,bool root)
    {
        string id=S(n,"id");var layer=_layers[id];_layoutRects[id]=rect;
        double left=rect.X,top=rect.Y;
        if(_record!=null&&S(n,"n")=="VinylRecord") {Canvas.SetLeft(_record,left);Canvas.SetTop(_record,top);left=top=0;}
        else if(_record!=null&&S(n,"n")=="Tonearm"){
            var disc=_nodes.Values.First(c=>S(c,"n")=="VinylRecord");var discRect=_layoutRects.GetValueOrDefault(S(disc,"id"),new Rect(54,46,288,288));left-=discRect.X;top-=discRect.Y;
        }
        if(!root){Canvas.SetLeft(layer,left);Canvas.SetTop(layer,top);}
        bool transformed=n.TryGetProperty("matrix",out _);
        layer.Width=Math.Max(.001,transformed?D(n,"w"):rect.Width);layer.Height=Math.Max(.001,transformed?D(n,"h"):rect.Height);
        if(_text.TryGetValue(id,out var text)){
            ApplyTracking(text,n);
            var natural=new FormattedText(text.Text,English,FlowDirection.LeftToRight,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,1);
            double tracked=natural.WidthIncludingTrailingWhitespace+Spacing(n,text.FontSize)*Math.Max(0,text.Text.Length-1);
            if(!_data.DesignReference&&tracked>rect.Width+1&&Spacing(n,text.FontSize)<0){text.TextEffects.Clear();text.Width=rect.Width;}
            else text.Width=Math.Max(.001,rect.Width+Math.Max(0,-Spacing(n,text.FontSize)*Math.Max(0,text.Text.Length-1)));
            if(!_data.DesignReference&&text.Text.Any(char.IsDigit)&&text.Text.Contains(':')&&_nodes.TryGetValue(S(n,"p"),out var parent)&&!IsAuto(parent)){
                text.FontSize=D(n,"fs",text.FontSize)*_textScale;
                var digits=new FormattedText(text.Text,English,FlowDirection.LeftToRight,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,1);
                if(digits.WidthIncludingTrailingWhitespace>rect.Width)text.FontSize*=rect.Width/digits.WidthIncludingTrailingWhitespace;
            }
            text.Height=Math.Max(rect.Height+2,text.FontSize*1.3);
        }
        foreach(var border in layer.Children.OfType<Border>().Where(b=>S(n,"n")=="Up next")) {border.Width=rect.Width;border.Height=rect.Height;}
        double radius=SourceCorner(n),exponent=_cornerExponent;
        if(D(n,"w")==_bounds.Width&&D(n,"h")==_bounds.Height)radius=20/_designScale;
        radius=Math.Clamp(radius,0,Math.Min(layer.Width,layer.Height)/2);
        var key=(layer.Width,layer.Height,radius,exponent);
        if(!_outlines.TryGetValue(id,out var previous)||previous!=key){
            _outlines[id]=key;var outline=WidgetGeometry.Get(layer.Width,layer.Height,exponent,radius);
            // Use the existing true inset contour for the full-width inset player panel.
            if(_style.Id=="music-figma-ipod-horizontal"&&id=="1:105")outline=ControlCenterTokens.InsetOutline(new Size(layer.Width,layer.Height),20/_designScale,15,exponent);
            foreach(var shape in layer.Children.OfType<Shape>().Where(s=>!Equals(s.Tag,"figma-play")&&!Equals(s.Tag,"figma-accent"))){shape.Width=layer.Width;shape.Height=layer.Height;if(shape is System.Windows.Shapes.Path path)path.Data=outline;}
            if(!root&&S(n,"t") is "FRAME" or "COMPONENT" or "INSTANCE"&&!n.TryGetProperty("clip",out _))layer.Clip=outline;
        }
        foreach(var button in layer.Children.OfType<Button>()){button.Width=Math.Max(rect.Width,24);button.Height=Math.Max(rect.Height,24);}
    }
    private Point ScenePoint(string id)
    {
        Point point=new();JsonElement n=_nodes[id];
        while(_layoutRects.TryGetValue(S(n,"id"),out var rect)){point.X+=rect.X;point.Y+=rect.Y;if(!_nodes.TryGetValue(S(n,"p"),out n))break;}
        return point;
    }
    private void SyncTimelineGeometry()
    {
        var media=_data.Media;bool known=media?.Duration>TimeSpan.Zero;
        double fraction=known?DockForWindows.Services.MediaControlService.PositionAt(media!.Position,media.Duration,media.Playing,media.PlaybackRate,ControlMotion.Now-_sampleAt).TotalSeconds/media.Duration.TotalSeconds:0;
        foreach(var pair in _seeks){var at=ScenePoint(pair.Key);Canvas.SetLeft(pair.Value,at.X);Canvas.SetTop(pair.Value,at.Y-10);pair.Value.Width=_layers[pair.Key].Width;}
        foreach(var n in _nodes.Values){string name=S(n,"n");if(!_layers.TryGetValue(S(n,"id"),out var layer))continue;
            if(name.StartsWith("Elapsed progress")||name is "ProgressFill" or "Elapsed")if(_layers.TryGetValue(S(n,"p"),out var parent)){
                double width=Math.Max(.001,parent.Width*fraction);layer.Width=width;foreach(var shape in layer.Children.OfType<Shape>()){shape.Width=width;if(shape is System.Windows.Shapes.Path path)path.Data=WidgetGeometry.Get(width,layer.Height,_data.Options.Exponent,Math.Min(SourceCorner(n),Math.Min(width,layer.Height)/2));}
            }
            if(name=="Playhead"&&_nodes.TryGetValue(S(n,"p"),out var progress)){var track=ChildrenOf(progress).FirstOrDefault(c=>S(c,"n")=="Track");if(track.ValueKind!=JsonValueKind.Undefined){var trackRect=_layoutRects[S(track,"id")];Canvas.SetLeft(layer,trackRect.X+trackRect.Width*fraction-D(n,"w")/2);}}
        }
    }
    private void Relayout(bool force=false)
    {
        if(_inLayout||!_layoutDirty&&!force)return;_inLayout=true;
        try{
            double width=ActualWidth>0?ActualWidth:Definition.Size.Width,height=ActualHeight>0?ActualHeight:Definition.Size.Height;
            _designScale=Math.Min(width/_bounds.Width,height/_bounds.Height);if(_designScale<=0)return;
            var native=new Size(width/_designScale,height/_designScale);_scene.Width=native.Width;_scene.Height=native.Height;
            DependencyObject? owner=this;WidgetHost? host=null;while(owner!=null){if(owner is WidgetHost found){host=found;break;}owner=VisualTreeHelper.GetParent(owner);}
            _cornerExponent=host?.CornerExponent??_data.Options.Exponent;_textScale=host?.CardAppearance.TextScale??1;
            _layoutSizes.Clear();MeasureNode(RootNode,native.Width,native.Height,native.Width,native.Height);ArrangeNode(RootNode,new Rect(native),true);
            _layoutDirty=false;if(!_data.DesignReference)SyncTimelineGeometry();
        }finally{_inLayout=false;}
    }
}
