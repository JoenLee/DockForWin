using System;
using System.Collections.Generic;
using System.Text.Json;
using DockForWindows.Models;

namespace DockForWindows.Services;

// Imports data only. No executable C# or shared theme/material settings are accepted.
public static class ControlCenterDesign
{
    public static void Apply(string json,ControlCenterOptions destination)
    {
        if(json.Length>128*1024)throw new ArgumentException(LocalizedText.Get("参数文件超过 128 KB。"));
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        if(!root.TryGetProperty("Schema",out var schema)||schema.GetString() is not ("DockForWindows.ControlCenterDesign/v1" or "DockForWindows.ControlCenterDesign/v2"))throw new ArgumentException(LocalizedText.Get("请选择控制中心设计参数文件。"));
        var geometry=root.GetProperty("Geometry");var next=JsonSerializer.Deserialize<ControlCenterGeometry>(geometry.GetRawText())??new();
        if(!geometry.TryGetProperty("SquareRoundness",out _)&&geometry.TryGetProperty("MediaRoundness",out var oldRadius))next.SquareRoundness=oldRadius.GetDouble();next.Normalize();
        var modules=new List<ControlModuleLayout>();
        foreach(var entry in root.GetProperty("Modules").EnumerateArray())
        {
            if(!Enum.TryParse<ControlModule>(entry.GetProperty("Module").GetString(),out var kind)||!Enum.IsDefined(kind))continue;
            var module=new ControlModuleLayout{Module=kind,Visible=!entry.TryGetProperty("Visible",out var visible)||visible.GetBoolean(),Span=entry.TryGetProperty("Span",out var span)?span.GetInt32():2};
            if(entry.TryGetProperty("Shape",out var shape)&&Enum.TryParse<ControlCardShape>(shape.GetString(),out var parsed)&&Enum.IsDefined(parsed))module.SetShape(parsed);modules.Add(module);
        }
        var candidate=destination.Copy();candidate.Geometry=next;candidate.Modules=modules;candidate.LayoutVersion=2;candidate.Normalize();destination.Geometry=candidate.Geometry;destination.Modules=candidate.Modules;destination.LayoutVersion=candidate.LayoutVersion;
    }
}
