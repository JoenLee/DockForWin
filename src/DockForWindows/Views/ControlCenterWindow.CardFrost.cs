using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DockForWindows.Views;
public sealed partial class ControlCenterWindow
{
    private ClassicBlurBackdrop? _classicCards;
    private readonly DrawingGroup _cardFrostDrawing=new();
    private DrawingBrush? _cardFrostMask;
    private readonly Dictionary<DockSurface,FrostMask> _cardFrostMasks=new();
    private sealed class FrostMask
    {
        internal Size Size;internal double Radius=-1,Exponent=-1;
        internal GeometryDrawing Shape=new(){Brush=Brushes.White};
        internal MatrixTransform Transform=new();internal DrawingGroup Drawing=new();
        internal FrostMask(){Drawing.Children.Add(Shape);Drawing.Transform=Transform;}
    }
    private void UpdateCardFrost()
    {
        if(_classicCards is null||Content is not System.Windows.Controls.Grid host)return;
        if(_cardFrostMask is null){_cardFrostMask=new DrawingBrush(_cardFrostDrawing){ViewboxUnits=BrushMappingMode.Absolute,ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill};_classicCards.OpacityMask=_cardFrostMask;}
        var bounds=new Rect(host.RenderSize);if(bounds.IsEmpty||bounds.Width<=0||bounds.Height<=0)return;
        if(_cardFrostMask.Viewbox!=bounds){_cardFrostMask.Viewbox=bounds;_cardFrostMask.Viewport=bounds;}
        foreach(var surface in _surfaces){
            if(!_cardFrostMasks.TryGetValue(surface,out var mask)){mask=new();_cardFrostMasks.Add(surface,mask);_cardFrostDrawing.Children.Add(mask.Drawing);}
            double opacity=surface.IsVisible?1:0;
            for(DependencyObject? node=surface;opacity>0&&node!=host;node=VisualTreeHelper.GetParent(node!)){if(node is null){opacity=0;break;}if(node is UIElement element)opacity*=element.Opacity;}
            if(mask.Drawing.Opacity!=opacity)mask.Drawing.Opacity=opacity;
            if(opacity<.001||surface.ActualWidth<=0||surface.ActualHeight<=0)continue;
            if(mask.Size!=surface.RenderSize||mask.Radius!=surface.Radius||mask.Exponent!=surface.CornerExponent){mask.Size=surface.RenderSize;mask.Radius=surface.Radius;mask.Exponent=surface.CornerExponent;mask.Shape.Geometry=DockSurface.CreateGeometry(mask.Size,mask.Radius,0,mask.Exponent);}
            var transform=surface.TransformToAncestor(host);var origin=transform.Transform(new Point());var x=transform.Transform(new Point(1,0));var y=transform.Transform(new Point(0,1));
            var matrix=new Matrix(x.X-origin.X,x.Y-origin.Y,y.X-origin.X,y.Y-origin.Y,origin.X,origin.Y);
            if(mask.Transform.Matrix!=matrix)mask.Transform.Matrix=matrix;
        }
    }
    private void ForgetCardFrost(DockSurface surface)
    {if(_cardFrostMasks.Remove(surface,out var mask))_cardFrostDrawing.Children.Remove(mask.Drawing);}
    private void ReleaseCardFrost(){_classicCards?.Release();_cardFrostMasks.Clear();_cardFrostDrawing.Children.Clear();}
}
