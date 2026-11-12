using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private ComboBox? _pagePicker;private bool _syncingPages;private readonly List<TextBlock> _groupTitles=new();
    private string ModuleName(ControlModule kind){var item=_options.Modules.FirstOrDefault(m=>m.Module==kind);return string.IsNullOrWhiteSpace(item?.DisplayName)?ControlCenterCatalog.Name(kind):item.DisplayName;}
    private string ModuleGlyph(ControlModule kind){var value=_options.Modules.FirstOrDefault(m=>m.Module==kind)?.Glyph;return value is not null&&ControlCenterIcon.AvailableGlyphs.Contains(value)?value:kind.ToString();}
    private void AddPagePicker(Grid heading)
    {
        _pagePicker=new ComboBox{Width=108,MinHeight=28,Height=28,FontSize=11,Margin=new Thickness(8,0,8,0)};AutomationProperties.SetAutomationId(_pagePicker,"control-pages");Grid.SetColumn(_pagePicker,1);heading.Children.Add(_pagePicker);
        _pagePicker.SelectionChanged+=(_,_)=>{if(_syncingPages||!_ready||_pagePicker.SelectedIndex<0)return;_options.ActivePageId=_options.Pages[_pagePicker.SelectedIndex].Id;if(_detailVisible)HideDetails();SyncCards(true);PersistLayout();};RefreshPagePicker();
    }
    private void RefreshPagePicker()
    {if(_pagePicker is null)return;_syncingPages=true;_pagePicker.ItemsSource=_options.Pages.Select(p=>p.Name).ToArray();_pagePicker.SelectedIndex=_options.Pages.FindIndex(p=>p.Id==_options.ActivePageId);_pagePicker.Visibility=_options.Pages.Count>1?Visibility.Visible:Visibility.Collapsed;_syncingPages=false;}
    private void DrawGroupLabels(IReadOnlyList<ControlCardPlacement> placements)
    {
        foreach(var title in _groupTitles)_cards.Children.Remove(title);_groupTitles.Clear();double z=_options.Geometry.ComponentScale;
        foreach(var group in placements.Where(p=>p.Group.Length>0).GroupBy(p=>p.Group)){var label=new TextBlock{Text=group.Key,FontSize=11,FontWeight=FontWeights.SemiBold,Opacity=.7,Foreground=Foreground,IsHitTestVisible=false};Canvas.SetLeft(label,8*z);Canvas.SetTop(label,group.Min(p=>p.Bounds.Top)-24*z);_cards.Children.Add(label);_groupTitles.Add(label);}
    }
    private void AddCardManagement(Card card,ControlModuleLayout original)
    {
        ControlModuleLayout Current()=>_options.Modules.Single(m=>m.Module==card.Kind);
        var edit=new MenuItem{Header="名称、图标与分组…"};edit.Click+=(_,_)=>EditControlMetadata(Current());card.Root.ContextMenu.Items.Add(edit);
        var pages=new MenuItem{Header="移到页面"};pages.SubmenuOpened+=(_,_)=>{pages.Items.Clear();foreach(var page in _options.Pages){var target=page;var item=new MenuItem{Header=target.Name,IsCheckable=true,IsChecked=Current().PageId==target.Id};item.Click+=(_,_)=>{Current().PageId=target.Id;SyncCards(true);PersistLayout();};pages.Items.Add(item);}};card.Root.ContextMenu.Items.Add(pages);
    }
    private void EditControlMetadata(ControlModuleLayout layout)
    {
        _page=ControlPage.ControlEditor;ShowDetails("编辑 "+ModuleName(layout.Module));
        TextBox Entry(string title,string value){_detailBody.Children.Add(new TextBlock{Text=title,FontSize=12,Margin=new Thickness(0,12,0,4)});var box=new TextBox{Text=value,MaxLength=40,Padding=new Thickness(10,8,10,8),FontSize=13};_detailBody.Children.Add(box);return box;}
        var name=Entry("显示名称（留空使用默认名称）",layout.DisplayName);var group=Entry("分组（相同名称归为一组，留空不分组）",layout.Group);
        _detailBody.Children.Add(new TextBlock{Text="图标",FontSize=12,Margin=new Thickness(0,12,0,4)});var icons=new ComboBox{ItemsSource=ControlCenterIcon.AvailableGlyphs,SelectedItem=ModuleGlyph(layout.Module),FontSize=12};_detailBody.Children.Add(icons);
        _detailBody.Children.Add(new TextBlock{Text="页面",FontSize=12,Margin=new Thickness(0,12,0,4)});var pages=new ComboBox{ItemsSource=_options.Pages.Select(p=>p.Name).ToArray(),SelectedIndex=_options.Pages.FindIndex(p=>p.Id==layout.PageId),FontSize=12};_detailBody.Children.Add(pages);
        AddDetail("应用更改",()=>{layout.DisplayName=name.Text;layout.Group=group.Text;layout.Glyph=icons.SelectedItem as string??"";layout.PageId=_options.Pages[Math.Max(0,pages.SelectedIndex)].Id;_options.Normalize();HideDetails();RebindVisibleControls();PersistLayout();},true);UpdateDetailLayout();
    }
    private void RebindVisibleControls()
    {foreach(var card in _modules)BindCard(card,_options.Modules.Single(m=>m.Module==card.Kind));SyncCards(true);RefreshPagePicker();}
    private void ManagePages()
    {
        _page=ControlPage.PageEditor;ShowDetails("管理页面");
        foreach(var item in _options.Pages.ToArray()){
            var page=item;var name=new TextBox{Text=page.Name,MaxLength=40,Padding=new Thickness(10,8,10,8),FontSize=13,Margin=new Thickness(0,8,0,2)};_detailBody.Children.Add(name);
            AddDetail("重命名此页",()=>{page.Name=name.Text;_options.Normalize();RefreshPagePicker();PersistLayout();},true);
            if(page.Id!="main")AddDetail("删除此页，控件移回主页面",()=>{foreach(var control in _options.Modules.Where(m=>m.PageId==page.Id))control.PageId="main";_options.Pages.Remove(page);if(_options.ActivePageId==page.Id)_options.ActivePageId="main";_options.Normalize();RebindVisibleControls();PersistLayout();ManagePages();},true);
        }
        if(_options.Pages.Count<12)AddDetail("添加页面",()=>{var page=new ControlCenterLayoutPage{Id="page-"+Guid.NewGuid().ToString("N"),Name="新页面 "+(_options.Pages.Count+1)};_options.Pages.Add(page);_options.ActivePageId=page.Id;RefreshPagePicker();HideDetails();SyncCards(true);PersistLayout();},true);UpdateDetailLayout();
    }
    private void ResetControlLayout()
    {_options.Modules=ControlCenterOptions.Defaults();_options.Pages=new(){new()};_options.ActivePageId="main";_options.Normalize();HideDetails();RebindVisibleControls();PersistLayout();}
}
