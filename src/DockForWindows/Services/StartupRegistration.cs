using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace DockForWindows.Services;

public static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "DockForWindows";
    public static string? ReadCommand(string? registryPath=null) { using var key=Registry.CurrentUser.OpenSubKey(registryPath??Key);return key?.GetValue(Name) as string; }
    public static string BuildCommand(string process, string entryAssembly, string profile)
    {
        static string Quote(string path) => "\""+path.Replace("\"", "")+"\"";
        string command=Quote(process);
        if(Path.GetFileNameWithoutExtension(process).Equals("dotnet",StringComparison.OrdinalIgnoreCase))command+=" "+Quote(entryAssembly);
        command+=" --data-directory "+Quote(profile);
        if(command.Length>260)throw new InvalidOperationException("启动路径过长，请将程序放到较短的安装路径。");
        return command;
    }
    public static void WriteCommand(string? command,string? registryPath=null)
    {
        using var key=Registry.CurrentUser.CreateSubKey(registryPath??Key,true);
        if(command is null)key.DeleteValue(Name,false);else key.SetValue(Name,command,RegistryValueKind.String);
    }
    public static string CurrentCommand(string profile) => BuildCommand(Environment.ProcessPath ?? throw new InvalidOperationException("无法确认程序路径"),Assembly.GetEntryAssembly()?.Location ?? "",profile);
}
