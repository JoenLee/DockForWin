using System;
using System.Linq;

namespace DockForWindows.Models;

public static class SettingsAccentPresets
{
    public const string Default="#2F8FFF";
    private static readonly (string Name,string Color)[] ChineseValues={
        ("晴空蓝",Default),("靛青","#5E6BC7"),("鸢尾紫","#8062C5"),("湖水青","#168A9C"),
        ("松石绿","#248878"),("苔叶绿","#52834D"),("玫瑰粉","#B65980"),("琥珀金","#A57825"),("黑色","#000000")};
    private static readonly Lazy<(string Name,string Color)[]> EnglishValues=new(()=>ChineseValues.Select(p=>(LocalizedText.Get(p.Name),p.Color)).ToArray());
    public static (string Name,string Color)[] Values=>LocalizedText.IsEnglish?EnglishValues.Value:ChineseValues;
    public static string Normalize(string? color)=>ThemeAppearance.TryColor(color,out var normalized)?normalized:Default;
}
