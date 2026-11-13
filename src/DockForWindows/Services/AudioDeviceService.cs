using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DockForWindows.Services;

public sealed record AudioOutput(string Id,string Name,bool Default);
public static class AudioDeviceService
{
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevices
    {void EnumAudioEndpoints(int flow,uint mask,out ICollection collection);void GetDefaultAudioEndpoint(int flow,int role,out IDevice device);}
    [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface ICollection
    {void GetCount(out uint count);void Item(uint index,out IDevice device);}
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevice
    {void Activate(ref Guid iid,uint context,nint parameters,out nint result);void OpenPropertyStore(uint mode,out IProperties properties);void GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);void GetState(out uint state);}
    [StructLayout(LayoutKind.Sequential)]private struct Key{public Guid Format;public uint Id;}
    [StructLayout(LayoutKind.Explicit,Size=24)]private struct Variant{[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public nint Pointer;}
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IProperties
    {void GetCount(out uint count);void GetAt(uint index,out Key key);void GetValue(ref Key key,out Variant value);}
    [DllImport("ole32.dll")]private static extern int PropVariantClear(ref Variant value);
    // Windows exposes endpoint enumeration publicly. The desktop default-route COM ABI
    // is isolated here; a failed activation is surfaced with the native Sound fallback.
    [ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]private class Policy{}
    [ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IPolicy
    {
        void GetMixFormat();void GetDeviceFormat();void ResetDeviceFormat();void SetDeviceFormat();void GetProcessingPeriod();void SetProcessingPeriod();void GetShareMode();void SetShareMode();void GetPropertyValue();void SetPropertyValue();
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);
    }
    private static void Release(object? obj){if(obj is not null&&Marshal.IsComObject(obj))Marshal.FinalReleaseComObject(obj);}
    public static IReadOnlyList<AudioOutput> Outputs()=>Endpoints(0);
    public static IReadOnlyList<AudioOutput> Inputs()=>Endpoints(1);
    // The shared enumerator is serialised with SystemControlService; see CoreAudio.
    private static IReadOnlyList<AudioOutput> Endpoints(int flow)=>CoreAudio.Use(enumerator=>
    {
        ICollection? collection=null;IDevice? current=null;
        try
        {
            var devices=(IDevices)enumerator;string selected="";try{devices.GetDefaultAudioEndpoint(flow,1,out current);current.GetId(out selected);}catch(COMException){}
            devices.EnumAudioEndpoints(flow,1,out collection);collection.GetCount(out uint count);var result=new List<AudioOutput>();
            for(uint i=0;i<count;i++)
            {
                IDevice? device=null;IProperties? properties=null;
                try{collection.Item(i,out device);device.GetId(out string id);device.OpenPropertyStore(0,out properties);var key=new Key{Format=new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),Id=14};properties.GetValue(ref key,out var value);string name;try{name=value.Type==31?Marshal.PtrToStringUni(value.Pointer)??id:id;}finally{PropVariantClear(ref value);}result.Add(new(id,name,id==selected));}
                finally{Release(properties);Release(device);}
            }
            return (IReadOnlyList<AudioOutput>)result;
        }
        finally{Release(current);Release(collection);}
    });
    public static void SetDefault(string id)
    {
        object? policy=null;try{policy=new Policy();for(int role=0;role<3;role++)((IPolicy)policy).SetDefaultEndpoint(id,role);}
        finally{Release(policy);}
    }
}

