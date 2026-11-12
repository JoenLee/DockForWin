using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DockForWindows.Services;

// Only cached hit rectangles are read in the hook. UI Automation runs separately.
public sealed class ControlCenterEntryService : IDisposable
{
    private delegate nint MouseProc(int code,nint message,nint data);
    [StructLayout(LayoutKind.Sequential)]private struct MouseData{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindow(string className,string? title);
    [DllImport("user32.dll")]private static extern nint SetWindowsHookEx(int id,MouseProc callback,nint module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(nint window,int id);
    private readonly Action<string>? _notify;private readonly Func<string>? _hotkey; private string _registeredHotkey="";
    private readonly Dispatcher _dispatcher;private readonly Action _toggle;private readonly Func<bool> _enabled;private readonly MouseProc _callback;private readonly HwndSource _messages;private readonly DispatcherTimer _refresh;
    internal static Rect AccessoryBounds=Rect.Empty,NetworkAnchor=Rect.Empty;
    internal static event Action<Rect>? NetworkAnchorChanged;
    internal static bool IsDedicatedButton(Point point)=>!AccessoryBounds.IsEmpty&&AccessoryBounds.Contains(point);
    private Rect[] _targets=Array.Empty<Rect>();private nint _hook;private bool _disposed,_busy,_swallowUp;
    public bool HotkeyRegistered {get;private set;}
    public bool MouseHookActive=>_hook!=0;public int TaskbarTargetCount=>_targets.Length;
    public ControlCenterEntryService(Dispatcher dispatcher,Action toggle,Func<bool> enabled,Func<string>? hotkey=null,Action<string>? notify=null)
    {
        _dispatcher=dispatcher;_toggle=toggle;_enabled=enabled;_hotkey=hotkey;_notify=notify;
        _messages=new HwndSource(new HwndSourceParameters("DockForWindows.ControlCenter.Messages"){ParentWindow=new nint(-3),Width=0,Height=0,WindowStyle=0});
        _messages.AddHook((nint h,int message,nint w,nint l,ref bool handled)=>{if(message==0x312&&w==0xD0C){handled=true;_toggle();}return 0;});
        UpdateHotkey();
        _callback=MouseHook;_hook=SetWindowsHookEx(14,_callback,0,0);
        _refresh=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1.5)};_refresh.Tick+=async(_,_)=>await RefreshTargets();_refresh.Start();_ = RefreshTargets();
    }
    private async Task RefreshTargets()
    {
        if(_busy||_disposed)return;UpdateHotkey();_busy=true;
        try
        {
            var rects=await Task.Run(()=>
            {
                var list=new List<Rect>();var network=Rect.Empty;var occupied=new List<Rect>();
                foreach(AutomationElement taskbar in AutomationElement.RootElement.FindAll(TreeScope.Children,new OrCondition(new PropertyCondition(AutomationElement.ClassNameProperty,"Shell_TrayWnd"),new PropertyCondition(AutomationElement.ClassNameProperty,"Shell_SecondaryTrayWnd"))))
                { var handle=new nint(taskbar.Current.NativeWindowHandle);if(handle==0)continue;
                try{var bar=AutomationElement.FromHandle(handle);foreach(AutomationElement element in bar.FindAll(TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition)){var current=element.Current;if(!current.ClassName.StartsWith("SystemTray.",StringComparison.Ordinal))continue;string name=current.Name;if(!current.BoundingRectangle.IsEmpty)occupied.Add(current.BoundingRectangle);if(network.IsEmpty&&(name.StartsWith("网络",StringComparison.Ordinal)||name.StartsWith("Network",StringComparison.OrdinalIgnoreCase)))network=current.BoundingRectangle;
                    if(name.StartsWith("网络",StringComparison.Ordinal)||name.StartsWith("音量",StringComparison.Ordinal)||name.StartsWith("电源",StringComparison.Ordinal)||name.StartsWith("Network",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Speakers",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Volume",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Battery",StringComparison.OrdinalIgnoreCase))
                    {var r=current.BoundingRectangle;if(!r.IsEmpty&&r.Width>0)list.Add(r);}}
                }catch(ElementNotAvailableException){}catch(InvalidOperationException){} } if(!network.IsEmpty){double previous=double.NegativeInfinity;foreach(var r in occupied)if(r.Right<=network.Left&&r.Right>previous)previous=r.Right;if(network.Left-previous<network.Height*1.25)network=Rect.Empty;}return (Normal:list.ToArray(),Anchor:network);
            });if(!_disposed){_targets=rects.Normal;if(NetworkAnchor!=rects.Anchor){NetworkAnchor=rects.Anchor;NetworkAnchorChanged?.Invoke(rects.Anchor);}}
        }
        catch{}finally{_busy=false;}
    }
    private nint MouseHook(int code,nint message,nint data)
    {
        if(code>=0&&!_disposed)
        {
            if(message==0x202&&_swallowUp){_swallowUp=false;return 1;}
            if(message==0x201&&GetAsyncKeyState(0x11)>=0)
            {var mouse=Marshal.PtrToStructure<MouseData>(data);var point=new Point(mouse.X,mouse.Y);if(_enabled())foreach(var target in _targets)if(target.Contains(point)){_swallowUp=true;_dispatcher.BeginInvoke(_toggle);return 1;}}
        }
        return CallNextHookEx(_hook,code,message,data);
    }
    public static string FormatHotkey(ModifierKeys modifiers,Key key)
    {
        string text="";if(modifiers.HasFlag(ModifierKeys.Control))text+="Ctrl+";if(modifiers.HasFlag(ModifierKeys.Alt))text+="Alt+";if(modifiers.HasFlag(ModifierKeys.Shift))text+="Shift+";if(modifiers.HasFlag(ModifierKeys.Windows))text+="Win+";return text+key;
    }
    public static bool TryParseHotkey(string text,out uint modifiers,out uint key)
    {
        modifiers=0;key=0;try{var parts=text.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);if(parts.Length<2)return false;for(int i=0;i<parts.Length-1;i++)modifiers|=parts[i].ToLowerInvariant() switch{"ctrl"=>2U,"alt"=>1U,"shift"=>4U,"win"=>8U,_=>0U};if((modifiers&11)==0)return false;var parsed=(Key)new KeyConverter().ConvertFromString(parts[^1])!;key=(uint)KeyInterop.VirtualKeyFromKey(parsed);return key>0&&parsed is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin);}catch{return false;}
    }
    private void UpdateHotkey()
    {
        string text=_hotkey?.Invoke()??"Ctrl+Win+A";if(text==_registeredHotkey)return;
        if(!TryParseHotkey(text,out var modifiers,out var key))return;
        UnregisterHotKey(_messages.Handle,0xD0C);HotkeyRegistered=RegisterHotKey(_messages.Handle,0xD0C,modifiers|0x4000,key);_registeredHotkey=text;if(!HotkeyRegistered)_notify?.Invoke("快捷键 "+text+" 已被占用，请在控制中心设置中更换；托盘入口仍可使用。");
    }
    public void Dispose(){if(_disposed)return;_disposed=true;AccessoryBounds=Rect.Empty;_refresh.Stop();if(_hook!=0){UnhookWindowsHookEx(_hook);_hook=0;}UnregisterHotKey(_messages.Handle,0xD0C);_messages.Dispose();}
}


