using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using DockForWindows.Models;
using DockForWindows.Views;

namespace DockForWindows.Services;

internal static class TrayMenu
{
    internal static ContextMenu Create(Action openSettings,Action openControlCenter,Action exit,Func<DockSettings>? settings=null)
    {
        var menu=new ContextMenu{StaysOpen=false};
        void Add(string text,Action action){var item=new MenuItem{Header=text};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add(LocalizedText.Get("打开设置"),openSettings);Add(LocalizedText.Get("打开控制中心"),openControlCenter);menu.Items.Add(new Separator());Add(LocalizedText.Get("退出 DockForWin"),exit);
        NativeMenuStyles.Apply(menu,settings?.Invoke()??new());
        menu.Opened+=(_,_)=>{NativeMenuStyles.Apply(menu,settings?.Invoke()??new());};
        return menu;
    }
    internal static void Show(ContextMenu menu){menu.IsOpen=false;var owner=Application.Current?.MainWindow;menu.PlacementTarget=owner;if(owner is not null)NativeMethods.SetForegroundWindow(new WindowInteropHelper(owner).Handle);menu.Placement=PlacementMode.MousePoint;menu.IsOpen=true;}
}
