using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal static partial class WidgetContent
{
    internal static FrameworkElement Weather(WidgetDefinition definition,string city,WidgetWeatherSnapshot? weather)
        =>Render(definition,new(new(),new(){City=city},weather));
    internal static FrameworkElement Calendar(WidgetDefinition definition,DateTime month,DateTime selected,IReadOnlyList<WidgetCalendarEvent> entries,Action<DateTime> selectDate,WidgetWeatherSnapshot? weather=null,string city="")
        =>Render(definition,new(new(){Events=entries.Select(e=>e.Copy()).ToList()},new(){City=city},weather,Month:month,SelectedDate:selected,SelectDate:selectDate));
    internal static IReadOnlyList<DateTime?> MonthDays(DateTime month)
    {
        var first=new DateTime(month.Year,month.Month,1);int offset=((int)first.DayOfWeek+6)%7;
        return Enumerable.Range(0,42).Select(i=>i<offset||i>=offset+DateTime.DaysInMonth(month.Year,month.Month)?(DateTime?)null:first.AddDays(i-offset)).ToArray();
    }
}
