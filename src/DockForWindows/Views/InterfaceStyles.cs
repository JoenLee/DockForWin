using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Views;

public sealed record InterfaceColors(string Background,string Sidebar,string Card,string Text,string Muted,string Border,
    string Selection,string SelectionText,string Accent,string OnAccent,string Track,string SidebarText,string SidebarMuted);

public static class InterfaceStyles
{
    public static string[] Names { get; }={"系统"};
    // Legacy enum values remain readable; every window palette resolves to the design system's
    // Porcelain (light) or Graphite (dark) palette with the system blue accent.
    public static InterfaceColors Resolve(InterfaceStyle style,bool dark)=>dark
        ?new("#1D1D1F","#262628","#2C2C2E","#F5F5F7","#98989D","#3A3A3C","#0A84FF","#FFFFFF","#0A84FF","#FFFFFF","#48484A","#F5F5F7","#98989D")
        :new("#F5F5F7","#ECECEF","#FFFFFF","#1D1D1F","#6E6E73","#E5E5EA","#007AFF","#FFFFFF","#007AFF","#FFFFFF","#E5E5EA","#1D1D1F","#6E6E73");
}
