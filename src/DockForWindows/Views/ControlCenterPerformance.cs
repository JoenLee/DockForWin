using System;
using System.Runtime.InteropServices;

namespace DockForWindows.Views;

// 高刷新率适配与 WPF 呈现回调间隔监测。
// 回调频率不等于 GPU 实际呈现帧率；不以回调或定时器次数宣称 GPU FPS。
public sealed class FramePerformance
{
    // DWM_TIMING_INFO is a packed 320-byte native structure. A short managed
    // prefix is not a valid output buffer for DwmGetCompositionTimingInfo.
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetCompositionTimingInfo(nint hwnd,nint info);
    private const int Capacity = 360;
    private readonly double[] _frames = new double[Capacity];
    private int _index, _count;
    private double _sumMs, _lastSeconds;
    private long _totalFrames, _droppedFrames;
    private double _longestGapMs;

    public double RefreshRate { get; private set; } = 60;
    public bool RefreshRateDetected { get; private set; }
    public double Fps { get; private set; }
    public double AvgFrameMs { get; private set; }
    public long TotalFrames => _totalFrames;
    public long DroppedFrames => _droppedFrames;
    public double LongestGapMs => _longestGapMs;

    // 读取窗口所在显示器的实际刷新率（支持 60/90/120/144/165/240Hz，及 DRR 动态刷新率）。
    public void Detect(nint hwnd)
    {
        try
        {
            if (hwnd == 0) return;
            var monitor=System.Windows.Forms.Screen.FromHandle(hwnd);
            var monitorRate=DockForWindows.Services.DisplayControlService.ReadRefreshRate(monitor.DeviceName);
            if(monitorRate>1&&monitorRate<1000){RefreshRate=monitorRate;RefreshRateDetected=true;return;}
            nint info=Marshal.AllocHGlobal(320);
            try{
                Marshal.WriteInt32(info,320);
                if(DwmGetCompositionTimingInfo(0,info)==0){
                    uint numerator=unchecked((uint)Marshal.ReadInt32(info,4)),denominator=unchecked((uint)Marshal.ReadInt32(info,8));
                    if(denominator!=0){double rate=(double)numerator/denominator;if(rate>1&&rate<1000){RefreshRate=rate;RefreshRateDetected=true;}}
                }
            }finally{Marshal.FreeHGlobal(info);}        }
        catch { /* 无法读取时退回默认 60，靠实测帧间隔兜底 */ }
    }

    // 每帧调用一次；seconds 为单调时钟秒，dt 由相邻两帧真实间隔得出。
    public void OnFrame(double seconds)
    {
        _totalFrames++;
        double dtMs = _lastSeconds > 0 ? (seconds - _lastSeconds) * 1000 : 0;
        _lastSeconds = seconds;
        if (dtMs <= 0) return;
        if (_count < Capacity) { _count++; _sumMs += dtMs; }
        else _sumMs += dtMs - _frames[_index];
        _frames[_index] = dtMs;
        _index = (_index + 1) % Capacity;
        AvgFrameMs = _count > 0 ? _sumMs / _count : 0;
        Fps = AvgFrameMs > 0 ? 1000 / AvgFrameMs : 0;
        // 掉帧：帧间隔明显大于 vsync 标称周期（×1.6）计为掉帧，容忍少量调度抖动。
        double vsync = 1000 / Math.Max(1, RefreshRate);
        if (dtMs > vsync * 1.6) _droppedFrames++;
        if (dtMs > _longestGapMs) _longestGapMs = dtMs;
    }

    // 按需计算 P95/P99（供调试面板低频读取，避免每帧分配）。
    public (double P95, double P99) Percentiles()
    {
        if (_count < 30) return (0, 0);
        var copy = new double[_count];
        Array.Copy(_frames, copy, _count);
        Array.Sort(copy);
        double p95 = copy[Math.Min(_count - 1, (int)(_count * .95))];
        double p99 = copy[Math.Min(_count - 1, (int)(_count * .99))];
        return (p95, p99);
    }

    public string Describe()
    {
        var (p95, p99) = Percentiles();
        return $"刷新率 {RefreshRate:0}Hz · 呈现回调 {Fps:0}/s · 平均 {AvgFrameMs:0.0}ms · P95 {p95:0.0}ms · P99 {p99:0.0}ms · 掉帧 {_droppedFrames} · 最长 {_longestGapMs:0.0}ms";
    }
}
