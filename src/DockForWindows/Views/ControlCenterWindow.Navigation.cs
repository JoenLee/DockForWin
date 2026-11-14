using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    // Move the existing controls into fixed layout hosts. Only their composition transforms
    // change while the shared glass outline grows; no nested live VisualBrush is repainted.
    private sealed class NavigationInk:FrameworkElement
    {
        private readonly Card _source;private readonly FrameworkElement _body,_header;private readonly Grid _destination;
        private readonly Grid _sourceHost=new OverflowGrid(),_targetHost=new OverflowGrid();
        private readonly ScaleTransform _sourceScale=new(),_targetScale=new();private readonly Size _sourceSize;
        private Size _targetSize;private readonly int _sourceIndex;private bool _released;
        public NavigationInk(Card source,Grid body,Grid header,Grid destination,double growth)
        {
            IsHitTestVisible=false;_source=source;_body=body;_header=header;_destination=destination;_sourceSize=source.Root.RenderSize;
            _sourceIndex=source.Root.Children.IndexOf(source.Body);source.Root.Children.Remove(source.Body);_sourceHost.Children.Add(source.Body);
            destination.Children.Remove(body);destination.Children.Remove(header);_targetHost.Children.Add(body);_targetHost.Children.Add(header);
            _sourceHost.RenderTransform=_sourceScale;_targetHost.RenderTransform=_targetScale;AddVisualChild(_sourceHost);AddVisualChild(_targetHost);
            // Both pages only scale and cross-fade inside the growing outline; cached, each is one texture per frame. The
            // card's content grows to the destination's width, so it is cached at that scale to stay sharp while it fades.
            _sourceHost.CacheMode=new BitmapCache{RenderAtScale=growth,EnableClearType=false};_targetHost.CacheMode=new BitmapCache{RenderAtScale=1,EnableClearType=false};
        }
        protected override int VisualChildrenCount=>2;
        protected override Visual GetVisualChild(int i)=>i switch{0=>_sourceHost,1=>_targetHost,_=>throw new ArgumentOutOfRangeException(nameof(i))};
        protected override Geometry? GetLayoutClip(Size size)=>null;
        protected override Size MeasureOverride(Size available){_sourceHost.Measure(_sourceSize);_targetHost.Measure(_targetSize);return new Size();}
        protected override Size ArrangeOverride(Size size){ArrangeHosts();return size;}
        private void ArrangeHosts(){_sourceHost.Arrange(new Rect(_sourceSize));if(_targetSize.Width>0&&_targetSize.Height>0)_targetHost.Arrange(new Rect(_targetSize));}
        public void Update(double progress,Geometry outline,Size destination)
        {
            if(_targetSize!=destination){_targetSize=destination;_targetHost.Measure(destination);ArrangeHosts();}
            Clip=outline;double p=Math.Clamp(progress,0,1),blend=p*p*(3-2*p);
            _sourceHost.Opacity=1-blend;_targetHost.Opacity=blend;
            _sourceScale.ScaleX=_sourceScale.ScaleY=RenderSize.Width/Math.Max(1,_sourceSize.Width);
            _targetScale.ScaleX=_targetScale.ScaleY=RenderSize.Width/Math.Max(1,_targetSize.Width);
        }
        public void Release()
        {
            if(_released)return;_released=true;_sourceHost.Children.Remove(_source.Body);_source.Root.Children.Insert(Math.Min(_sourceIndex,_source.Root.Children.Count),_source.Body);
            _targetHost.Children.Remove(_body);_targetHost.Children.Remove(_header);_destination.Children.Add(_body);_destination.Children.Add(_header);
        }
    }
    private sealed class NavigationLayer:FrameworkElement
    {
        private readonly DockSurface _plate;private NavigationInk? _ink;private Size _size=new(1,1);
        public NavigationLayer(DockSurface plate){_plate=plate;IsHitTestVisible=false;AddVisualChild(plate);}
        public void Content(NavigationInk ink){if(_ink is not null)RemoveVisualChild(_ink);_ink=ink;AddVisualChild(ink);ink.Arrange(new Rect(_size));}
        protected override int VisualChildrenCount=>_ink is null?1:2;
        protected override Visual GetVisualChild(int i)=>i switch{0=>_plate,1 when _ink is not null=>_ink,_=>throw new ArgumentOutOfRangeException(nameof(i))};
        protected override Size MeasureOverride(Size available){_plate.Measure(_size);_ink?.Measure(_size);return new Size();}
        protected override Size ArrangeOverride(Size final){ArrangeChildren();return final;}
        protected override Geometry? GetLayoutClip(Size size)=>null;
        private void ArrangeChildren(){var rect=new Rect(_size);_plate.Arrange(rect);_ink?.Arrange(rect);}
        public void Resize(Size size){if(_size==size)return;_size=size;ArrangeChildren();}
        public void Update(double progress,Geometry outline,Size destination)=>_ink?.Update(progress,outline,destination);
        public void Release()=>_ink?.Release();
    }
    private Grid _detailGlassHost=null!;private NavigationLayer? _navigationLayer;
    private DockSurface? _navigationGlass;private Card? _returnCard;
    private readonly TranslateTransform _navigationPosition=new();
    private Rect _navigationFrom,_navigationCurrent,_navigationDestination;
    private Card? _navigationCard;private double _navigationDestinationRadius;
    private double _navigationStarted=-1,_navigationRadiusFrom,_navigationRadius;
    private bool _navigationForward;private ControlMotion.SpringPath _navigationPath;
    private void StartNavigationGlass(bool forward,Card? source)
    {
        if(!Animate||source is null){FinishNavigationGlass();return;}
        if(_navigationGlass is null){_navigationGlass=Surface(0);_navigationGlass.CacheMode=null; // reshaped every frame: a cache would only add a pass
            _navigationLayer=new NavigationLayer(_navigationGlass){RenderTransform=_navigationPosition};Panel.SetZIndex(_navigationLayer,3);_stage.Children.Add(_navigationLayer);}
        bool continuing=_navigationStarted>=0&&_navigationCard==source;double now=_runtime.Elapsed.TotalSeconds;
        double progress=continuing?_navigationPath.Value(now-_navigationStarted):forward?0:1,velocity=continuing?_navigationPath.Velocity(now-_navigationStarted):0;
        if(!continuing){
            _navigationLayer!.Release(); // Restore the previous hosts before transferring another card.
            _navigationFrom=forward?source.Root.TransformToAncestor(_stage).TransformBounds(new Rect(source.Root.RenderSize)):source.Target;
            _navigationRadiusFrom=Math.Min(_navigationFrom.Width,_navigationFrom.Height)*source.Surface.Radius/200;
            _navigationDestination=new Rect(0,0,Math.Max(1,_stage.Width),Math.Max(1,_details.ActualHeight));_navigationDestinationRadius=DetailPhysicalRadius(_navigationDestination.Size);
            _navigationLayer!.Content(new NavigationInk(source,_detailContent,_detailHeader,_details,Math.Clamp(_navigationDestination.Width/Math.Max(1,source.Root.RenderSize.Width),1,5)));
        }
        if(_navigationCard is {} old&&old!=source)ControlMotion.Stop(old.Root,OpacityProperty,1);
        _returnCard=source;_navigationCard=source;_navigationForward=forward;
        _navigationPath=new(progress,forward?1:0,velocity,ControlMotion.PageSpeed*(forward?1:ControlMotion.ExitSpeed),ControlMotion.PageDamping);_navigationStarted=now;
        ControlMotion.Stop(source.Root,OpacityProperty,0);ControlMotion.Stop(_cards,OpacityProperty,1-progress);ControlMotion.Stop(_details,OpacityProperty,0);
        _navigationLayer!.Visibility=Visibility.Visible;_navigationGlass.Visibility=Visibility.Visible;_detailGlassHost.Opacity=0;_details.IsHitTestVisible=false;
        StepNavigationGlass();
    }
    private double DetailPhysicalRadius(Size size)=>Math.Min(Math.Min(size.Width,size.Height)/2,_stage.Width*.5*Math.Min(35,_options.Geometry.DetailRoundness)/100);
    private void StepNavigationGlass()
    {
        if(_navigationStarted<0||_navigationGlass is null||_navigationCard is null)return;
        double elapsed=_runtime.Elapsed.TotalSeconds-_navigationStarted,ease=_navigationPath.Value(elapsed);
        if(_navigationForward){_navigationDestination=new Rect(0,0,Math.Max(1,_stage.Width),Math.Max(1,double.IsFinite(_details.Height)?_details.Height:_details.ActualHeight));_navigationDestinationRadius=DetailPhysicalRadius(_navigationDestination.Size);}
        double Mix(double a,double b)=>a+(b-a)*ease;
        _navigationCurrent=new Rect(Mix(_navigationFrom.X,_navigationDestination.X),Mix(_navigationFrom.Y,_navigationDestination.Y),Math.Max(1,Mix(_navigationFrom.Width,_navigationDestination.Width)),Math.Max(1,Mix(_navigationFrom.Height,_navigationDestination.Height)));
        _navigationRadius=Math.Max(0,Mix(_navigationRadiusFrom,_navigationDestinationRadius));
        _navigationLayer!.Resize(_navigationCurrent.Size);_navigationPosition.X=_navigationCurrent.X;_navigationPosition.Y=_navigationCurrent.Y;
        _navigationGlass.Radius=Math.Clamp(_navigationRadius/Math.Min(_navigationCurrent.Width,_navigationCurrent.Height)*200,0,100);
        double p=Math.Clamp(ease,0,1),sourceExponent=_navigationCard.Surface.CornerExponent;
        if(sourceExponent==1.5)sourceExponent=2;
        _navigationGlass.CornerExponent=sourceExponent+(3-Math.Pow(_detailSurface.Radius/100,8)-sourceExponent)*p;
        _navigationGlass.InvalidateVisual();
        _navigationLayer.Update(ease,DockSurface.CreateGeometry(_navigationCurrent.Size,_navigationGlass.Radius,0,_navigationGlass.CornerExponent),_navigationDestination.Size);_cards.Opacity=1-p;
        if(elapsed>=_navigationPath.Duration)FinishNavigationGlass();
    }
    private void FinishNavigationGlass()
    {
        ((Grid)Content).Background=_detailVisible?new SolidColorBrush(Color.FromArgb(1,0,0,0)):null;
        _navigationLayer?.Release();if(_navigationCard is {} source)ControlMotion.Stop(source.Root,OpacityProperty,1);
        _navigationStarted=-1;_navigationCard=null;
        if(_navigationLayer is not null)_navigationLayer.Visibility=Visibility.Collapsed;
        if(_navigationGlass is not null)_navigationGlass.Visibility=Visibility.Collapsed;
        if(_detailGlassHost is not null)_detailGlassHost.Opacity=1;
        ControlMotion.Stop(_details,OpacityProperty,_detailVisible?1:0);ControlMotion.Stop(_cards,OpacityProperty,_detailVisible?0:1);
        _details.Visibility=_detailVisible?Visibility.Visible:Visibility.Collapsed;_cards.Visibility=_detailVisible?Visibility.Collapsed:Visibility.Visible;
        _details.IsHitTestVisible=_detailVisible;_cards.IsHitTestVisible=!_detailVisible;_transitioning=false;_chromeDirty=true;
    }
}
