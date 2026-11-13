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
    // Every page is built once and then kept: switching only flips visibility, so returning to a page costs no
    // rebuild, no reload and no re-layout of its template. Top-level tabs switch instantly; a detail page eases in
    // (only the incoming page moves, so no two whole pages are ever composited at once).
    private void Show(int direction,bool detail)
    {
        var route=_routes[CurrentKey];var next=route.Page??=route.Build();
        if(next.RenderTransform is not TranslateTransform)next.RenderTransform=new TranslateTransform();
        bool focus=Children.OfType<FrameworkElement>().Any(p=>p.IsKeyboardFocusWithin);
        if(!Children.Contains(next))Children.Add(next);
        ++_generation;
        foreach(FrameworkElement page in Children)
        {
            if(page==next)continue;
            Stop(page);page.Opacity=1;((TranslateTransform)page.RenderTransform).X=0;
            page.Visibility=Visibility.Collapsed;page.IsHitTestVisible=false;KeyboardNavigation.SetTabNavigation(page,KeyboardNavigationMode.None);Panel.SetZIndex(page,0);
        }
        next.Visibility=Visibility.Visible;next.IsHitTestVisible=true;KeyboardNavigation.SetTabNavigation(next,KeyboardNavigationMode.Continue);Panel.SetZIndex(next,1);
        bool animate=detail&&IsLoaded&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
        Stop(next);
        if(animate)
        {
            const double seconds=.22;next.Opacity=0;((TranslateTransform)next.RenderTransform).X=direction*14;
            next.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromSeconds(seconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);
            SettingsSwitchMotion.Move((TranslateTransform)next.RenderTransform,TranslateTransform.XProperty,0,seconds);
        }
        else{next.Opacity=1;SettingsSwitchMotion.Move((TranslateTransform)next.RenderTransform,TranslateTransform.XProperty,0,0);}
        if(focus){next.Focusable=true;next.Focus();}Navigated?.Invoke();
    }
    // Builds the given pages ahead of time and lays them out once while hidden, so their first visit is instant too.
    public void Prewarm(IEnumerable<string> keys)
    {
        foreach(var key in keys)
        {
            if(!_routes.TryGetValue(key,out var route)||key==CurrentKey)continue;
            var page=route.Page??=route.Build();if(Children.Contains(page))continue;
            if(page.RenderTransform is not TranslateTransform)page.RenderTransform=new TranslateTransform();
            page.Visibility=Visibility.Hidden;page.IsHitTestVisible=false;KeyboardNavigation.SetTabNavigation(page,KeyboardNavigationMode.None);Children.Add(page);
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,()=>{if(page.Visibility==Visibility.Hidden)page.Visibility=Visibility.Collapsed;});
        }
    }
}
