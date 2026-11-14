using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow
{
    private WidgetGalleryPage? _widgetGallery;
    private void OpenWidgetCategory(WidgetKind kind){_pages.Navigate("widget-gallery");_widgetGallery?.ShowCategory(kind);}
    private FrameworkElement BuildWidgetHome()
    {
        var (page,body)=Page(LocalizedText.Get("小组件"),"");body.Margin=new Thickness(DesignSystem.PageInset,18,DesignSystem.PageInset,28);
        var cards=new Grid();cards.ColumnDefinitions.Add(new());cards.ColumnDefinitions.Add(new());
        for(int row=0;row<3;row++)cards.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var navigator=BuildWidgetNavigator(query=>{int visible=0;foreach(Button card in cards.Children){var kind=(WidgetKind)card.Tag;card.Visibility=WidgetSizes.Category(kind).Contains(query,StringComparison.OrdinalIgnoreCase)||WidgetRegistry.Definitions.Any(d=>d.Kind==kind&&d.Title.Contains(query,StringComparison.OrdinalIgnoreCase))?Visibility.Visible:Visibility.Collapsed;if(card.Visibility==Visibility.Visible){Grid.SetRow(card,visible/2);Grid.SetColumn(card,visible%2);visible++;}}});body.Children.Add(navigator);
        string[] descriptions={LocalizedText.Get("实况、逐小时天气与多日预报"),LocalizedText.Get("月历、日期与本地日程"),LocalizedText.Get("本地时间、世界时钟与表盘"),LocalizedText.Get("管理待办，记录下一件要做的事"),LocalizedText.Get("便签、备忘录与文件夹笔记"),LocalizedText.Get("正在播放与最近播放的音乐")};
        int index=0;
        foreach(var kind in Enum.GetValues<WidgetKind>()){
            var content=new StackPanel();var heading=new StackPanel{Orientation=Orientation.Horizontal};
            var icon=WidgetGalleryPage.CategoryIcon(kind.ToString().ToLowerInvariant());icon.Margin=new Thickness(0,0,10,0);heading.Children.Add(icon);
            heading.Children.Add(new TextBlock{Text=WidgetSizes.Category(kind),FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});content.Children.Add(heading);
            content.Children.Add(Muted(descriptions[index],new Thickness(0,14,0,0)));
            content.Children.Add(Muted(LocalizedText.Format($"{WidgetRegistry.Definitions.Count(d=>d.Kind==kind)} 种模板 · 查看与设置 ›"),new Thickness(0,20,0,0)));
            var card=new Button{Tag=kind,Content=content,Height=164,Template=CardTemplate(),HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(index%2==0?0:8,0,index%2==0?8:0,16)};
            AutomationProperties.SetAutomationId(card,"widget-home-"+kind.ToString().ToLowerInvariant());AutomationProperties.SetName(card,WidgetSizes.Category(kind));
            AttachWidgetCardMotion(card);card.Click+=(_,_)=>OpenWidgetCategory(kind);Grid.SetRow(card,index/2);Grid.SetColumn(card,index%2);cards.Children.Add(card);index++;
        }
        AutomationProperties.SetAutomationId(cards,"widget-home-cards");body.Children.Add(cards);return page;
    }
    private FrameworkElement BuildWidgetNavigator(Action<string> filter)
    {
        var navigator=new WrapPanel{Margin=new Thickness(0,0,0,14)};
        var search=new TextBox{Height=32,Width=180,MaxLength=100,Padding=new Thickness(30,2,9,2)};AutomationProperties.SetAutomationId(search,"widget-template-search");AutomationProperties.SetName(search,LocalizedText.Get("搜索模板"));
        var host=new Grid{Height=32,Margin=new Thickness(0,0,10,7)};host.Children.Add(search);
        var glass=new System.Windows.Shapes.Path{Data=System.Windows.Media.Geometry.Parse("M6,1.2 A4.8,4.8 0 1 1 5.99,1.2 Z M9.6,9.6 L13,13"),StrokeThickness=1.5,Width=14,Height=14,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(11,0,0,0),IsHitTestVisible=false};glass.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"SettingsTertiary");host.Children.Add(glass);
        var hint=new TextBlock{Text=LocalizedText.Get("搜索模板"),FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(31,0,0,0),IsHitTestVisible=false};hint.SetResourceReference(TextBlock.ForegroundProperty,"SettingsTertiary");host.Children.Add(hint);
        search.TextChanged+=(_,_)=>{hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;filter(search.Text.Trim());};navigator.Children.Add(host);
        foreach(var kind in Enum.GetValues<WidgetKind>()){
            var button=new Button{Content=WidgetSizes.Category(kind),Height=32,Padding=new Thickness(13,0,13,0),Margin=new Thickness(0,0,8,7),Template=WidgetGalleryPage.CategoryTemplate()};AutomationProperties.SetAutomationId(button,"widget-category-"+kind.ToString().ToLowerInvariant());button.Click+=(_,_)=>OpenWidgetCategory(kind);navigator.Children.Add(button);
        }
        return navigator;
    }
    private void AttachWidgetCardMotion(Button card)
    {
        var scale=new System.Windows.Media.ScaleTransform(1,1);card.RenderTransform=scale;card.RenderTransformOrigin=new Point(.5,.5);
        void Move(double target){foreach(var property in new[]{System.Windows.Media.ScaleTransform.ScaleXProperty,System.Windows.Media.ScaleTransform.ScaleYProperty}){if(_draft.Animations&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast)ControlMotion.To(scale,property,target,speed:2.4);else ControlMotion.Stop(scale,property,target);}}
        card.MouseEnter+=(_,_)=>Move(1.018);card.MouseLeave+=(_,_)=>Move(1);card.Unloaded+=(_,_)=>Move(1);
    }

}
