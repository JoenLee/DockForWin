using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static partial class WidgetContent
{
    private static readonly ControlTemplate ReferenceSeekTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'><Grid Background='Transparent'><Track Name='PART_Track' Height='3' VerticalAlignment='Center' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Background='{TemplateBinding Foreground}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='{TemplateBinding Background}' CornerRadius='2'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='4' Height='4'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#EFE6D6' CornerRadius='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Background='#111916'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='{TemplateBinding Background}' CornerRadius='2'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");
    internal static bool ReferencePlayer(string id) => id.StartsWith("music-form-", StringComparison.Ordinal) || id is "music-studio-record" or "music-pocket";
    private static FrameworkElement ReferencePlayerLayout(WidgetDefinition definition, WidgetRenderData data)
    {
        bool single = definition.Id == "music-form-single", room = definition.Id == "music-form-room";
        bool studio = definition.Id == "music-studio-record", pocket = definition.Id == "music-pocket";
        double width = single ? 240 : pocket ? 272 : 496, height = single ? 240 : room || studio ? 496 : pocket ? 480 : 240;
        var canvas = new Canvas { Width = width, Height = height };
        Brush cream = new LinearGradientBrush(Color.FromRgb(232,223,204), Color.FromRgb(171,163,145), 75);
        Brush steel = new LinearGradientBrush(new GradientStopCollection { new(Color.FromRgb(88,92,98),0),new(Color.FromRgb(169,172,178),.12),new(Color.FromRgb(119,123,130),.45),new(Color.FromRgb(54,59,65),1)},90);
        void Place(FrameworkElement child,double x,double y) { Canvas.SetLeft(child,x);Canvas.SetTop(child,y);canvas.Children.Add(child); }
        Border Panel(double x,double y,double w,double h,Brush fill,double radius=18) {
            var panel = new Border { Width=w,Height=h,Background=fill,CornerRadius=new CornerRadius(radius),BorderThickness=new Thickness(1),BorderBrush=WidgetBrushes.Get("#505C615B") };
            Place(panel,x,y);return panel;
        }
        TextBlock Label(string text,double x,double y,double w,double size,string color="#EEEADD",bool bold=false) {
            var label = new TextBlock { Text=text,Width=w,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,FontFamily=new FontFamily("Segoe UI"),Foreground=WidgetBrushes.Get(color),TextTrimming=TextTrimming.CharacterEllipsis };
            Place(label,x,y);return label;
        }
        var chassis = Panel(0,0,width,height,studio?WidgetBrushes.Get("#493039"):pocket?steel:cream, single?26:32);
        chassis.BorderBrush=WidgetBrushes.Get(pocket?"#B9BCC1":"#F3EDDE");
        string title=string.IsNullOrWhiteSpace(data.Media?.Title)?"尚未播放":data.Media.Title;
        string artist=string.IsNullOrWhiteSpace(data.Media?.Artist)?"等待系统媒体会话":data.Media.Artist;
        void Deck(double x,double y,double w,double h,bool full) {
            Panel(x,y,w,h,new LinearGradientBrush(Color.FromRgb(105,72,49),Color.FromRgb(48,34,27),90));
            var grain=new ReferenceWood { Width=w,Height=h };Place(grain,x,y);
            double disc=full?216:single?104:164;
            var record = new WidgetRecordPlayer(data,disc,false,true,studio);
            Place(record,x+(w-disc)*(single?.38:.42),y+(h-disc)*.5);
            foreach(var at in new[]{new Point(x+14,y+14),new Point(x+w-14,y+14),new Point(x+14,y+h-14),new Point(x+w-14,y+h-14)}) {
                var screw=new Ellipse{Width=5,Height=5,Fill=steel,Stroke=WidgetBrushes.Get("#202926"),StrokeThickness=.5};Place(screw,at.X-2.5,at.Y-2.5);
            }
            if(full) { Label("DIRECT\nDRIVE",x+20,y+21,60,8,"#C6C4B3");Label("QUARTZ LOCK",x+20,y+h-76,100,8,"#C6C4B3");
                Label("FORM / TT–01",x+w-126,y+h-29,120,8); }
        }
        Button Command(string command,double x,double y,double size,bool primary=false,string? symbol=null) {
            bool available=command switch {"previous"=>data.Media?.CanPrevious==true,"next"=>data.Media?.CanNext==true,_=>data.Media?.CanPlay==true};
            string glyph=symbol??(command=="previous"?"M2,1 V17 M16,1 L4,9 16,17 Z":command=="next"?"M16,1 V17 M2,1 L14,9 2,17 Z":data.Media?.Playing==true?"M3,1 L7,1 7,17 3,17 Z M11,1 L15,1 15,17 11,17 Z":"M4,1 L17,9 4,17 Z");
            var icon=new System.Windows.Shapes.Path { Data=Geometry.Parse(glyph),Fill=WidgetBrushes.Get(primary?"#343D29":"#EEEADD"),Width=primary?13:14,Height=primary?15:14,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
            var body=new Grid{Width=size,Height=size};if(primary)body.Children.Add(new Ellipse {Fill=new LinearGradientBrush(Color.FromRgb(247,180,111),Color.FromRgb(195,109,55),90),Stroke=WidgetBrushes.Get("#F7E7CF"),StrokeThickness=1.2});body.Children.Add(icon);
            double hit=Math.Max(32,Math.Max(size,32/(definition.Size.Width/width))); var button=new Button { Width=hit,Height=hit,MinHeight=0,Padding=new Thickness(0),Template=PlainButton,Background=Brushes.Transparent,Content=body,IsEnabled=available&&data.MediaCommand!=null,ToolTip=command=="previous"?"上一首":command=="next"?"下一首":"播放 / 暂停" };
            AutomationProperties.SetAutomationId(button,"widget-music-"+command);button.Click+=(_,_)=>data.MediaCommand?.Invoke(command);Place(button,x-(hit-size)/2,y-(hit-size)/2);return button;
        }
        void Progress(double x,double y,double w) {
            var timeline=MusicTimeline(data,true);
            foreach(var slider in WidgetMusicView.Elements<Slider>(timeline)) { slider.Template=ReferenceSeekTemplate; slider.Background=WidgetBrushes.Get("#111916"); }
            foreach(var label in WidgetMusicView.Elements<FrameworkElement>(timeline))if(AutomationProperties.GetAutomationId(label)=="widget-music-remaining")AutomationProperties.SetAutomationId(label,"widget-music-duration");timeline.Width=w;timeline.Resources["WidgetAccent"]=WidgetBrushes.Get(pocket?"#2DB4E6":"#EB9A58");timeline.Resources["WidgetMuted"]=WidgetBrushes.Get(pocket?"#40474D":"#A2AFA4");Place(timeline,x,y);
        }
        void Controls(double x,double y,double w,double h,bool art) {
            Panel(x,y,w,h,WidgetBrushes.Get("#1E2723"));
            double pad=16, textX=x+pad, metaY=single||room?16:46;
            if(art) { var cover=Art(data.Media?.Artwork,40);Place(cover,x+pad,y+metaY);textX+=52; }
            Label(title,textX,y+metaY,w-(textX-x)-(single?105:18),room?19:single?15:18,bold:true);
            Label(artist,textX,y+metaY+(single?20:26),w-(textX-x)-18,room?12:single?10:12,"#A4AFA5");
            if(single) { Command("toggle",x+w-49,y+14,32,true);var compactVolume=new WidgetVolumeButton(data){Opacity=0};compactVolume.MouseEnter+=(_,_)=>compactVolume.Opacity=1;compactVolume.MouseLeave+=(_,_)=>compactVolume.Opacity=0;compactVolume.GotKeyboardFocus+=(_,_)=>compactVolume.Opacity=1;compactVolume.LostKeyboardFocus+=(_,_)=>compactVolume.Opacity=0;Place(compactVolume,x+w-92,y+14);return; }
            Progress(x+pad,y+(room?68:88),w-pad*2);
            double cy=y+h-(room?48:76);
            Command("previous",x+w*.24-14,cy,28);Command("toggle",x+w*.5-22,cy-8,44,true);Command("next",x+w*.76-14,cy,28);
            var volume=new WidgetVolumeButton(data);Place(volume,x+w-38,y+12);
        }
        if(pocket) {
            Panel(24,30,224,172,WidgetBrushes.Get("#EAF3F6"),4);
            Label("Now Playing",34,33,190,9,"#20303D",true);
            var cover=Art(data.Media?.Artwork,78);Place(cover,36,57);
            Label(title,124,59,110,12,"#26353D",true);Label(artist,124,80,108,10,"#47525C");Progress(36,167,200);
            Panel(42,250,188,188,new LinearGradientBrush(Color.FromRgb(47,49,51),Color.FromRgb(23,25,27),90),94);
            Label("MENU",106,259,60,9,"#E4E5E7",true).TextAlignment=TextAlignment.Center;
            Command("previous",51,327,31);Command("next",190,327,31);Command("toggle",119,396,34);
            var center=Command("toggle",110,318,52);AutomationProperties.SetAutomationId(center,"widget-pocket-select");center.Content=new Ellipse{Width=52,Height=52,Fill=steel};
            var volume=new WidgetVolumeButton(data);Place(volume,120,285);
        } else if(studio) {
            Label("L   ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪",55,12,385,7,"#BBA8A8");
            var record=new WidgetRecordPlayer(data,390,false,true,true);Place(record,45,34);
            Progress(30,421,436);Command("previous",152,459,28);Command("toggle",224,452,40,true);Command("next",304,459,28);
            var volume=new WidgetVolumeButton(data);Place(volume,430,458);
        } else if(single) { Deck(16,16,208,137,false);Controls(16,165,208,59,false);foreach(var button in new[]{Command("previous",20,112,30),Command("next",188,112,30)}){button.Opacity=0;button.MouseEnter+=(_,_)=>button.Opacity=1;button.MouseLeave+=(_,_)=>button.Opacity=0;button.GotKeyboardFocus+=(_,_)=>button.Opacity=1;button.LostKeyboardFocus+=(_,_)=>button.Opacity=0;} }
        else if(room) { Label("FORM   /   ANALOG TT–01",16,16,360,9,"#252A24",true);Deck(16,42,464,254,true);Controls(16,309,464,171,true); }
        else { Deck(16,16,220,208,false);Controls(249,16,231,208,true);Label("FORM / TT–01",265,32,165,9);Label("LOSSLESS",265,199,140,7,"#ADC58E").Text=data.Media==null?"等待媒体":"系统媒体会话"; }
        return new Viewbox { Child=canvas,Stretch=Stretch.Uniform };
    }
}

internal sealed class ReferenceWood : FrameworkElement
{
    protected override void OnRender(DrawingContext dc) {
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize),18,18));
        for(int i=0;i<32;i++) { double y=(i+.2)*ActualHeight/32;var pen=new Pen(WidgetBrushes.Get(i%3==0?"#44301D14":"#294E2F1D"),i%4==0?2:1);
            var g=new StreamGeometry();using(var p=g.Open()){p.BeginFigure(new Point(0,y),false,false);p.BezierTo(new Point(ActualWidth*.3,y-3),new Point(ActualWidth*.7,y+4),new Point(ActualWidth,y-1),true,false);}dc.DrawGeometry(null,pen,g); }
        dc.Pop();
    }
}

internal sealed class ReferenceRecordFace : FrameworkElement
{
    private readonly bool _metal;
    internal ReferenceRecordFace(bool metal) { _metal=metal; }
    protected override void OnRender(DrawingContext dc) {
        double r=Math.Min(ActualWidth,ActualHeight)/2;var center=new Point(r,r);
        dc.DrawEllipse(_metal?WidgetBrushes.Get("#151515"):new LinearGradientBrush(Color.FromRgb(216,222,204),Color.FromRgb(99,118,109),80),new Pen(WidgetBrushes.Get("#26302B"),1),center,r,r);
        dc.DrawEllipse(WidgetBrushes.Get("#171D1B"),null,center,r*.963,r*.963);
        for(int i=0;i<95;i++){double ring=r*(.36+i*.0063);dc.DrawEllipse(null,new Pen(WidgetBrushes.Get(i%2==0?"#3A45413D":"#60474D44"),.35),center,ring,ring);}
        if(!_metal)for(int i=0;i<100;i++){double a=i*Math.PI*2/100;dc.DrawEllipse(WidgetBrushes.Get("#24352C"),null,new Point(r+Math.Sin(a)*r*.985,r+Math.Cos(a)*r*.985),.55,.55);}
    }
}
