using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static class WidgetTypography
{
    private sealed class Settings(double size,bool emphasis,bool numeric)
    {
        internal readonly double Size=size;
        internal readonly bool Emphasis=emphasis,Numeric=numeric;
        internal FontFamily? DefaultNumberFont;
    }
    private static readonly ConditionalWeakTable<FrameworkElement,Settings> State=new();
    private static readonly FontFamily Rounded=new(new Uri("pack://application:,,,/DockForWindows;component/"),"./Assets/Fonts/#Varela Round");
    private static readonly FontFamily Standard=new("Segoe UI Variable, Microsoft YaHei UI, Segoe UI"),Artistic=new("Georgia");
    private static readonly DependencyProperty ScaleProperty=DependencyProperty.RegisterAttached("Scale",typeof(double),typeof(WidgetTypography),new FrameworkPropertyMetadata(1d,Changed));
    private static readonly DependencyProperty WeightProperty=DependencyProperty.RegisterAttached("Weight",typeof(WidgetTextWeight),typeof(WidgetTypography),new FrameworkPropertyMetadata(WidgetTextWeight.Automatic,Changed));
    internal static readonly DependencyProperty NumberProperty=DependencyProperty.RegisterAttached("Number",typeof(WidgetNumberStyle),typeof(WidgetTypography),new FrameworkPropertyMetadata(WidgetNumberStyle.Standard,Changed));
    private static void Changed(DependencyObject owner,DependencyPropertyChangedEventArgs args)=>Apply((FrameworkElement)owner);
    internal static FrameworkElement Create(string text,double size,bool emphasis)
    {
        bool numeric=(text.Any(char.IsDigit)||text=="—°")&&text.All(c=>char.IsDigit(c)||char.IsWhiteSpace(c)||"°:/.—-".Contains(c));
        FrameworkElement element=numeric&&size>=18?new WidgetText(text):new TextBlock{Text=text};
        State.Add(element,new Settings(size,emphasis,numeric));
        element.SetResourceReference(ScaleProperty,"WidgetTextScale");element.SetResourceReference(WeightProperty,"WidgetTextWeight");element.SetResourceReference(NumberProperty,"WidgetNumberStyle");Apply(element);return element;
    }
    internal static void SetNumberFont(FrameworkElement element,string family){State.GetValue(element,_=>throw new InvalidOperationException()).DefaultNumberFont=new FontFamily(family);Apply(element);}
    private static void Apply(FrameworkElement owner)
    {
        if(!State.TryGetValue(owner,out var settings))return;
        owner.SetValue(TextElement.FontSizeProperty,settings.Size*(double)owner.GetValue(ScaleProperty));
        var weight=(WidgetTextWeight)owner.GetValue(WeightProperty) switch{WidgetTextWeight.Light=>FontWeights.Light,WidgetTextWeight.Normal=>FontWeights.Normal,WidgetTextWeight.Bold=>FontWeights.Bold,_=>settings.Emphasis?FontWeights.SemiBold:FontWeights.Normal};
        owner.SetValue(TextElement.FontWeightProperty,weight);
        owner.SetValue(TextElement.FontFamilyProperty,settings.Numeric?(WidgetNumberStyle)owner.GetValue(NumberProperty) switch{WidgetNumberStyle.Rounded=>Rounded,WidgetNumberStyle.Art=>Artistic,_=>settings.DefaultNumberFont??Standard}:Standard);
        if(owner is WidgetText number)number.UpdateMode();
    }
}

internal sealed class WidgetText : ContentControl
{
    private readonly TextBlock _label=new(){TextTrimming=TextTrimming.CharacterEllipsis};
    private readonly NumberInk _ink;
    private Geometry? _outline;
    private Pen? _pen;
    private ShapeKey _key;
    private readonly record struct ShapeKey(string Text,string Family,double Size,FontStyle Style,FontWeight Weight,FontStretch Stretch,double Dpi,double Width);
    private sealed class NumberInk(WidgetText owner):FrameworkElement
    {
        protected override void OnRender(DrawingContext context)=>owner.DrawOutline(context,RenderSize);
    }
    internal Geometry? OutlineGeometry=>_outline;
    internal string? NumberFontOverride {set{if(value!=null)WidgetTypography.SetNumberFont(this,value);}}
    internal WidgetText(string text)
    {
        Focusable=false;IsTabStop=false;HorizontalContentAlignment=HorizontalAlignment.Stretch;VerticalContentAlignment=VerticalAlignment.Stretch;
        _label.Text=text;_ink=new NumberInk(this){IsHitTestVisible=false,Visibility=Visibility.Hidden};
        var body=new Grid();body.Children.Add(_label);body.Children.Add(_ink);Content=body;
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);if(_label==null)return;
        if(e.Property==ForegroundProperty||e.Property==FontSizeProperty||e.Property==FontWeightProperty||e.Property==FontFamilyProperty||e.Property==FontStyleProperty||e.Property==FontStretchProperty){
            _label.Foreground=Foreground;_label.FontSize=FontSize;_label.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;_label.LineHeight=FontSize*1.06;_label.FontWeight=FontWeight;_label.FontFamily=FontFamily;_label.FontStyle=FontStyle;_label.FontStretch=FontStretch;
            if(e.Property!=ForegroundProperty)_outline=null;_ink?.InvalidateVisual();
        }
    }
    internal void UpdateMode()
    {
        bool outline=(WidgetNumberStyle)GetValue(WidgetTypography.NumberProperty)==WidgetNumberStyle.Outline;
        _label.Visibility=outline?Visibility.Hidden:Visibility.Visible;_ink.Visibility=outline?Visibility.Visible:Visibility.Hidden;
        _ink.InvalidateVisual();
    }
    protected override Size MeasureOverride(Size constraint)
    {
        double size=double.IsFinite(constraint.Height)?Math.Min(FontSize,Math.Max(8,constraint.Height/1.06)):FontSize;
        _label.FontSize=size;_label.LineHeight=size*1.06;return base.MeasureOverride(constraint);
    }
    private void DrawOutline(DrawingContext context,Size size)
    {
        if(size.Width<=0||size.Height<=0||Foreground==null)return;
        double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double fontSize=_label.FontSize;
        var key=new ShapeKey(_label.Text,FontFamily.Source,fontSize,FontStyle,FontWeight,FontStretch,dpi,size.Width);
        if(_outline==null||!_key.Equals(key)){
            var formatted=new FormattedText(_label.Text,CultureInfo.CurrentUICulture,FlowDirection,new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),fontSize,Foreground,dpi){MaxTextWidth=Math.Max(.1,size.Width),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};
            _outline=formatted.BuildGeometry(new Point());_outline.Freeze();_key=key;
        }
        double thickness=Math.Clamp(fontSize/44,.8,1.9);
        if(_pen==null||!ReferenceEquals(_pen.Brush,Foreground)||Math.Abs(_pen.Thickness-thickness)>.0001){_pen=new Pen(Foreground,thickness);if(_pen.CanFreeze)_pen.Freeze();}
        context.DrawGeometry(null,_pen,_outline);
    }
}
