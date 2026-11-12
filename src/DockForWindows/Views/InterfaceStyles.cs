using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Views;

public sealed record InterfaceColors(string Background,string Sidebar,string Card,string Text,string Muted,string Border,
    string Selection,string SelectionText,string Accent,string OnAccent,string Track,string SidebarText,string SidebarMuted);

public static class InterfaceStyles
{
    public static string[] Names { get; }={"极简黑白"};
    // Legacy enum values remain readable; all window palettes now resolve to monochrome.
    public static InterfaceColors Resolve(InterfaceStyle style,bool dark)=>dark
        ?new("#111111","#191919","#222222","#F5F5F5","#A3A3A3","#393939","#EFEFEF","#171717","#EEEEEE","#171717","#484848","#E8E8E8","#A3A3A3")
        :new("#F4F4F4","#F0F0F0","#FFFFFF","#191919","#717171","#DEDEDE","#DADADD","#191919","#202020","#FFFFFF","#D5D5D5","#292929","#7B7B7B");
}