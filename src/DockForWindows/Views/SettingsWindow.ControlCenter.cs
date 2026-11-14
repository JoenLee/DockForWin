using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal sealed partial class SettingsWindow
{
    private void UpdateControlPreview()
    {
        if(_controlLive?.IsChecked!=true){_controlPreview?.Dismiss();_controlPreview=null;return;}
        if(_controlPreview is not null){_controlPreview.ApplyPreview(_draft);return;}
        var window=new ControlCenterWindow(_draft,saveLayout:options=>{_draft.ControlCenter.Modules=options.Modules;_draft.ControlCenter.Pages=options.Pages;_draft.ControlCenter.ActivePageId=options.ActivePageId;_draft.ControlCenter.Geometry.MediaDetailWidth=options.Geometry.MediaDetailWidth;_draft.ControlCenter.Geometry.MediaDetailHeight=options.Geometry.MediaDetailHeight;}){DismissOnDeactivate=false,ShowActivated=false};
        _controlPreview=window;window.Closed+=(_,_)=>{if(_controlPreview==window){_controlPreview=null;if(_controlLive is not null)_controlLive.IsChecked=false;}};window.Show();
    }
    // Tile colour per module family: connectivity blue, sound pink, light orange, appearance indigo, power green.
    private static int ModuleTile(ControlModule module)=>module switch{
        ControlModule.Wifi or ControlModule.Bluetooth or ControlModule.Airplane or ControlModule.MobileHotspot or ControlModule.Projection=>0,
        ControlModule.Media or ControlModule.Volume or ControlModule.AudioOutput or ControlModule.Microphone or ControlModule.VoiceInput=>4,
        ControlModule.Brightness or ControlModule.NightLight or ControlModule.Hdr or ControlModule.RefreshRate=>2,
        ControlModule.DarkMode or ControlModule.Focus=>3,
        ControlModule.Battery or ControlModule.PowerSaver or ControlModule.Performance or ControlModule.KeepAwake=>5,
        _=>1};
    private void BuildControlCenterSettings(Panel page)
    {
        var options=_draft.ControlCenter;
        // Grouped by what each choice changes (audit P14-03): 外观 here, 打开方式 and 任务栏 at the end of this method,
        // and the three detail pages in between become one "布局与细节" list (SettingsWindow.ShellLayout).
        var center=Section(page,LocalizedText.Get("外观"),LocalizedText.Get("液态玻璃带折射与高光；经典磨砂更省电。"));
        var mode=Segmented(new[]{LocalizedText.Get("液态玻璃"),LocalizedText.Get("经典磨砂")},(int)options.MaterialMode);
        AutomationProperties.SetAutomationId(mode,"cc-material-mode");AutomationProperties.SetName(mode,LocalizedText.Get("控制中心材质"));
        mode.SelectionChanged+=(_,_)=>{if(mode.SelectedIndex>=0){options.MaterialMode=(ControlCenterMaterialMode)mode.SelectedIndex;QueuePreview();}};AddSettingsRow(center,LabeledRow(LocalizedText.Get("材质"),mode));
        var colors=new ComboBox{ItemsSource=new[]{LocalizedText.Get("自适应背景"),LocalizedText.Get("浅色"),LocalizedText.Get("深色")},SelectedIndex=(int)options.ColorMode,MinWidth=150};AutomationProperties.SetAutomationId(colors,"cc-color-mode");AutomationProperties.SetName(colors,LocalizedText.Get("文字配色"));colors.SelectionChanged+=(_,_)=>{if(colors.SelectedIndex>=0)options.ColorMode=(ControlCenterColorMode)colors.SelectedIndex;QueuePreview();};
        // Three choices fit on the row, so they are shown as segments; the drop-down stays (hidden) as the source.
        colors.Visibility=Visibility.Collapsed;var colorSegments=Segmented(new[]{LocalizedText.Get("自动"),LocalizedText.Get("浅色"),LocalizedText.Get("深色")},(int)options.ColorMode);AutomationProperties.SetAutomationId(colorSegments,"cc-color-mode-segment");AutomationProperties.SetName(colorSegments,LocalizedText.Get("文字配色"));
        colorSegments.SelectionChanged+=(_,_)=>{if(colorSegments.SelectedIndex>=0&&colors.SelectedIndex!=colorSegments.SelectedIndex)colors.SelectedIndex=colorSegments.SelectedIndex;};colors.SelectionChanged+=(_,_)=>{if(colors.SelectedIndex>=0)colorSegments.SelectedIndex=colors.SelectedIndex;};
        var colorCell=new Grid();colorCell.Children.Add(colors);colorCell.Children.Add(colorSegments);AddSettingsRow(center,LabeledRow(LocalizedText.Get("文字配色"),colorCell));

        var layout=Section(page,LocalizedText.Get("模块与排列"),LocalizedText.Get("打开开关添加模块，关闭则移除。普通控件有圆形、胶囊、正方形和矩形；播放器有 4×1、2×2、4×2 和 4×7 四档。也可以在控制中心点“编辑控制”直接拖动排列、拖右下角切换尺寸。\nWi-Fi 和蓝牙开关直接控制设备，个人 Wi-Fi 可以直接输入密码连接；企业网络认证、受限的蓝牙连接和专注模式使用 Windows 原生面板，不能可靠控制时会说明原因。亮度只在显示器提供控制接口时可用，媒体需要播放器支持系统媒体会话。"));
        foreach(var pageOption in options.Pages){var entryPage=pageOption;var name=new TextBox{Text=entryPage.Name,MaxLength=40,Width=220};AutomationProperties.SetName(name,LocalizedText.Get("页面名称"));name.LostKeyboardFocus+=(_,_)=>{entryPage.Name=name.Text;options.Normalize();QueuePreview();};AddSettingsRow(layout,LabeledRow(options.Pages.Count>1?LocalizedText.Get("页面 · ")+entryPage.Name:LocalizedText.Get("页面名称"),name));}
        var pageButtons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,10)};
        var addPage=SettingsButton(LocalizedText.Get("添加页面"),(_,_)=>{if(options.Pages.Count>=12)return;options.Pages.Add(new(){Id="page-"+Guid.NewGuid().ToString("N"),Name=LocalizedText.Get("新页面 ")+(options.Pages.Count+1)});_dock.ReopenSettings(_draft,7);});addPage.Margin=new Thickness(0,0,8,0);pageButtons.Children.Add(addPage);
        var resetLayout=SettingsButton(LocalizedText.Get("恢复默认布局"),(_,_)=>{options.Modules=ControlCenterOptions.Defaults();options.Pages=new(){new()};options.ActivePageId="main";options.Normalize();_dock.ReopenSettings(_draft,7);});resetLayout.Margin=new Thickness(0);pageButtons.Children.Add(resetLayout);AddSettingsRow(layout,pageButtons);
        var rows=new StackPanel();AddSettingsRow(layout,rows);
        void Rebuild()
        {
            rows.Children.Clear();
            var shown=options.Modules.Where(m=>m.Visible).ToArray();var hidden=options.Modules.Where(m=>!m.Visible).ToArray();
            void Heading(string text){var label=new TextBlock{Text=text,FontSize=12,FontWeight=FontWeights.Medium,Margin=new Thickness(0,rows.Children.Count==0?10:18,0,4)};label.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");rows.Children.Add(label);}
            foreach(var item in shown.Concat(hidden))
            {
                if(item==shown.FirstOrDefault())Heading(LocalizedText.Format($"已显示 · {shown.Length}"));
                if(item==hidden.FirstOrDefault())Heading(LocalizedText.Format($"未显示 · {hidden.Length}"));
                else if(rows.Children.Count>0&&rows.Children[^1] is not TextBlock)rows.Children.Add(RowSeparator());
                var row=new Grid{MinHeight=50};
                foreach(var width in new[]{GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto,GridLength.Auto,GridLength.Auto})row.ColumnDefinitions.Add(new(){Width=width});
                string glyph=ControlCenterIcon.AvailableGlyphs.Contains(item.Glyph)?item.Glyph:item.Module.ToString();
                var tile=DesignSystem.IconTile(ModuleTile(item.Module),glyph,26);tile.Margin=new Thickness(0,0,12,0);tile.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(tile);
                var name=new TextBlock{Text=string.IsNullOrWhiteSpace(item.DisplayName)?ControlCenterCatalog.Name(item.Module):item.DisplayName,FontSize=DesignSystem.Body,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetColumn(name,1);row.Children.Add(name);
                var shapes=ControlCenterLayout.Shapes(item.Module);var size=new ComboBox{ItemsSource=shapes.Select(ControlCenterCatalog.ShapeName).ToArray(),SelectedIndex=Array.IndexOf(shapes,item.EffectiveShape),MinWidth=96,Margin=new Thickness(12,0,8,0),VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetName(size,LocalizedText.Get("尺寸"));size.SelectionChanged+=(_,_)=>{if(size.SelectedIndex>=0)item.SetShape(shapes[size.SelectedIndex]);QueuePreview();};Grid.SetColumn(size,2);row.Children.Add(size);
                void Move(int delta){var order=options.Modules;int from=order.IndexOf(item),to=from;do to+=delta;while(to>=0&&to<order.Count&&!order[to].Visible);if(to<0||to>=order.Count)return;order.RemoveAt(from);order.Insert(to,item);Rebuild();QueuePreview();}
                var stepper=Stepper(()=>Move(-1),()=>Move(1));stepper.Margin=new Thickness(0,0,6,0);Grid.SetColumn(stepper,3);row.Children.Add(stepper);
                var fields=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(38,0,0,12)};
                var more=new Button{Style=(Style)FindResource("SettingsToolButton"),Content=new TextBlock{Text="⋯",FontSize=16,Margin=new Thickness(0,-3,0,0)},ToolTip=LocalizedText.Get("名称、图标、分组与页面"),Margin=new Thickness(0,0,6,0)};AutomationProperties.SetName(more,LocalizedText.Get("自定义 ")+name.Text);more.Click+=(_,_)=>fields.Visibility=fields.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;Grid.SetColumn(more,4);row.Children.Add(more);
                var visible=new CheckBox{IsChecked=item.Visible,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetAutomationId(visible,"module-"+item.Module);AutomationProperties.SetName(visible,name.Text);void Changed(){if(!_ready||_updating)return;item.Visible=visible.IsChecked==true;QueuePreview();Dispatcher.BeginInvoke(Rebuild,System.Windows.Threading.DispatcherPriority.Background);}
                if(!item.Visible){size.Visibility=Visibility.Collapsed;stepper.Visibility=Visibility.Collapsed;tile.Opacity=.55;name.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");}visible.Checked+=(_,_)=>Changed();visible.Unchecked+=(_,_)=>Changed();Grid.SetColumn(visible,5);row.Children.Add(visible);
                rows.Children.Add(row);
                // Disclosure: per-module customisation in a quiet inset well.
                var well=new Border{CornerRadius=new CornerRadius(10),Padding=new Thickness(14,4,14,4),Child=new StackPanel()};well.SetResourceReference(Border.BackgroundProperty,"SettingsQuiet");var wellRows=(StackPanel)well.Child;fields.Children.Add(well);
                void TextEntry(string title,string current,Action<string> set){var box=new TextBox{Text=current,MaxLength=40,Width=200};AutomationProperties.SetName(box,title);box.LostKeyboardFocus+=(_,_)=>{set(box.Text);QueuePreview();};AddRow(wellRows,LabeledRow(title,box));}
                TextEntry(LocalizedText.Get("显示名称"),item.DisplayName,v=>item.DisplayName=v);TextEntry(LocalizedText.Get("分组"),item.Group,v=>item.Group=v);
                var icons=new ComboBox{ItemsSource=new[]{LocalizedText.Get("默认图标")}.Concat(ControlCenterIcon.AvailableGlyphs).ToArray(),SelectedIndex=0,MinWidth=150};if(ControlCenterIcon.AvailableGlyphs.Contains(item.Glyph))icons.SelectedItem=item.Glyph;icons.SelectionChanged+=(_,_)=>{item.Glyph=icons.SelectedIndex==0?"":icons.SelectedItem as string??"";QueuePreview();};AddRow(wellRows,LabeledRow(LocalizedText.Get("图标"),icons));
                var pages=new ComboBox{ItemsSource=options.Pages.Select(p=>p.Name).ToArray(),SelectedIndex=options.Pages.FindIndex(p=>p.Id==item.PageId),MinWidth=150};pages.SelectionChanged+=(_,_)=>{if(pages.SelectedIndex>=0)item.PageId=options.Pages[pages.SelectedIndex].Id;QueuePreview();};AddRow(wellRows,LabeledRow(LocalizedText.Get("所在页面"),pages));
                rows.Children.Add(fields);
            }
        }
        Rebuild();

        var geometry=options.Geometry;
        var proportions=Section(page,LocalizedText.Get("大小与圆角"),LocalizedText.Get("面板缩放、组件缩放与各类圆角。内容超过屏幕时自动缩小显示。"));
        Range(proportions,LocalizedText.Get("整体面板缩放"),"cc-panel-scale",65,150,geometry.PanelScale*100,"0'%'",v=>geometry.PanelScale=v/100,1,false);
        Range(proportions,LocalizedText.Get("组件缩放"),"cc-component-scale",75,150,geometry.ComponentScale*100,"0'%'",v=>geometry.ComponentScale=v/100,1,false);
        Range(proportions,LocalizedText.Get("组件间距"),"cc-gap",6,24,geometry.Gap,"0' px'",v=>geometry.Gap=v,1,false);
        Range(proportions,LocalizedText.Get("胶囊圆滑程度"),"cc-capsule-radius",0,100,geometry.CapsuleRoundness,"0'%'",v=>geometry.CapsuleRoundness=v,.1,false);
        Range(proportions,LocalizedText.Get("正方形圆角"),"cc-square-radius",0,100,geometry.SquareRoundness,"0'%'",v=>geometry.SquareRoundness=v,1,false);
        Range(proportions,LocalizedText.Get("矩形圆角"),"cc-rectangle-radius",0,100,geometry.RectangleRoundness,"0'%'",v=>geometry.RectangleRoundness=v,1,false);
        Range(proportions,LocalizedText.Get("详情页圆角"),"cc-detail-radius",0,35,geometry.DetailRoundness,"0'%'",v=>geometry.DetailRoundness=v,1,false);
        Range(proportions,LocalizedText.Get("播放器圆角"),"cc-media-radius",0,100,geometry.MediaRoundness,"0'%'",v=>geometry.MediaRoundness=v,.1,false);
        var material=Section(page,LocalizedText.Get("液态玻璃参数"),LocalizedText.Get("默认跟随程序坞，也可以为控制中心单独设置；只在液态玻璃材质下使用。"));
        Toggle(material,LocalizedText.Get("单独设置控制中心材质"),"control-material-override",options.OverrideMaterial,v=>options.OverrideMaterial=v);
        void RangeOverride(string label,string id,double min,double max,double value,Action<double> setter,string format,double step=1)
        {Range(material,label,id,min,max,value,format,setter,step,false);Require(_ranges[id],()=>options.OverrideMaterial&&options.MaterialMode==ControlCenterMaterialMode.LiquidGlass);}
        RangeOverride(LocalizedText.Get("折射强度"),"cc-intensity",0,100,options.Intensity*100,v=>options.Intensity=v/100,"0'%'");
        RangeOverride(LocalizedText.Get("边缘扭曲度"),"cc-distortion",0,100,options.Distortion*100,v=>options.Distortion=v/100,"0'%'");
        RangeOverride(LocalizedText.Get("高斯磨砂"),"cc-frost",0,100,options.Frost*100,v=>options.Frost=v/100,"0'%'",.1);
        RangeOverride(LocalizedText.Get("玻璃透明度"),"cc-opacity",0,100,(1-options.Opacity)*100,v=>options.Opacity=1-v/100,"0'%'");
        RangeOverride(LocalizedText.Get("暗色衬底上限"),"cc-tint",0,25,options.Tint*100,v=>options.Tint=v/100,"0.0'%'",.1);
        RangeOverride(LocalizedText.Get("上下高光"),"cc-highlight",0,100,options.Highlight*100,v=>options.Highlight=v/100,"0'%'");
        RangeOverride(LocalizedText.Get("高光过渡范围"),"cc-highlight-width",.5,8,options.HighlightWidth,v=>options.HighlightWidth=v,"0.0' px'",.1);

        var entry=Section(page,LocalizedText.Get("打开方式"),"");
        Toggle(entry,LocalizedText.Get("从任务栏打开"),"glass-control-center",_draft.UseGlassControlCenter,v=>_draft.UseGlassControlCenter=v);
        entry.Children.Add(RowNote(LocalizedText.Get("点击任务栏的网络或音量图标时打开控制中心。")));
        AddSettingsRow(entry,LabeledRow(LocalizedText.Get("快捷键"),Keycaps(options.Hotkey)));
        var taskbar=Section(page,LocalizedText.Get("任务栏"),"");
        Toggle(taskbar,LocalizedText.Get("隐藏任务栏应用图标"),"hide-taskbar-apps",_draft.HideTaskbarApplications,v=>_draft.HideTaskbarApplications=v);
        taskbar.Children.Add(RowNote(LocalizedText.Get("保留开始按钮与托盘；任务栏由辅助工具接管，工具不可用时不会生效。")));
    }
    // A quiet line under a switch that says what it does, so the switch's own label can stay short.
    private static TextBlock RowNote(string text){var note=Muted(text,new Thickness(0,-6,0,10));return note;}
    // A shortcut drawn as key caps: Ctrl  Win  A.
    private static FrameworkElement Keycaps(string shortcut)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetName(row,LocalizedText.Get("快捷键 ")+shortcut);
        foreach(var key in shortcut.Split('+',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var cap=new Border{CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1,1,1,2),Padding=new Thickness(7,1,7,1),Margin=new Thickness(5,0,0,0),MinWidth=24};
            cap.SetResourceReference(Border.BackgroundProperty,"SettingsCard");cap.SetResourceReference(Border.BorderBrushProperty,"SettingsControlStroke");
            var label=new TextBlock{Text=key,FontSize=11.5,FontWeight=FontWeights.Medium,HorizontalAlignment=HorizontalAlignment.Center};label.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");cap.Child=label;row.Children.Add(cap);
        }
        return row;
    }
    // Compact reorder control: one bordered capsule holding up and down chevrons.
    private Border Stepper(Action up,Action down)
    {
        Button Half(bool upward,Action action,string name)
        {
            var path=new System.Windows.Shapes.Path{Data=System.Windows.Media.Geometry.Parse(upward?"M1,5.5 L5,1.5 L9,5.5":"M1,1.5 L5,5.5 L9,1.5"),StrokeThickness=1.6,StrokeStartLineCap=System.Windows.Media.PenLineCap.Round,StrokeEndLineCap=System.Windows.Media.PenLineCap.Round,StrokeLineJoin=System.Windows.Media.PenLineJoin.Round,Width=10,Height=7};
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1)});
            var button=new Button{Style=(Style)FindResource("SettingsToolButton"),Content=path,Width=26,Height=26};AutomationProperties.SetName(button,name);button.Click+=(_,_)=>action();return button;
        }
        var pair=new StackPanel{Orientation=Orientation.Horizontal};pair.Children.Add(Half(true,up,LocalizedText.Get("上移")));var divider=new Border{Width=1,Margin=new Thickness(0,6,0,6)};divider.SetResourceReference(Border.BackgroundProperty,"SettingsLine");pair.Children.Add(divider);pair.Children.Add(Half(false,down,LocalizedText.Get("下移")));
        var frame=new Border{CornerRadius=new CornerRadius(7),BorderThickness=new Thickness(1),Child=pair,VerticalAlignment=VerticalAlignment.Center,Padding=new Thickness(1)};frame.SetResourceReference(Border.BackgroundProperty,"SettingsControlFill");frame.SetResourceReference(Border.BorderBrushProperty,"SettingsControlStroke");return frame;
    }
}





