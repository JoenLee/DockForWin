using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using DockForWindows.Views;

internal static class SpringCurveChecks
{
    private static readonly Type Spring=typeof(ControlCenterWindow).Assembly.GetType("DockForWindows.Views.ControlMotion+SpringPath")!;
    private static object Path(double from,double to,double velocity,double speed,double damping,bool soften)=>Activator.CreateInstance(Spring,new object[]{from,to,velocity,speed,damping,soften})!;
    private static double Value(object path,double t)=>(double)Spring.GetMethod("Value")!.Invoke(path,new object[]{t})!;
    private static double Velocity(object path,double t)=>(double)Spring.GetMethod("Velocity")!.Invoke(path,new object[]{t})!;
    private static double Duration(object path)=>(double)Spring.GetField("Duration")!.GetValue(path)!;
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    internal static int Run(string output)
    {
        try{
            double maxChange=0;int cases=0;var csv=new List<string>{"progress,rawPosition,softPosition,rawVelocity,softVelocity"};
            foreach(double speed in new[]{1d,1.12,1.22,1.30,1.65})foreach(double damping in new[]{.82,.90})foreach(double initialVelocity in new[]{0d,2d,-2d}){
                var raw=Path(0,1,initialVelocity,speed,damping,false);var soft=Path(0,1,initialVelocity,speed,damping,true);double duration=Duration(raw),h=duration*1e-6;
                Assert(Duration(soft)==duration,"Duration changed");Assert(Math.Abs(Value(soft,0))<1e-12&&Value(soft,duration)==1,"Endpoints changed");
                Assert(Math.Abs(Velocity(soft,0)-initialVelocity)<1e-10&&Velocity(soft,duration)==0,"Endpoint velocity changed");
                for(int i=0;i<=1000;i++){
                    double t=duration*i/1000d,delta=Math.Abs(Value(soft,t)-Value(raw,t));maxChange=Math.Max(maxChange,delta);
                    if(i>=60&&i<=900)Assert(delta<1e-12&&Math.Abs(Velocity(soft,t)-Velocity(raw,t))<1e-11,"Middle spring changed");
                    if(i>0&&i<1000)Assert(Math.Abs((Value(soft,t+h)-Value(soft,t-h))/(2*h)-Velocity(soft,t))<.0001,"Position/velocity mismatch");
                    if(speed==1.30&&damping==.82&&initialVelocity==0)csv.Add(string.Join(",",new[]{i/1000d,Value(raw,t),Value(soft,t),Velocity(raw,t),Velocity(soft,t)}.SelectInvariant()));
                }
                Assert(Math.Abs(Velocity(soft,duration-h))<.0001,"Terminal velocity does not converge");
                if(initialVelocity==0)Assert(Math.Abs((Velocity(soft,h)-Velocity(soft,0))/h)<1,"Initial acceleration was not softened");
                foreach(double join in new[]{Math.Min(.024,duration*.06),duration-Math.Min(.045,duration*.10)}){
                    if(initialVelocity!=0&&join<duration*.5)continue;
                    double leftAcceleration=(Velocity(soft,join)-Velocity(soft,join-h))/h,rightAcceleration=(Velocity(soft,join+h)-Velocity(soft,join))/h;
                    Assert(Math.Abs(leftAcceleration-rightAcceleration)<.1,"Acceleration discontinuity at a join");
                }
                cases++;
            }
            Assert(maxChange<.006,$"Endpoint correction too large: {maxChange}");File.WriteAllLines(output,csv);
            Console.WriteLine($"PASS {cases} spring cases; durations unchanged; middle 6%-90% identical; C1/C2 joins verified; maximum displacement adjustment {maxChange:P4}; no easing reset for inherited velocity or per-frame integration");return 0;
        }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);return 1;}
    }
    private static IEnumerable<string> SelectInvariant(this IEnumerable<double> values){foreach(double value in values)yield return value.ToString("R",CultureInfo.InvariantCulture);}
}
