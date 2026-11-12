using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DockForWindows.Models;
using DockForWindows.Services;
using Windows.Devices.Radios;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private ControlModule? _lastDragTarget;private Rect _dragGate=Rect.Empty;
    private void SyncCards(bool animate)
    {
        foreach(var removed in _modules.Where(c=>!_options.Modules.Any(m=>m.Module==c.Kind&&m.Visible&&m.PageId==_options.ActivePageId)).ToArray())
        {
            _modules.Remove(removed);
            if(removed.Kind==ControlModule.Media){_play=_previous=_next=_mediaIcon=null;}
            if(removed.Kind==ControlModule.Volume)_mute=null;
            void Finish(){_cards.Children.Remove(removed.Root);removed.Body.Children.Clear();removed.Surface.Backdrop=null;_surfaces.Remove(removed.Surface);ForgetCardFrost(removed.Surface);_surfaceFrames.Remove(removed.Surface);}
            if(animate&&Animate&&_ready){
                removed.Root.IsHitTestVisible=false;Scale(removed.Scale,.76,190);
                ControlMotion.To(removed.Root,OpacityProperty,0,speed:ControlMotion.ExitSpeed,completed:Finish);
            }else Finish();
        }
        foreach(var layout in _options.Modules.Where(m=>m.Visible&&m.PageId==_options.ActivePageId))
        {
            var card=_modules.FirstOrDefault(c=>c.Kind==layout.Module);
            if(card is null)
            {
                card=new Card{Kind=layout.Module,Surface=Surface(0)};var c=card;c.Surface.RenderTransform=c.Stretch;_modules.Add(c);c.Root.Children.Add(c.Surface);c.Root.Children.Add(c.Accent);c.Root.Children.Add(c.Body);
                var transforms=new TransformGroup();transforms.Children.Add(c.Scale);transforms.Children.Add(c.Enter);transforms.Children.Add(c.Position);c.Root.RenderTransform=transforms;c.Root.RenderTransformOrigin=new Point(.5,.5);
                c.Root.SizeChanged+=(_,_)=>{c.Accent.Data=DockSurface.CreateGeometry(c.Root.RenderSize,c.Surface.Radius,0,c.Surface.CornerExponent);UpdateCardInsets(c);};
                c.Root.MouseEnter+=(_,_)=>{if(c.Kind!=ControlModule.Media&&!_editing&&_runtime.Elapsed.TotalSeconds>_openingUntil)Scale(c.Scale,1,160);};c.Root.MouseLeave+=(_,_)=>{if(c.Kind!=ControlModule.Media&&!_editing&&_runtime.Elapsed.TotalSeconds>_openingUntil)Scale(c.Scale,1,350);};
                c.Root.PreviewMouseLeftButtonDown+=(_,e)=>{if(!_editing&&!InInteractive(e.OriginalSource as DependencyObject))ButtonPressFeedback(c.Scale,.975);if(!_editing||InInteractive(e.OriginalSource as DependencyObject))return;_drag=c;_dragStart=e.GetPosition(_cards);_grab=e.GetPosition(c.Root);_dragged=false;_lastDragTarget=null;_dragGate=Rect.Empty;c.Root.CaptureMouse();e.Handled=true;};
                c.Root.PreviewMouseMove+=(_,e)=>DragCard(e.GetPosition(_cards));c.Root.PreviewMouseLeftButtonUp+=(_,e)=>{if(!_editing)ButtonPressFeedback(c.Scale,1);if(_drag is null)return;EndDrag();e.Handled=true;};
                c.Root.LostMouseCapture+=(_,_)=>{if(_drag==c)EndDrag();else if(!_editing)Scale(c.Scale,1,460);};
                // 点击卡片任意空白处展开二级页面；滑块/开关/按钮自行处理事件不冒泡至此。
                c.Root.MouseLeftButtonUp+=(_,e)=>{if(!_editing&&_drag is null&&!_dragged&&!InInteractive(e.OriginalSource as DependencyObject)){if(Animate&&c.Primary?.Content is ControlCenterIcon symbol)symbol.Feedback();OpenModule(c.Kind);e.Handled=true;}};
                c.Root.ContextMenu=new ContextMenu();c.Root.ContextMenuOpening+=(_,e)=>{if(!_editing)e.Handled=true;};
                foreach(var shape in ControlCenterLayout.Shapes(c.Kind))
                {var chosen=shape;var item=new MenuItem{Header=ControlCenterCatalog.ShapeName(shape)};item.Click+=(_,_)=>{var current=_options.Modules.Single(m=>m.Module==c.Kind);current.SetShape(chosen);BindCard(c,current);LayoutCards(true);PersistLayout();};c.Root.ContextMenu.Items.Add(item);}
                AddCardManagement(c,layout);c.Root.ContextMenu.Items.Add(new Separator());var remove=new MenuItem{Header="移除此控件"};remove.Click+=(_,_)=>{_options.Modules.Single(m=>m.Module==c.Kind).Visible=false;SyncCards(true);PersistLayout();};c.Root.ContextMenu.Items.Add(remove);
                AutomationProperties.SetAutomationId(c.Root,"control-card-"+c.Kind);_cards.Children.Add(c.Root);BindCard(c,layout);if(animate&&Animate&&_ready){ControlMotion.To(c.Root,OpacityProperty,1,0);c.Scale.ScaleX=c.Scale.ScaleY=.78;Scale(c.Scale,1,380);}
            }
            else if(card.Shape!=layout.EffectiveShape)BindCard(card,layout);
            if(card.Grip is not null)card.Grip.Visibility=_editing?Visibility.Visible:Visibility.Collapsed;if(card.EditBadge is not null)card.EditBadge.Visibility=_editing?Visibility.Visible:Visibility.Collapsed;
        }
        if(!_cards.Children.Contains(_edit))_cards.Children.Add(_edit);if(!_cards.Children.Contains(_add))_cards.Children.Add(_add);
        _edit.Content=new TextBlock{Text=_editing?"完成排列":"编辑控制"};_edit.Width=120;_add.Width=90;_add.Visibility=_editing?Visibility.Visible:Visibility.Collapsed;
        _edit.ToolTip="拖动卡片排列；拖动右下角吸附预设规格；右键选择形状或移除。";LayoutCards(animate);
        if(_ready){Dispatcher.BeginInvoke(RefreshChrome);if(!_applyingPreview)_ = PollSystem();}
    }
    private static bool IsGrip(DependencyObject? source)
    {while(source is not null){if(source is Thumb)return true;source=source is Visual?VisualTreeHelper.GetParent(source):null;}return false;}
    private static bool InInteractive(DependencyObject? source)
    {while(source is not null){if(source is Slider or ControlCenterToggle or ButtonBase or Thumb)return true;source=source is Visual?VisualTreeHelper.GetParent(source):null;}return false;}
    private void BindCard(Card c,ControlModuleLayout layout)
    {
        string previous=c.Status.Text;c.Shape=layout.EffectiveShape;
        c.Surface.Radius=c.Kind==ControlModule.Media?_options.Geometry.MediaRoundness:c.Shape switch{ControlCardShape.Circle=>100,ControlCardShape.Capsule=>_options.Geometry.CapsuleRoundness,ControlCardShape.Square=>_options.Geometry.SquareRoundness,_=>_options.Geometry.RectangleRoundness};
        c.Body.Children.Clear();c.Body.RowDefinitions.Clear();c.Body.ColumnDefinitions.Clear();c.Title=null;c.Holder=null;
        c.Body.LayoutTransform=new ScaleTransform(_options.Geometry.ComponentScale,_options.Geometry.ComponentScale);
        if(c.Grip is not null)c.Root.Children.Remove(c.Grip);
        // The content group is optically centered; text inside the group remains left aligned.
        double zoom=_options.Geometry.ComponentScale; double insetX=(c.Shape==ControlCardShape.Circle?5:Math.Clamp(_options.Geometry.CardPadding,18,22))*zoom,insetY=(c.Shape==ControlCardShape.Circle?4:9)*zoom;
        if(c.Kind==ControlModule.Media){insetX=insetY=PlayerPadding(c.Shape)*zoom;}
        c.Body.Margin=new Thickness(insetX,insetY,insetX,insetY);c.Body.ClipToBounds=true;c.Surface.CornerExponent=c.Shape==ControlCardShape.Circle||c.Kind==ControlModule.Media?0:c.Shape==ControlCardShape.Capsule||c.Kind is ControlModule.Volume or ControlModule.Brightness?1.5:0;
        c.Status=new TextBlock{Text=string.IsNullOrEmpty(previous)?"正在读取":previous,FontSize=c.Shape==ControlCardShape.Circle?10:11,Opacity=.78,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
        Action primary=()=>PrimaryAction(c.Kind);
        var icon=ActionButton(ModuleGlyph(c.Kind),ModuleName(c.Kind),primary,Math.Max(20,_options.Geometry.IconSize));
        c.Primary=icon;
        if(c.Kind==ControlModule.Volume){_mute=icon;AutomationProperties.SetAutomationId(icon,"control-center-mute");}
        if(c.Kind==ControlModule.Media){_mediaIcon=icon;}
        if(c.Shape==ControlCardShape.Circle)
        {
            icon.Width=icon.Height=Math.Max(_options.Geometry.IconSize,22);
            var group=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};icon.HorizontalAlignment=HorizontalAlignment.Center;
            group.Children.Add(icon);c.Status.Visibility=Visibility.Collapsed;c.Body.Children.Add(group);
            if(c.Kind==ControlModule.Media){_play=_previous=_next=null;c.Status.Text="播放";}
        }
        else if(c.Kind is ControlModule.Brightness or ControlModule.Volume)
        {
            c.Body.RowDefinitions.Add(new(){Height=GridLength.Auto});c.Body.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
            // 回退到旧版样式：标题行（图标+名称+百分比+右侧箭头）+ 下方滑块（IosStyles 原版：细轨道+白色拇指）。
            var header=new Grid();header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.Children.Add(icon);
            var labels=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(_options.Geometry.ContentGap,0,0,0)};
            c.Title=new TextBlock{Text=ModuleName(c.Kind),FontWeight=FontWeights.SemiBold,FontSize=_options.Geometry.FontSize,VerticalAlignment=VerticalAlignment.Center};c.Status.FontSize=_options.Geometry.FontSize;c.Status.Margin=new Thickness(6,0,0,0);labels.Children.Add(c.Title);labels.Children.Add(c.Status);Grid.SetColumn(labels,1);header.Children.Add(labels);DetailHotspot(labels,c.Kind);
            c.Body.Children.Add(header);
            c.Body.ClipToBounds=false;var slider=c.Kind==ControlModule.Volume?_volume:_brightness;slider.Resources["ControlSliderAccent"]=Palette.Brush("#0A84FF");slider.Style=BlueSliderStyle();Detach(slider);slider.VerticalAlignment=VerticalAlignment.Center;slider.Margin=new Thickness(1,0,1,0);Grid.SetRow(slider,1);c.Body.Children.Add(slider);
        }
        else if(c.Kind==ControlModule.Media)
        {
            BuildMediaCard(c,icon);
        }
        else
        {
            // 参考图：白圆彩标 / 深圆白标的圆形图标容器 + 左对齐文字，无 chevron。
            var header=new Grid{VerticalAlignment=VerticalAlignment.Center};header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            if(c.Kind==ControlModule.Battery)
            {
                icon.Width=icon.Height=24;header.Children.Add(icon);
                var labels=new StackPanel{Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};
                c.Title=new TextBlock{Text=previous.Split(" · ")[0],FontSize=15,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};
                c.Status=new TextBlock{Text=previous.Contains(" · ")?previous[(previous.IndexOf(" · ",StringComparison.Ordinal)+3)..]:"",FontSize=10,Opacity=.6,TextTrimming=TextTrimming.CharacterEllipsis};
                labels.Children.Add(c.Title);labels.Children.Add(c.Status);Grid.SetColumn(labels,1);header.Children.Add(labels);DetailHotspot(labels,c.Kind);
            }
            else
            {
                // 参考图：圆形图标容器即可点击开关（Wi-Fi/蓝牙/深色/省电），不再放右侧药丸开关。
                header.Children.Add(IconHolder(c,icon,c.Shape==ControlCardShape.Square?54:44));
                var labels=new StackPanel{Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};
                c.Title=new TextBlock{Text=ModuleName(c.Kind),FontSize=14,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};
                c.Status.FontSize=11;c.Status.Opacity=.68;c.Status.Margin=new Thickness(0,2,0,0);
                labels.Children.Add(c.Title);labels.Children.Add(c.Status);Grid.SetColumn(labels,1);header.Children.Add(labels);DetailHotspot(labels,c.Kind);
            }
            c.Body.Children.Add(header);
        }
        var grip=new Thumb{Width=27,Height=27,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,1*zoom,1*zoom),LayoutTransform=new ScaleTransform(zoom,zoom),Cursor=Cursors.SizeNWSE,Visibility=_editing?Visibility.Visible:Visibility.Collapsed};
        var gripHit=new FrameworkElementFactory(typeof(Grid));gripHit.SetValue(Grid.BackgroundProperty,Brushes.Transparent);var path=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));path.SetValue(System.Windows.Shapes.Path.DataProperty,Geometry.Parse("M 4,23 C 14,23 23,14 23,4"));path.SetValue(System.Windows.Shapes.Path.StrokeProperty,Palette.Brush("#CFFFFFFF"));path.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty,5d);path.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty,PenLineCap.Round);path.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty,PenLineCap.Round);path.SetValue(OpacityProperty,.8);gripHit.AppendChild(path);grip.Template=new ControlTemplate(typeof(Thumb)){VisualTree=gripHit};
        AutomationProperties.SetAutomationId(grip,"resize-"+c.Kind);AutomationProperties.SetName(grip,"调整"+ControlCenterCatalog.Name(c.Kind)+"形状");
        Point resizePointer=new();double resizeScale=1;
        grip.DragStarted+=(_,_)=>{EndDrag();_resizing=true;_resizeStart=ControlCenterLayout.Size(layout.EffectiveShape,_options.Geometry,layout.Module);var pointer=System.Windows.Forms.Cursor.Position;resizePointer=new Point(pointer.X,pointer.Y);resizeScale=VisualTreeHelper.GetDpi(this).DpiScaleX*_fit.ScaleX;Panel.SetZIndex(c.Root,5);};
        grip.DragDelta+=(_,e)=>{var pointer=System.Windows.Forms.Cursor.Position;_resizeDelta=(new Point(pointer.X,pointer.Y)-resizePointer)/resizeScale;var requested=new Size(Math.Max(1,_resizeStart.Width+_resizeDelta.X),Math.Max(1,_resizeStart.Height+_resizeDelta.Y));var shape=ControlCenterLayout.Snap(requested,_options.Geometry,layout.EffectiveShape,layout.Module);if(shape!=layout.EffectiveShape){layout.SetShape(shape); // Keep the captured grip alive during resize.
                c.Shape=shape;c.Surface.CornerExponent=shape==ControlCardShape.Circle||c.Kind==ControlModule.Media?0:shape==ControlCardShape.Capsule||c.Kind is ControlModule.Volume or ControlModule.Brightness?1.5:0;c.Surface.Radius=c.Kind==ControlModule.Media?_options.Geometry.MediaRoundness:shape switch{ControlCardShape.Circle=>100,ControlCardShape.Capsule=>_options.Geometry.CapsuleRoundness,ControlCardShape.Square=>_options.Geometry.SquareRoundness,_=>_options.Geometry.RectangleRoundness};LayoutCards(true);}};
        grip.DragCompleted+=(_,_)=>{_resizing=false;Panel.SetZIndex(c.Root,0);BindCard(c,layout);LayoutCards(true);PersistLayout();RefreshChrome();PositionAtTray();};
        c.Grip=grip;c.Root.Children.Add(grip);InstallEditBadge(c);UpdateCardInsets(c);_chromeDirty=true;
        if(c.Light.HasValue)TextForeground(c.Body,c.Light==true?Brushes.White:_palette.Text);
    }
    // 参考图的圆形图标容器：Wi-Fi/蓝牙/省电为白底彩标，专注/深色为深灰底白标。
    private FrameworkElement IconHolder(Card card,Button icon,double size)
    {
        var kind=card.Kind;bool dark=kind is ControlModule.Focus or ControlModule.DarkMode;
        if(icon.Content is ControlCenterIcon glyph)
        {
            glyph.Brush=dark?Brushes.White:kind switch{ControlModule.Wifi=>Palette.Brush("#FF2D55"),ControlModule.Bluetooth=>Palette.Brush("#0A84FF"),ControlModule.PowerSaver=>Palette.Brush("#34C759"),_=>DarkInk};
            glyph.Tag="keep";// 彩色图标不受自适应文字明暗翻转影响，避免白圆里刷成白色看不见。
        }
        icon.RenderTransform=Transform.Identity;icon.MinWidth=icon.MinHeight=0;icon.Width=icon.Height=Math.Max(26,_options.Geometry.IconSize+6);icon.Padding=new Thickness();
        var holder=new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size/2),Background=dark?Palette.Brush("#5A5A5E"):Brushes.White,Child=icon,VerticalAlignment=VerticalAlignment.Center,Cursor=Cursors.Hand};
        holder.MouseLeftButtonUp+=(_,e)=>{if(!InInteractive(e.OriginalSource as DependencyObject)&&!_editing&&!_dragged&&icon.IsEnabled){icon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}};
        var press=new ScaleTransform(1,1);holder.RenderTransform=press;holder.RenderTransformOrigin=new Point(.5,.5);
        holder.PreviewMouseLeftButtonDown+=(_,_)=>{if(Animate){ControlMotion.To(press,ScaleTransform.ScaleXProperty,.975,speed:3.5);ControlMotion.To(press,ScaleTransform.ScaleYProperty,.975,speed:3.5);}};
        void Release(){ControlMotion.To(press,ScaleTransform.ScaleXProperty,1,speed:3.5);ControlMotion.To(press,ScaleTransform.ScaleYProperty,1,speed:3.5);}
        holder.PreviewMouseLeftButtonUp+=(_,_)=>{if(Animate)Release();};
        card.Holder=holder;holder.MouseEnter+=(_,_)=>{holder.Opacity=.88;if(Animate&&icon.Content is ControlCenterIcon mark)mark.Feedback(true);};holder.MouseLeave+=(_,_)=>{holder.Opacity=1;if(Animate)Release();};return holder;
    }
    private void ToggleModule(ControlModule kind)=>Run(()=>Execute(kind,"正在切换…",async()=>{
        switch(kind){
            case ControlModule.Wifi:await ToggleRadio(RadioKind.WiFi);break;
            case ControlModule.Bluetooth:await ToggleRadio(RadioKind.Bluetooth);break;
            case ControlModule.DarkMode:await Task.Run(()=>SystemControlService.SetDarkMode(SystemControlService.ReadDarkMode()!=true));break;
            case ControlModule.PowerSaver:await Task.Run(()=>SystemControlService.SetPowerSaver(SystemControlService.ReadPowerSaver()!=true));break;
        }
    }));    private static void Detach(FrameworkElement element){if(element.Parent is Panel panel)panel.Children.Remove(element);else if(element.Parent is ContentControl control)control.Content=null;}
    private void LayoutCards(bool animate)
    {
        var placements=ControlCenterLayout.Arrange(_options);DrawGroupLabels(placements);
        foreach(var p in placements){var c=_modules.Single(c=>c.Kind==p.Module);var previous=c.Target;c.Target=p.Bounds;if(!animate||!Animate||double.IsNaN(c.Root.Width)){c.MoveStarted=-1;c.Root.Width=p.Bounds.Width;c.Root.Height=p.Bounds.Height;c.Position.X=p.Bounds.X;c.Position.Y=p.Bounds.Y;}
            else if(c!=_drag&&previous.TopLeft!=p.Bounds.TopLeft){c.MoveFrom=new Point(c.Position.X,c.Position.Y);var movement=p.Bounds.TopLeft-c.MoveFrom;double momentum=movement.LengthSquared>1?(c.VX*movement.X+c.VY*movement.Y)/movement.LengthSquared:0;c.MovePath=new ControlMotion.SpringPath(0,1,momentum,ControlMotion.LayoutSpeed);c.MoveStarted=_runtime.Elapsed.TotalSeconds;var delta=p.Bounds.TopLeft-c.MoveFrom;double distance=delta.Length;c.Avoidance=distance>1?new Vector(-delta.Y/distance,delta.X/distance)*Math.Min(26,ControlCenterLayout.Unit(_options.Geometry)*.4):new Vector();}}
        double bottom=placements.Count==0?0:placements.Max(p=>p.Bounds.Bottom)+_options.Geometry.Gap;
        Canvas.SetTop(_edit,bottom);Canvas.SetLeft(_edit,(_stage.Width-120)/2);Canvas.SetTop(_add,bottom);Canvas.SetLeft(_add,0);
        _mainHeight=bottom+38;_cards.Height=_mainHeight;if(!_detailVisible)SetStageHeight(_mainHeight,animate);_chromeDirty=true;
    }
    private void DragCard(Point point)
    {
        if(_drag is null||Mouse.LeftButton!=MouseButtonState.Pressed)return;
        if(!_dragged){if((point-_dragStart).Length<3)return;_dragged=true;_drag.Root.Opacity=.88;Panel.SetZIndex(_drag.Root,10);Scale(_drag.Scale,1.045,190);}
        TrackDragPointer(_drag,point);
        // Repacking a square may put a different card beneath the same pointer.
        // A second reorder requires leaving the previous drop zone, preventing flip-flop.
        if(!_dragGate.IsEmpty&&_dragGate.Contains(point))return;_dragGate=Rect.Empty;
        var target=_modules.Where(c=>c!=_drag).FirstOrDefault(c=>{var rect=c.Target;rect.Inflate(-Math.Min(5,rect.Width*.08),-Math.Min(5,rect.Height*.08));return rect.Contains(point);});
        if(target is null){_lastDragTarget=null;return;}if(_lastDragTarget==target.Kind)return;_lastDragTarget=target.Kind;
        _dragGate=new Rect(point.X-9,point.Y-9,18,18);ControlCenterLayout.MoveBefore(_options,_drag.Kind,target.Kind);LayoutCards(true);
    }
    private void TrackDragPointer(Card card,Point point)
    {if(!_dragged)return;card.Position.X=point.X-_grab.X-(_grab.X-card.Root.ActualWidth/2)*(card.Scale.ScaleX-1);card.Position.Y=point.Y-_grab.Y-(_grab.Y-card.Root.ActualHeight/2)*(card.Scale.ScaleY-1);}
    private void EndDrag()
    {
        var card=_drag;_drag=null;if(card is not null){card.Root.ReleaseMouseCapture();card.Root.Opacity=1;Panel.SetZIndex(card.Root,0);Scale(card.Scale,1,240);LayoutCards(true);if(_dragged)PersistLayout();}
        _lastDragTarget=null;_dragGate=Rect.Empty;if(_ready)PositionAtTray();Dispatcher.BeginInvoke(()=>_dragged=false);
    }
    private void SetStageHeight(double height,bool animate)
    {
        // Keep the layered HWND at a stable size during page animations. The
        // incoming/outgoing pages animate inside it; the secondary plate fits its content.
        _stage.Height=Math.Max(_mainHeight,height);
        if(_detailVisible){_details.VerticalAlignment=VerticalAlignment.Top;AnimateDetailHeight(height,animate);}
    }
}
