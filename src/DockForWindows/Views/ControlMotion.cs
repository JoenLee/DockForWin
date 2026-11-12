using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace DockForWindows.Views;

// Shared control-center motion parameters; the Dock's animation engine stays unchanged.
internal static class ControlMotion
{
    public const double Stiffness=230, DampingRatio=.90, Mass=1, ExitSpeed=1.12;
    public const double EntranceSpeed=1.22, PageSpeed=1.30, PageDamping=.82, LayoutSpeed=1.65;
    public static double RefreshRate {get;set;}
    private static readonly ConditionalWeakTable<DependencyObject,Dictionary<DependencyProperty,Flight>> Flights=new();
    private sealed record Flight(SpringPath Path,double Started,double Delay);
    public static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;

    internal readonly struct SpringPath
    {
        private readonly double _offset,_b,_decay,_frequency;
        private readonly double _startTime,_tailTime,_startAcceleration,_endOffset,_endVelocity,_endAcceleration;
        public readonly double Target,Duration;
        public SpringPath(double from,double target,double velocity=0,double speed=1,double damping=DampingRatio,bool softenEdges=true)
        {
            Target=target;double omega=Math.Sqrt(Stiffness/Mass)*speed;_decay=damping*omega;_frequency=omega*Math.Sqrt(1-damping*damping);
            _offset=from-target;_b=(velocity+_decay*_offset)/_frequency;
            double amplitude=Math.Sqrt(_offset*_offset+_b*_b),epsilon=Math.Max(.000001,Math.Abs(_offset)*.002);
            double va=-_decay*_offset+_frequency*_b,vb=-_decay*_b-_frequency*_offset;
            double envelope=Math.Max(amplitude/epsilon,Math.Sqrt(va*va+vb*vb)/(epsilon*omega));
            Duration=envelope<=1?0:Math.Log(envelope)/_decay;
            _startTime=softenEdges&&velocity==0?Math.Min(.024,Duration*.06):0;
            _tailTime=softenEdges?Math.Min(.045,Duration*.10):0;
            _startAcceleration=-2*_decay*velocity-(_decay*_decay+_frequency*_frequency)*_offset;
            if(!softenEdges){_endOffset=_endVelocity=_endAcceleration=0;return;}
            double endCos=Math.Cos(_frequency*Duration),endSin=Math.Sin(_frequency*Duration),endDecay=Math.Exp(-_decay*Duration);
            _endOffset=endDecay*(_offset*endCos+_b*endSin);
            _endVelocity=endDecay*((-_decay*_offset+_frequency*_b)*endCos+(-_decay*_b-_frequency*_offset)*endSin);
            _endAcceleration=-2*_decay*_endVelocity-(_decay*_decay+_frequency*_frequency)*_endOffset;
        }
        public double Value(double seconds){if(seconds>=Duration)return Target;double t=Math.Max(0,seconds);return Target+Math.Exp(-_decay*t)*(_offset*Math.Cos(_frequency*t)+_b*Math.Sin(_frequency*t))+EdgeCorrection(t,false);}
        public double Velocity(double seconds){if(seconds>=Duration)return 0;double t=Math.Max(0,seconds);return Math.Exp(-_decay*t)*((-_decay*_offset+_frequency*_b)*Math.Cos(_frequency*t)+(-_decay*_b-_frequency*_offset)*Math.Sin(_frequency*t))+EdgeCorrection(t,true);}
        // Compact C2 corrections leave the analytic spring and its duration unchanged in the middle.
        private double EdgeCorrection(double t,bool derivative)
        {
            if(_startTime>0&&t<_startTime){
                double u=t/_startTime,v=1-u;
                return derivative?-.5*_startAcceleration*_startTime*(2*u*v*v*v-3*u*u*v*v):-.5*_startAcceleration*_startTime*_startTime*u*u*v*v*v;
            }
            if(_tailTime>0&&t>Duration-_tailTime){
                double u=(t-Duration+_tailTime)/_tailTime,u2=u*u,u3=u2*u,u4=u3*u;
                if(derivative)return -_endOffset*(30*u2-60*u3+30*u4)/_tailTime-_endVelocity*(-12*u2+28*u3-15*u4)-_endAcceleration*_tailTime*(1.5*u2-4*u3+2.5*u4);
                return -_endOffset*(10*u3-15*u4+6*u4*u)-_endVelocity*_tailTime*(-4*u3+7*u4-3*u4*u)-_endAcceleration*_tailTime*_tailTime*(.5*u3-u4+.5*u4*u);
            }
            return 0;
        }
    }
    private sealed class PhysicalEase: EasingFunctionBase
    {
        private readonly SpringPath _path;private readonly double _from,_distance;
        public PhysicalEase(SpringPath path,double from){_path=path;_from=from;_distance=path.Target-from;EasingMode=EasingMode.EaseIn;}
        protected override double EaseInCore(double t)=>(_path.Value(t*_path.Duration)-_from)/_distance;
        protected override Freezable CreateInstanceCore()=>new PhysicalEase(_path,_from);
    }
    public static void To(DependencyObject owner,DependencyProperty property,double target,double? from=null,double speed=1,double delay=0,Action? completed=null,double damping=DampingRatio,double? initialVelocity=null)
    {
        var states=Flights.GetOrCreateValue(owner);double now=Now,current=from??(double)owner.GetValue(property),velocity=0;
        if(from is null&&states.TryGetValue(property,out var previous)&&now>=previous.Started+previous.Delay)velocity=previous.Path.Velocity(now-previous.Started-previous.Delay);
        if(initialVelocity.HasValue)velocity=initialVelocity.Value;var path=new SpringPath(current,target,velocity,speed,damping);var flight=new Flight(path,now,delay);states[property]=flight;
        var animated=(IAnimatable)owner;animated.BeginAnimation(property,null);if(System.Windows.Data.BindingOperations.IsDataBound(owner,property))owner.SetCurrentValue(property,current);else owner.SetValue(property,current);
        if(Math.Abs(target-current)<.0000001||path.Duration==0){owner.SetCurrentValue(property,target);completed?.Invoke();return;}
        var easing=new PhysicalEase(path,current);easing.Freeze();
        var animation=new DoubleAnimation(current,target,TimeSpan.FromSeconds(path.Duration)){BeginTime=TimeSpan.FromSeconds(delay),EasingFunction=easing};
        if(RefreshRate>1)Timeline.SetDesiredFrameRate(animation,(int)Math.Ceiling(RefreshRate));
        animation.Completed+=(_,_)=>{if(states.TryGetValue(property,out var latest)&&ReferenceEquals(flight,latest)){animated.BeginAnimation(property,null);owner.SetCurrentValue(property,target);completed?.Invoke();}};animated.BeginAnimation(property,animation);
    }
    public static void Stop(DependencyObject owner,DependencyProperty property,double value)
    {if(Flights.TryGetValue(owner,out var states))states.Remove(property);((IAnimatable)owner).BeginAnimation(property,null);owner.SetCurrentValue(property,value);}
    public static double Step(double current,double target,ref double velocity,double dt,double speed=1)
    {if(Math.Abs(current-target)<.002&&Math.Abs(velocity)<.02){velocity=0;return target;}var path=new SpringPath(current,target,velocity,speed,softenEdges:false);double value=path.Value(dt);velocity=path.Velocity(dt);return value;}
}
