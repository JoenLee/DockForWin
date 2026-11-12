using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using DockForWindows.Views;

internal static class GenerateIconMetrics
{
    internal static int Run(string path)
    {
        _=new Application();var type=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.ControlCenterIcon")!;
        var icons=(Dictionary<string,string>)type.GetField("Icons",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        ((Dictionary<string,Geometry>)type.GetField("Cache",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Clear();
        if(type.GetField("OpticalMetrics",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) is System.Collections.IDictionary metrics)metrics.Clear();
        var code=new StringBuilder("using System.Collections.Generic;\nnamespace DockForWindows.Views;\ninternal sealed partial class ControlCenterIcon\n{\n    // Precomputed from the pinned official SVGs; regenerate with --generate-control-icon-metrics.\n    private static readonly Dictionary<string,(double Extent,double X,double Y)> OpticalMetrics=new()\n    {\n");
        string N(double value)=>value.ToString("G17",CultureInfo.InvariantCulture);
        foreach(var item in icons.DistinctBy(p=>p.Value)){
            var geometry=(Geometry)type.GetMethod("Shape",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{item.Key})!;var matrix=((MatrixTransform)geometry.Transform).Matrix;
            code.Append("        [\"").Append(item.Value).Append("\"]=(").Append(N(1/matrix.M11)).Append(',').Append(N(-matrix.OffsetX/matrix.M11)).Append(',').Append(N(-matrix.OffsetY/matrix.M22)).Append("),\n");
        }
        code.Append("    };\n}\n");File.WriteAllText(path,code.ToString());Console.WriteLine("Generated official icon optical metrics: "+path);return 0;
    }
}
