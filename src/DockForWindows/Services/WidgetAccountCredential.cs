using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DockForWindows.Services;

// Generic credentials are protected by Windows and scoped to the signed-in Windows user.
internal static class WidgetAccountCredential
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Credential
    {
        internal uint Flags,Type;
        internal string TargetName,Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint CredentialBlobSize;
        internal nint CredentialBlob;
        internal uint Persist,AttributeCount;
        internal nint Attributes;
        internal string TargetAlias,UserName;
    }
    internal static string? Read(string key)
    {
        if(!CredRead(key,1,0,out var pointer))return null;
        try{var value=Marshal.PtrToStructure<Credential>(pointer);var bytes=new byte[value.CredentialBlobSize];Marshal.Copy(value.CredentialBlob,bytes,0,bytes.Length);try{return Encoding.UTF8.GetString(bytes);}finally{Array.Clear(bytes);}}
        finally{CredFree(pointer);}
    }
    internal static void Write(string key,string value)
    {
        var bytes=Encoding.UTF8.GetBytes(value);if(bytes.Length>2560)throw new InvalidOperationException("Microsoft credential is too large.");
        nint pointer=Marshal.AllocHGlobal(bytes.Length);try{Marshal.Copy(bytes,0,pointer,bytes.Length);var credential=new Credential{Type=1,TargetName=key,UserName="Microsoft",CredentialBlob=pointer,CredentialBlobSize=(uint)bytes.Length,Persist=2};if(!CredWrite(ref credential,0))throw new Win32Exception(Marshal.GetLastWin32Error());}
        finally{for(int i=0;i<bytes.Length;i++)Marshal.WriteByte(pointer,i,0);Marshal.FreeHGlobal(pointer);Array.Clear(bytes);}
    }
    internal static void Delete(string key)=>CredDelete(key,1,0);
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CredRead(string key,uint type,uint flags,out nint credential);
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CredWrite(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CredDelete(string key,uint type,uint flags);
    [DllImport("advapi32.dll")]private static extern void CredFree(nint credential);
}
