using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DockForWindows.Models;
using DockForWindows.Services;
using System.Collections.Generic;
using Windows.Devices.Radios;

namespace DockForWindows.Views;

internal sealed class OverflowGrid:Grid {protected override Geometry? GetLayoutClip(Size layoutSlotSize)=>null;}
internal sealed class OverflowCanvas:Canvas {protected override Geometry? GetLayoutClip(Size layoutSlotSize)=>null;}

public sealed partial class ControlCenterWindow
{
    private readonly List<(Button Button,Func<string> Text)> _deviceRows=new();
    private static string NetworkText(WifiNetwork n)=>$"{(n.Connected?"✓ ":"")}{n.Name}{(n.Secure?"  锁定":"")}";
    private static string BluetoothText(BluetoothPeer p)=>$"{p.Name} · {(p.Connected?"已连接":p.Paired?"已配对":"可配对")}";
    private void UpdateDeviceLabels()
    {
        foreach(var row in _deviceRows){var text=row.Button.Content as TextBlock;if(row.Button.Content is Grid grid){text=grid.Children.OfType<TextBlock>().FirstOrDefault();if(row.Button.Tag is Func<WifiNetwork> read&&grid.Children.OfType<ControlCenterIcon>().FirstOrDefault() is {} icon){var network=read();icon.SignalStrength=(int)Math.Min(100u,network.Signal);System.Windows.Automation.AutomationProperties.SetHelpText(row.Button,$"信号强度 {network.Signal}%");}}if(text is null)continue;row.Button.IsEnabled=_openKind==ControlModule.Wifi?_connectivity.State(RadioKind.WiFi)==RadioState.On:_connectivity.State(RadioKind.Bluetooth)==RadioState.On;string value=row.Text();if(text.Text!=value)text.Text=value;}
        if(_detailVisible&&_openKind==ControlModule.Bluetooth&&_page==ControlPage.Module){if(_connectivity.State(RadioKind.Bluetooth)==RadioState.On)_connectivity.StartBluetoothDiscovery();else _connectivity.StopBluetoothDiscovery();}
    }
    private sealed class SafetyVisual
    {public VisualBrush Brush=null!;public RectangleGeometry Geometry=new();public GeometryDrawing Drawing=new();public bool Active;}
    private sealed class SafetyEdge
    {public Rectangle Edge=null!;public bool Left;public DrawingGroup Drawing=new();public DrawingBrush Brush=null!;public readonly RectangleGeometry Clip=new();public readonly Dictionary<FrameworkElement,SafetyVisual> Layers=new();}
    private readonly List<SafetyEdge> _safetyEdges=new();
    private void InstallSafetyFade(Grid host)
    {
        foreach(bool left in new[]{true,false}){
            var state=new SafetyEdge{Left=left};state.Drawing.ClipGeometry=state.Clip;state.Brush=new DrawingBrush(state.Drawing){ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill};
            var edge=new Rectangle{Width=SideGutter,HorizontalAlignment=left?HorizontalAlignment.Left:HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Stretch,IsHitTestVisible=false,Opacity=.65,Visibility=Visibility.Collapsed,Effect=new BlurEffect{Radius=4,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Performance},Tag="safety-fade",Fill=state.Brush};state.Edge=edge;
            edge.OpacityMask=new LinearGradientBrush(left?Colors.Transparent:Colors.White,left?Colors.White:Colors.Transparent,new Point(0,0),new Point(1,0));_safetyEdges.Add(state);host.Children.Add(edge);
        }
    }
    private void UpdateSafetyFade()
    {
        // Classic already has one continuous blurred backdrop; no duplicate edge-material blur.
        if(UsesClassicBlur){foreach(var edge in _safetyEdges)edge.Edge.Visibility=Visibility.Collapsed;SetGutterActive(false);return;}
        if(Content is not Grid host||host.ActualWidth<=0||host.ActualHeight<=0)return;
        bool gutter=false;
        foreach(var item in _safetyEdges){
            double x=item.Left?0:host.ActualWidth-SideGutter;var strip=new Rect(x,0,SideGutter,host.ActualHeight);bool visible=false;
            foreach(var layer in item.Layers.Values)layer.Active=false;
            void Include(FrameworkElement element){
                if(!element.IsVisible||element.Opacity<.001||element.ActualWidth<=0)return;
                // Only content that actually enters the gutter: Rect.IntersectsWith also counts a shared edge, and the
                // cards and detail page end exactly where the strips begin, which kept both strips re-rendering every card.
                var bounds=element.TransformToAncestor(host).TransformBounds(new Rect(element.RenderSize));var overlap=Rect.Intersect(bounds,strip);if(overlap.IsEmpty||overlap.Width<.5||overlap.Height<.5)return;visible=true;
                if(!item.Layers.TryGetValue(element,out var layer)){layer=new SafetyVisual{Brush=new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill}};layer.Drawing.Geometry=layer.Geometry;item.Layers.Add(element,layer);item.Drawing.Children.Add(layer.Drawing);}
                layer.Active=true;bounds.Offset(-x,0);if(layer.Geometry.Rect!=bounds)layer.Geometry.Rect=bounds;
                var viewbox=new Rect(element.RenderSize);if(layer.Brush.Viewbox!=viewbox)layer.Brush.Viewbox=viewbox;
            }
            foreach(var card in _modules)Include(card.Root);Include(_details);
            foreach(var layer in item.Layers.Values){var brush=layer.Active?layer.Brush:null;if(layer.Drawing.Brush!=brush)layer.Drawing.Brush=brush;}
            item.Edge.Visibility=visible?Visibility.Visible:Visibility.Collapsed;gutter|=visible;
            var clip=new Rect(0,0,SideGutter,host.ActualHeight);if(item.Clip.Rect!=clip){item.Clip.Rect=clip;item.Brush.Viewbox=clip;}
        }
        SetGutterActive(gutter);
    }
    private async Task RefreshEnteredDevices(ControlModule kind,int page)
    {
        try{
            if(kind==ControlModule.Bluetooth)await _connectivity.WaitForBluetoothEnumeration(_lifetime.Token);
            await Task.Delay(kind==ControlModule.Wifi?650:120,_lifetime.Token);
            if(!_closing&&_detailVisible&&_transition==page&&_page==ControlPage.Module&&_openKind==kind)OpenModule(kind,true);
        }catch(OperationCanceledException)when(_closing){}
    }
    private void RefreshDetailDevices()=>Run(async()=>
    {
        if(_openKind is not (ControlModule.Wifi or ControlModule.Bluetooth)||_page!=ControlPage.Module||!_detailRefresh.IsEnabled)return;
        var kind=_openKind.Value;_detailRefresh.IsEnabled=false;
        try{
            if(kind==ControlModule.Wifi){if(_connectivity.CanListNetworks){await _connectivity.ReadNetworks(true);await Task.Delay(450,_lifetime.Token);_networks=await _connectivity.ReadNetworks();}}
            else{_connectivity.StopBluetoothDiscovery();_connectivity.StartBluetoothDiscovery();await Task.Delay(650,_lifetime.Token);}
            if(!_closing&&_openKind==kind&&_detailVisible)OpenModule(kind,true);
        }finally{if(!_closing)_detailRefresh.IsEnabled=true;}
    });
}
