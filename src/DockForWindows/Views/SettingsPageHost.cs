using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DockForWindows.Views;

internal sealed class SettingsPageHost:Grid
{
    private sealed record Route(int Section,string Title,bool Detail,Func<FrameworkElement> Build){public FrameworkElement? Page;}
    private readonly Dictionary<string,Route> _routes=new(StringComparer.Ordinal);
    private readonly List<string> _history=new();private int _index=-1,_generation;
    public string CurrentKey=>_index<0?"":_history[_index];
    public int CurrentSection=>_index<0?0:_routes[CurrentKey].Section;
    public string CurrentTitle=>_index<0?"":_routes[CurrentKey].Title;
    public bool IsDetail=>_index>=0&&_routes[CurrentKey].Detail;
    public bool CanGoBack=>_index>0;public bool CanGoForward=>_index+1<_history.Count;
    public int CachedPageCount=>_routes.Values.Select(r=>r.Page).OfType<FrameworkElement>().Distinct().Count();
    internal sealed record NavigationHistory(string[] Pages,int Index);
    internal NavigationHistory CaptureHistory()=>new(_history.ToArray(),_index);
    internal void RestoreHistory(NavigationHistory state)
    {
        var valid=state.Pages.Select((key,index)=>(Key:key,Index:index)).Where(p=>_routes.ContainsKey(p.Key)).TakeLast(64).ToArray();if(valid.Length==0)return;
        _history.Clear();_history.AddRange(valid.Select(p=>p.Key));
        _index=Array.FindLastIndex(valid,p=>p.Index<=state.Index);if(_index<0)_index=0;Show(1,IsDetail);
    }
    public event Action? Navigated;
    public SettingsPageHost(){ClipToBounds=true;Unloaded+=(_,_)=>{_generation++;foreach(FrameworkElement page in Children)Stop(page);};}
    public void Register(string key,int section,string title,bool detail,Func<FrameworkElement> factory)=>_routes.Add(key,new(section,title,detail,factory));
    public void Navigate(string key)
    {
        if(!_routes.ContainsKey(key))throw new ArgumentException("Unknown settings page",nameof(key));
        if(CurrentKey==key)return;
        bool detail=_routes[key].Detail||IsDetail;
        if(CanGoForward)_history.RemoveRange(_index+1,_history.Count-_index-1);
        _history.Add(key);if(_history.Count>64)_history.RemoveAt(0);_index=_history.Count-1;Show(1,detail);
    }
    public void Back(){if(!CanGoBack)return;bool detail=IsDetail;_index--;Show(-1,detail||IsDetail);}
    public void Forward(){if(!CanGoForward)return;bool detail=IsDetail;_index++;Show(1,detail||IsDetail);}
    private static void Stop(FrameworkElement page)
    {
        double opacity=page.Opacity;var shift=(TranslateTransform)page.RenderTransform;double x=shift.X;
        page.BeginAnimation(OpacityProperty,null);shift.BeginAnimation(TranslateTransform.XProperty,null);page.Opacity=opacity;shift.X=x;
    }
    private void Show(int direction,bool detail)
    {
        var route=_routes[CurrentKey];var next=route.Page??=route.Build();
        if(next.RenderTransform is not TranslateTransform)next.RenderTransform=new TranslateTransform();
        var previous=Children.OfType<FrameworkElement>().Where(p=>p!=next).OrderByDescending(p=>p.Opacity).FirstOrDefault();
        bool present=Children.Contains(next),focus=Children.OfType<FrameworkElement>().Any(p=>p.IsKeyboardFocusWithin);
        double discardedOpacity=Children.OfType<FrameworkElement>().Where(p=>p!=previous&&p!=next).Sum(p=>p.Opacity);
        foreach(var page in Children.OfType<FrameworkElement>().ToArray()){Stop(page);if(page!=previous&&page!=next)Children.Remove(page);}
        // Keep discarded intermediate pages from draining opacity during repeated retargeting.
        if(previous is not null&&discardedOpacity>0)previous.Opacity=Math.Clamp(previous.Opacity+discardedOpacity,0,1);
        int generation=++_generation;bool animate=IsLoaded&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
        double offset=detail?20:8,seconds=detail ? .27 : .2;
        if(!present){next.Opacity=animate?0:1;((TranslateTransform)next.RenderTransform).X=animate?direction*offset:0;Children.Add(next);}
        next.IsHitTestVisible=true;KeyboardNavigation.SetTabNavigation(next,KeyboardNavigationMode.Continue);Panel.SetZIndex(next,1);
        if(previous is not null){
            previous.IsHitTestVisible=false;KeyboardNavigation.SetTabNavigation(previous,KeyboardNavigationMode.None);Panel.SetZIndex(previous,0);
            if(animate){var fade=new DoubleAnimation(0,TimeSpan.FromSeconds(seconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};fade.Completed+=(_,_)=>{if(generation==_generation&&Children.Contains(previous)){Stop(previous);Children.Remove(previous);}};previous.BeginAnimation(OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);SettingsSwitchMotion.Move((TranslateTransform)previous.RenderTransform,TranslateTransform.XProperty,-direction*offset,seconds);}
            else Children.Remove(previous);
        }
        if(animate){next.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromSeconds(seconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);SettingsSwitchMotion.Move((TranslateTransform)next.RenderTransform,TranslateTransform.XProperty,0,seconds);}
        else{next.Opacity=1;SettingsSwitchMotion.Move((TranslateTransform)next.RenderTransform,TranslateTransform.XProperty,0,0);}
        if(focus){next.Focusable=true;next.Focus();}Navigated?.Invoke();
    }
}
