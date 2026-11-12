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
    private void BuildControlCenterSettings(Panel page)
    {
        var options=_draft.ControlCenter;
        var themes=Section(page,"控制中心主题","液态玻璃保留完整光学效果；经典模糊使用一层实时高斯背景和无描边的平面卡片。两种主题共用布局、功能与动画。");
        var mode=new SettingsSegmentedControl{ItemsSource=new[]{"Liquid Glass","经典高斯模糊"},SelectedIndex=(int)options.MaterialMode,FontSize=12,MinHeight=34};
        AutomationProperties.SetAutomationId(mode,"cc-material-mode");AutomationProperties.SetName(mode,"控制中心主题");
        mode.SelectionChanged+=(_,_)=>{if(mode.SelectedIndex>=0){options.MaterialMode=(ControlCenterMaterialMode)mode.SelectedIndex;QueuePreview();}};themes.Children.Add(mode);
        var entry=Section(page,"唤出控制中心","");
        Toggle(entry,"接管右侧快捷设置入口","glass-control-center",_draft.UseGlassControlCenter,v=>_draft.UseGlassControlCenter=v);
        Toggle(entry,"隐藏任务栏应用图标，保留开始与托盘","hide-taskbar-apps",_draft.HideTaskbarApplications,v=>_draft.HideTaskbarApplications=v);
        var geometry=options.Geometry;
        var proportions=Section(page,"大小与圆角","整体缩放同时调整面板、图标、文字及间距。组件缩放统一调整四种组件规格，不单独改变长宽比例；内容超过屏幕时自动缩小显示。开启实时预览时立即生效。");
        Range(proportions,"整体面板缩放 %","cc-panel-scale",65,150,geometry.PanelScale*100,"0",v=>geometry.PanelScale=v/100,1,false);
        Range(proportions,"组件等比例缩放 %","cc-component-scale",75,150,geometry.ComponentScale*100,"0",v=>geometry.ComponentScale=v/100,1,false);
        Range(proportions,"组件间距","cc-gap",6,24,geometry.Gap,"0",v=>geometry.Gap=v,1,false);
        Range(proportions,"胶囊圆滑程度 %","cc-capsule-radius",0,100,geometry.CapsuleRoundness,"0.0",v=>geometry.CapsuleRoundness=v,.1,false);
        Range(proportions,"正方形圆角 %","cc-square-radius",0,100,geometry.SquareRoundness,"0",v=>geometry.SquareRoundness=v,1,false);
        Range(proportions,"矩形圆角 %","cc-rectangle-radius",0,100,geometry.RectangleRoundness,"0",v=>geometry.RectangleRoundness=v,1,false);
        Range(proportions,"二级界面圆角 %","cc-detail-radius",0,35,geometry.DetailRoundness,"0",v=>geometry.DetailRoundness=v,1,false);
        Range(proportions,"音乐播放器圆角 %","cc-media-radius",0,100,geometry.MediaRoundness,"0.0",v=>geometry.MediaRoundness=v,.1,false);
        var layout=Section(page,"模块与排列","打开开关添加模块，关闭则移除。普通控件使用圆形、胶囊、正方形和矩形；播放器使用横向 4×1、正方形 2×2、横向 4×2、大封面 4×7 四档。在控制中心点“编辑控制”后拖动排列，拖动组件右下角切换规格，其余组件自动让位。支持页面、分组、名称与图标；编辑完成自动保存，设置窗口内可实时预览。");
        var pageTools=new StackPanel();layout.Children.Add(pageTools);
        foreach(var pageOption in options.Pages){var entryPage=pageOption;var name=new TextBox{Text=entryPage.Name,MaxLength=40,FontSize=12,Padding=new Thickness(8),Margin=new Thickness(0,4,0,4)};name.LostKeyboardFocus+=(_,_)=>{entryPage.Name=name.Text;options.Normalize();QueuePreview();};pageTools.Children.Add(name);}
        pageTools.Children.Add(SettingsButton("添加页面",(_,_)=>{if(options.Pages.Count>=12)return;options.Pages.Add(new(){Id="page-"+Guid.NewGuid().ToString("N"),Name="新页面 "+(options.Pages.Count+1)});_dock.ReopenSettings(_draft,7);}));
        pageTools.Children.Add(SettingsButton("恢复默认控件布局",(_,_)=>{options.Modules=ControlCenterOptions.Defaults();options.Pages=new(){new()};options.ActivePageId="main";options.Normalize();_dock.ReopenSettings(_draft,7);}));
        var rows=new StackPanel();layout.Children.Add(rows);
        void Rebuild()
        {
            rows.Children.Clear();
            foreach(var item in options.Modules.ToArray())
            {
                var row=new Grid{Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(82)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
                var left=new StackPanel();row.Children.Add(left);Toggle(left,ControlCenterCatalog.Name(item.Module),"module-"+item.Module,item.Visible,v=>item.Visible=v);
                var fields=new StackPanel{Margin=new Thickness(4,0,8,8)};var customize=new Expander{Header="名称、图标、分组与页面",Content=fields,FontSize=11};left.Children.Add(customize);
                void TextEntry(string title,string current,Action<string> set){fields.Children.Add(new TextBlock{Text=title,FontSize=11,Margin=new Thickness(0,4,0,2)});var box=new TextBox{Text=current,MaxLength=40,FontSize=12,Padding=new Thickness(6)};box.LostKeyboardFocus+=(_,_)=>{set(box.Text);QueuePreview();};fields.Children.Add(box);}
                TextEntry("显示名称",item.DisplayName,v=>item.DisplayName=v);TextEntry("分组",item.Group,v=>item.Group=v);
                var icons=new ComboBox{ItemsSource=new[]{"默认图标"}.Concat(ControlCenterIcon.AvailableGlyphs).ToArray(),SelectedIndex=0,FontSize=11,Margin=new Thickness(0,4,0,4)};if(ControlCenterIcon.AvailableGlyphs.Contains(item.Glyph))icons.SelectedItem=item.Glyph;icons.SelectionChanged+=(_,_)=>{item.Glyph=icons.SelectedIndex==0?"":icons.SelectedItem as string??"";QueuePreview();};fields.Children.Add(icons);
                var pages=new ComboBox{ItemsSource=options.Pages.Select(p=>p.Name).ToArray(),SelectedIndex=options.Pages.FindIndex(p=>p.Id==item.PageId),FontSize=11};pages.SelectionChanged+=(_,_)=>{if(pages.SelectedIndex>=0)item.PageId=options.Pages[pages.SelectedIndex].Id;QueuePreview();};fields.Children.Add(pages);
                var shapes=ControlCenterLayout.Shapes(item.Module);var size=new ComboBox{ItemsSource=shapes.Select(ControlCenterCatalog.ShapeName).ToArray(),SelectedIndex=Array.IndexOf(shapes,item.EffectiveShape),VerticalAlignment=VerticalAlignment.Center,FontSize=11};size.SelectionChanged+=(_,_)=>{if(size.SelectedIndex>=0)item.SetShape(shapes[size.SelectedIndex]);QueuePreview();};Grid.SetColumn(size,1);row.Children.Add(size);
                var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
                void Move(int delta){int from=options.Modules.IndexOf(item),to=Math.Clamp(from+delta,0,options.Modules.Count-1);options.Modules.RemoveAt(from);options.Modules.Insert(to,item);Rebuild();QueuePreview();}
                var up=SettingsButton("↑",(_,_)=>Move(-1));var down=SettingsButton("↓",(_,_)=>Move(1));foreach(var b in new[]{up,down}){b.Padding=new Thickness(4);b.MinHeight=30;b.Width=48;b.FontSize=12;b.Margin=new Thickness(3,0,0,0);actions.Children.Add(b);}Grid.SetColumn(actions,2);row.Children.Add(actions);rows.Children.Add(row);
            }
        }
        Rebuild();
        var appearance=Section(page,"控制中心配色","系统模式根据背景自动适配文字，浅色与深色分别使用对应文字对比。液态玻璃参数可继承 Dock；经典模糊使用独立的平面材质。");
        var colors=new ComboBox{BorderThickness=new Thickness(0),ItemsSource=new[]{"跟随系统 / 自适应背景","浅色","深色"},SelectedIndex=(int)options.ColorMode,FontSize=12};AutomationProperties.SetAutomationId(colors,"cc-color-mode");colors.SelectionChanged+=(_,_)=>{if(colors.SelectedIndex>=0)options.ColorMode=(ControlCenterColorMode)colors.SelectedIndex;QueuePreview();};appearance.Children.Add(colors);
        var material=Section(page,"控制中心玻璃","默认完整继承 Dock 的折射、扭曲、磨砂、透明度、衬色、高光；此处启用覆盖后，仅修改控制中心，不改变程序坞。控制中心不使用玻璃投影或内部暗边；所有卡片使用同一个实时背景采样流。");
        Toggle(material,"单独设置控制中心材质","control-material-override",options.OverrideMaterial,v=>options.OverrideMaterial=v);
        void RangeOverride(string label,string id,double min,double max,double value,Action<double> setter,double step=1)
        {Range(material,label,id,min,max,value,"0.0",setter,step,false);Require(_ranges[id],()=>options.OverrideMaterial&&options.MaterialMode==ControlCenterMaterialMode.LiquidGlass);}
        RangeOverride("折射强度 %","cc-intensity",0,100,options.Intensity*100,v=>options.Intensity=v/100);
        RangeOverride("边缘扭曲度 %","cc-distortion",0,100,options.Distortion*100,v=>options.Distortion=v/100);
        RangeOverride("高斯磨砂 %","cc-frost",0,100,options.Frost*100,v=>options.Frost=v/100,.1);
        RangeOverride("玻璃透明度 %","cc-opacity",0,100,(1-options.Opacity)*100,v=>options.Opacity=1-v/100);
        RangeOverride("淡黑衬色上限 %","cc-tint",0,25,options.Tint*100,v=>options.Tint=v/100,.1);
        RangeOverride("上下高光 %","cc-highlight",0,100,options.Highlight*100,v=>options.Highlight=v/100);
        RangeOverride("高光过渡范围 px","cc-highlight-width",.5,8,options.HighlightWidth,v=>options.HighlightWidth=v,.1);
        Help(page,"Wi-Fi 和蓝牙开关直接控制设备；个人 Wi-Fi 可直接输入密码连接，企业网络认证、受限的蓝牙连接与专注启停使用 Windows 原生面板，无法可靠控制时显示原因。录屏与听写调用原生工具。亮度只在显示器提供控制接口时启用。媒体需播放器支持系统媒体会话。",new Thickness(4,4,4,12));
    }
}





