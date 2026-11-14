using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DockForWindows.Views;

public sealed class SettingsScrollViewportConverter:IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)
    {
        if(values.Length<3||values[0] is not double viewport||values[1] is not double maximum||values[2] is not double length||length<=0||maximum<=0)return 0d;
        double grip=Math.Min(48,length*.18);
        return Math.Min(viewport,maximum*grip/Math.Max(1,length-grip));
    }
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
