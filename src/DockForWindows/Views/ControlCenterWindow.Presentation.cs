using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private bool _applyingPreview;
    internal void ApplyPreview(DockSettings draft)
    {
        var next=draft.ControlCenter.Copy();next.Normalize();bool geometryChanged=System.Text.Json.JsonSerializer.Serialize(_options.Geometry)!=System.Text.Json.JsonSerializer.Serialize(next.Geometry);bool layoutChanged=!_options.Modules.Select(m=>m.Module).SequenceEqual(next.Modules.Select(m=>m.Module))||_options.ActivePageId!=next.ActivePageId||_options.Pages.Count!=next.Pages.Count||_options.Modules.Any(m=>next.Modules.Single(n=>n.Module==m.Module) is {} n&&(m.Visible!=n.Visible||m.EffectiveShape!=n.EffectiveShape||m.PageId!=n.PageId||m.Group!=n.Group||m.DisplayName!=n.DisplayName||m.Glyph!=n.Glyph));_settings.ApplyPreferences(draft);_material.ApplyPreferences(next.Material(draft));_options.Geometry=next.Geometry;
        foreach(var item in _options.Modules){var value=next.Modules.Single(m=>m.Module==item.Module);item.Visible=value.Visible;item.SetShape(value.EffectiveShape);item.PageId=value.PageId;item.Group=value.Group;item.DisplayName=value.DisplayName;item.Glyph=value.Glyph;}
        _options.Modules=next.Modules.Select(n=>_options.Modules.Single(m=>m.Module==n.Module)).ToList();_options.Pages=next.Pages;_options.ActivePageId=next.ActivePageId;_options.ColorMode=next.ColorMode;_options.MaterialMode=next.MaterialMode;ApplyMaterialMode();RefreshPagePicker();_detailLight=null;foreach(var card in _modules)card.Light=null;
        foreach(var surface in _surfaces){surface.GlassIntensity=_material.GlassIntensity;surface.GlassDistortion=_material.GlassDistortion;surface.GlassFrost=_material.GlassFrost;surface.GlassOpacity=_material.GlassOpacity;surface.GlassTint=_material.GlassTint;surface.GlassHighlight=_material.GlassHighlight;surface.GlassHighlightWidth=_material.GlassHighlightWidth;surface.GlassGrain=_material.GlassGrain;surface.InvalidateVisual();}
        if(!geometryChanged&&!layoutChanged)return;
        _fit.ScaleX=_fit.ScaleY=next.Geometry.PanelScale;_detailBody.LayoutTransform=new ScaleTransform(next.Geometry.ComponentScale,next.Geometry.ComponentScale);
        _stage.Width=(_options.Geometry.PanelWidth-36)*_options.Geometry.ComponentScale;Width=(_stage.Width+SideGutter*2)*_fit.ScaleX;
        _applyingPreview=true;try{foreach(var card in _modules)BindCard(card,_options.Modules.Single(m=>m.Module==card.Kind));SyncCards(true);}finally{_applyingPreview=false;}
        if(_detailVisible&&_openKind is {} kind)OpenModule(kind,true);PositionAtTray();
    }
    private readonly AdaptiveGlassAppearance _detailContrast=new();private bool? _detailLight;
    private bool _pointerPressed;
    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T:DependencyObject
    {if(root is T found)yield return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in VisualDescendants<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private void StyleCloseButton(Button back)
    {
        if(back.Content is ControlCenterIcon icon){icon.Brush=DarkInk;icon.Tag="keep";}
        var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);var circle=new FrameworkElementFactory(typeof(Border));circle.SetValue(Border.WidthProperty,20d);circle.SetValue(Border.HeightProperty,20d);circle.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);circle.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);circle.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(220,255,255,255)));circle.SetValue(Border.CornerRadiusProperty,new CornerRadius(100));if(back.Content is ControlCenterIcon mark)mark.Width=mark.Height=10;
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);circle.AppendChild(content);hit.AppendChild(circle);back.Template=new ControlTemplate(typeof(Button)){VisualTree=hit};
    }
    private void UpdateDetailForeground(BitmapSource frame)
    {
        if(!_detailVisible||_details.ActualWidth<=0||_details.ActualHeight<=0)return;
        var origin=_details.PointToScreen(new Point());var far=_details.PointToScreen(new Point(_details.ActualWidth,_details.ActualHeight));
        _detailContrast.Observe(frame,new Int32Rect((int)(origin.X-GlassBackdropBounds.X),(int)(origin.Y-GlassBackdropBounds.Y),Math.Max(1,(int)(far.X-origin.X)),Math.Max(1,(int)(far.Y-origin.Y))),_runtime.Elapsed.TotalSeconds);
        bool light=ResolveLight(_detailContrast.Luminance,_detailLight);
        if(_detailLight==light)return;_detailLight=light;TextForeground(_details,light?Brushes.White:DarkInk);TextForeground(_detailContent,light?Brushes.White:DarkInk);TextForeground(_detailHeader,light?Brushes.White:DarkInk);
    }
    private void ObserveBackgroundPress(bool down)
    {
        bool fresh=down&&!_pointerPressed;_pointerPressed=down;
        if(!fresh||!DismissOnDeactivate||!_ready||_closing||_systemDialog||_resizing||_drag is not null||_runtime.Elapsed.TotalSeconds<.5)return;
        var cursor=System.Windows.Forms.Cursor.Position;BackgroundPress(new Point(cursor.X,cursor.Y));
    }
    private void BackgroundPress(Point screen)
    {
        if(ControlCenterEntryService.IsDedicatedButton(screen))return;
        if(_returnPress)return;if(_detailVisible){var detailHit=InputHitTest(PointFromScreen(screen)) as DependencyObject;if(detailHit is not null&&_details.IsAncestorOf(detailHit)&&InInteractive(detailHit))return;ReturnFromBackdrop();return;}
        var hit=InputHitTest(PointFromScreen(screen)) as DependencyObject;if(InInteractive(hit)||hit is not null&&((Grid)_root.Children[0]).IsAncestorOf(hit))return;
        foreach(var card in _modules){var p=card.Surface.PointFromScreen(screen);if(DockSurface.CreateGeometry(card.Surface.RenderSize,card.Surface.Radius,0,card.Surface.CornerExponent).FillContains(p))return;}
        Dismiss();
    }
    private void StretchSlider(ControlModule kind,double stretch,bool release)
    {
        var c=_modules.FirstOrDefault(x=>x.Kind==kind);if(c is null)return;
        if(release){if(Animate){ControlMotion.To(c.Stretch,ScaleTransform.ScaleXProperty,1,damping:.66);ControlMotion.To(c.Stretch,ScaleTransform.ScaleYProperty,1,damping:.66);}else{ControlMotion.Stop(c.Stretch,ScaleTransform.ScaleXProperty,1);ControlMotion.Stop(c.Stretch,ScaleTransform.ScaleYProperty,1);}return;}
        c.Surface.RenderTransformOrigin=new Point(stretch>=0?0:1,.5);
        ControlMotion.Stop(c.Stretch,ScaleTransform.ScaleXProperty,1+Math.Abs(stretch)/Math.Max(1,c.Surface.ActualWidth));ControlMotion.Stop(c.Stretch,ScaleTransform.ScaleYProperty,1-Math.Abs(stretch)/440);
    }
}
