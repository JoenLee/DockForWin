using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using Forms = System.Windows.Forms;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage
{
    private readonly List<Action> _colorEditors = new();
    private static readonly (string Name,string Background,string Ink,string Accent,bool Gradient)[] CardPresets = {
        ("海湾蓝","#184F74","#F3FAFF","#FFE08B",true),
        ("暖砂","#F1E3CF","#4E392D","#B15E38",false),
        ("森林","#254D43","#F3F7EC","#D2E8A0",false),
        ("莓果","#683C51","#FFF3F7","#FFC1AF",true),
        ("暮色紫","#383857","#F3EFFF","#C6B6FF",true),
        ("极简黑","#14191D","#F5F7FA","#91CEC5",false) };

    private FrameworkElement BuildColorEditor(WidgetCarousel carousel,string category)
    {
        bool sync=false;
        var body=new StackPanel{Margin=new Thickness(0,8,0,0)};
        var root=new StackPanel();var typography=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};
        var editor=new Expander{Content=root,Margin=new Thickness(0,12,0,0),FontSize=11};
        var tabs=new SettingsSegmentedControl{ItemsSource=new[]{"配色","字体与大小"},SelectedIndex=0};
        AutomationProperties.SetAutomationId(tabs,"widget-appearance-tab-"+category);root.Children.Add(tabs);root.Children.Add(body);root.Children.Add(typography);
        tabs.SelectionChanged+=(_,_)=>{body.Visibility=tabs.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;typography.Visibility=tabs.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;};
        AutomationProperties.SetAutomationId(editor,"widget-colors-"+category);
        var preset=new ComboBox{ItemsSource=new[]{"跟随样式","自定义"}.Concat(CardPresets.Select(p=>p.Name)).ToArray(),Margin=new Thickness(0,0,0,7),MinHeight=30};
        AutomationProperties.SetAutomationId(preset,"widget-color-preset-"+category); AutomationProperties.SetName(preset,"当前卡片配色方案"); body.Children.Add(preset);
        var fields=new TextBox[3];var chips=new Border[3];string[] keys={"background","foreground","accent"};string[] titles={"背景","文字","强调色"};
        var hint=Note("留空跟随样式；外观随窗口底部保存设置提交。",new Thickness(0,5,0,0));
        WidgetHost Current()=>carousel.Hosts[carousel.SelectedIndex];
        string Read(WidgetCardColors? colors,int index)=>index switch{0=>colors?.Background??"",1=>colors?.Foreground??"",_=>colors?.Accent??""};
        void RefreshChips(){var host=Current();for(int i=0;i<3;i++)chips[i].Background=(Brush)host.Resources[i switch{0=>"WidgetBackground",1=>"WidgetInk",_=>"WidgetAccent"}];}
        void Apply(){var host=Current();host.SetMaterial(_options.Material,_options.CardColors.GetValueOrDefault(host.Definition.Id));RefreshChips();}
        var gradient=new CheckBox{Content="渐变背景",Margin=new Thickness(0,4,0,0)};
        ComboBox numberStyle=null!,textWeight=null!;Slider textScale=null!,panelScale=null!;
        AutomationProperties.SetAutomationId(gradient,"widget-color-gradient-"+category);
        void Refresh()
        {
            sync=true;var host=Current();var colors=_options.CardColors.GetValueOrDefault(host.Definition.Id);
            editor.Header="卡片外观 · "+host.Definition.Title;
            for(int i=0;i<3;i++){fields[i].Text=Read(colors,i);fields[i].SetResourceReference(Control.BorderBrushProperty,"SettingsLine");}
            gradient.IsChecked=(bool)host.Resources["WidgetGradient"];
            int match=Array.FindIndex(CardPresets,p=>colors!=null&&p.Background==colors.Background&&p.Ink==colors.Foreground&&p.Accent==colors.Accent&&p.Gradient==colors.Gradient);
            preset.SelectedIndex=colors==null||!colors.HasColors?0:match>=0?match+2:1;
            numberStyle.SelectedIndex=(int)(colors?.NumberStyle??WidgetNumberStyle.Standard);textWeight.SelectedIndex=(int)(colors?.TextWeight??WidgetTextWeight.Automatic);
            textScale.Value=(colors?.TextScale??1)*100;panelScale.Value=(colors?.PanelScale??1)*100;
            RefreshChips();hint.Text="外观已预览；点击窗口底部保存设置后保留。";sync=false;
        }
        for(int i=0;i<3;i++){
            int slot=i;var row=new Grid{Margin=new Thickness(0,4,0,4)};
            row.ColumnDefinitions.Add(new(){Width=new GridLength(58)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            row.Children.Add(new TextBlock{Text=titles[i],FontSize=11,VerticalAlignment=VerticalAlignment.Center});
            var field=fields[i]=new TextBox{MaxLength=7,FontSize=12,CharacterCasing=CharacterCasing.Upper,ToolTip="颜色 HEX，例如 #184F74；留空跟随样式"};
            AutomationProperties.SetAutomationId(field,"widget-color-"+category+"-"+keys[i]);AutomationProperties.SetName(field,"当前卡片"+titles[i]+"颜色");Grid.SetColumn(field,1);row.Children.Add(field);
            var chip=chips[i]=new Border{Width=16,Height=16,CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,7,0)};
            var choose=ActionButton("选色",()=>{
                var color=((SolidColorBrush)chips[slot].Background).Color;
                using var dialog=new Forms.ColorDialog{FullOpen=true,Color=System.Drawing.Color.FromArgb(color.R,color.G,color.B)};
                if(dialog.ShowDialog()==Forms.DialogResult.OK)fields[slot].Text=$"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            },"pick-widget-color-"+category+"-"+keys[i]);
            var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(chip);content.Children.Add(new TextBlock{Text="选色",FontSize=11,VerticalAlignment=VerticalAlignment.Center});choose.Content=content;choose.Margin=new Thickness(8,0,0,0);Grid.SetColumn(choose,2);row.Children.Add(choose);body.Children.Add(row);
            field.TextChanged+=(_,_)=>{
                if(sync)return;string input=field.Text.Trim(),hex="";
                if(input.Length>0&&!ThemeAppearance.TryColor(input,out hex)){field.BorderBrush=WidgetBrushes.Get("#C35950");hint.Text="颜色格式无效，未应用；请输入 HEX 或留空跟随样式。";return;}
                field.SetResourceReference(Control.BorderBrushProperty,"SettingsLine");var id=Current().Definition.Id;
                var colors=_options.CardColors.GetValueOrDefault(id)??new();
                if(slot==0)colors.Background=hex;else if(slot==1)colors.Foreground=hex;else colors.Accent=hex;
                if(colors.IsDefault)_options.CardColors.Remove(id);else _options.CardColors[id]=colors;
                Apply();sync=true;preset.SelectedIndex=_options.CardColors.ContainsKey(id)?1:0;sync=false;
                hint.Text="配色已预览；点击窗口底部保存设置后保留。";
            };
            field.LostKeyboardFocus+=(_,_)=>{if(field.Text.Trim().Length>0&&!ThemeAppearance.TryColor(field.Text,out _)){Refresh();hint.Text="无效颜色未应用，已恢复上次有效值。";}};
        }
        gradient.Checked+=(_,_)=>SetGradient();gradient.Unchecked+=(_,_)=>SetGradient();
        void SetGradient(){if(sync)return;var id=Current().Definition.Id;var colors=_options.CardColors.GetValueOrDefault(id)??new();colors.Gradient=gradient.IsChecked==true;_options.CardColors[id]=colors;Apply();sync=true;preset.SelectedIndex=1;sync=false;}
        body.Children.Add(gradient);
        ComboBox Choice(string title,string id,string[] items){typography.Children.Add(Note(title,new Thickness(0,5,0,5)));var box=new ComboBox{ItemsSource=items,MinHeight=30};AutomationProperties.SetAutomationId(box,id);AutomationProperties.SetName(box,title);typography.Children.Add(box);return box;}
        numberStyle=Choice("数字样式","widget-number-style-"+category,new[]{"标准","圆润 · Varela Round","空心轮廓","艺术 · Georgia"});
        textWeight=Choice("文字字重","widget-text-weight-"+category,new[]{"跟随版式","细体","常规","加粗"});
        Slider Scale(string title,string id,double minimum,double maximum){
            var row=new Grid{Margin=new Thickness(0,12,0,0)};row.ColumnDefinitions.Add(new(){Width=new GridLength(88)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(44)});
            row.Children.Add(new TextBlock{Text=title,FontSize=11,VerticalAlignment=VerticalAlignment.Center});var value=new TextBlock{FontSize=11,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
            var slider=new Slider{Minimum=minimum,Maximum=maximum,Value=100,TickFrequency=5,IsSnapToTickEnabled=true};AutomationProperties.SetAutomationId(slider,id);AutomationProperties.SetName(slider,title);slider.ValueChanged+=(_,_)=>value.Text=$"{slider.Value:0}%";value.Text="100%";
            Grid.SetColumn(slider,1);row.Children.Add(slider);Grid.SetColumn(value,2);row.Children.Add(value);typography.Children.Add(row);return slider;
        }
        textScale=Scale("文字大小","widget-text-scale-"+category,75,135);panelScale=Scale("卡片大小","widget-panel-scale-"+category,75,125);
        void SetTypography(){if(sync)return;var id=Current().Definition.Id;var colors=_options.CardColors.GetValueOrDefault(id)??new();
            colors.NumberStyle=(WidgetNumberStyle)Math.Max(0,numberStyle.SelectedIndex);colors.TextWeight=(WidgetTextWeight)Math.Max(0,textWeight.SelectedIndex);colors.TextScale=textScale.Value/100;colors.PanelScale=panelScale.Value/100;
            if(colors.IsDefault)_options.CardColors.Remove(id);else _options.CardColors[id]=colors;Apply();hint.Text="外观已预览；点击窗口底部保存设置后保留。";}
        numberStyle.SelectionChanged+=(_,_)=>SetTypography();textWeight.SelectionChanged+=(_,_)=>SetTypography();textScale.ValueChanged+=(_,_)=>SetTypography();panelScale.ValueChanged+=(_,_)=>SetTypography();
        var reset=ActionButton("恢复当前卡片默认外观",()=>{_options.CardColors.Remove(Current().Definition.Id);Apply();Refresh();},"widget-color-reset-"+category);
        reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new Thickness(0,5,0,0);root.Children.Add(reset);root.Children.Add(hint);
        preset.SelectionChanged+=(_,_)=>{
            if(sync||preset.SelectedIndex<0||preset.SelectedIndex==1)return;var id=Current().Definition.Id;
            var colors=_options.CardColors.GetValueOrDefault(id)??new();
            if(preset.SelectedIndex==0){colors.Background=colors.Foreground=colors.Accent="";colors.Gradient=null;}else{var p=CardPresets[preset.SelectedIndex-2];colors.Background=p.Background;colors.Foreground=p.Ink;colors.Accent=p.Accent;colors.Gradient=p.Gradient;}
            if(colors.IsDefault)_options.CardColors.Remove(id);else _options.CardColors[id]=colors;
            Apply();Refresh();
        };
        carousel.SelectionChanged+=_=>Refresh();_colorEditors.Add(Refresh);Refresh();return editor;
    }
}
