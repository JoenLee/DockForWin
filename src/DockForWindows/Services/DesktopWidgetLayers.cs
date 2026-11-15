using System;
using System.Drawing;
using System.Threading;

namespace DockForWindows.Services;

// Desktop widgets are layered child windows of the Explorer desktop window. Neither the compositor capture of that
// window nor PrintWindow includes them (both return the wallpaper and icons only), so glass sampling the desktop
// showed the wallpaper where a widget stands. Each widget window publishes its own rendered pixels here instead;
// the backdrop service draws them over the desktop layer. Written on the UI thread, read on sampling threads.
internal static class DesktopWidgetLayers
{
    // Premultiplied BGRA rows of the whole window at its physical screen bounds. Never changed after publishing.
    internal sealed record Snapshot(byte[] Pixels,Rectangle Bounds,long Version);
    internal interface ISource
    {
        nint Handle { get; }
        Snapshot? Latest { get; }
        // Called from a sampling thread while glass is drawn over the widget; the widget throttles the work.
        void RequestRefresh();
    }
    private static readonly object Gate=new();
    private static ISource[] _sources=Array.Empty<ISource>();
    internal static ISource[] Sources=>Volatile.Read(ref _sources);
    internal static void Add(ISource source){lock(Gate){if(Array.IndexOf(_sources,source)<0){var next=new ISource[_sources.Length+1];_sources.CopyTo(next,0);next[^1]=source;Volatile.Write(ref _sources,next);}}}
    internal static void Remove(ISource source){lock(Gate){int index=Array.IndexOf(_sources,source);if(index<0)return;var next=new ISource[_sources.Length-1];for(int i=0,j=0;i<_sources.Length;i++)if(i!=index)next[j++]=_sources[i];Volatile.Write(ref _sources,next);}}
}
