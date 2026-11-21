using System;
using System.Diagnostics;
using DockForWindows.Models;
using DockForWindows.Views;

namespace DockForWindows.Services;

internal sealed partial class WidgetRuntime
{
    private void MicrosoftChanged(WidgetKind? kind){if(!_disposed)Changed?.Invoke(kind);}
    internal WidgetRenderData Populate(WidgetRenderData data,WidgetKind kind)
    {
        data=PopulateExtras(data,kind);
        if(!data.Source.MicrosoftAccount||kind is not (WidgetKind.Reminders or WidgetKind.Notes))return data;
        Microsoft.Ensure(kind);Microsoft.Errors.TryGetValue(kind,out var error);
        return data with{
            RemoteTasks=kind==WidgetKind.Reminders?Microsoft.Tasks:null,
            FolderNotes=kind==WidgetKind.Notes?Microsoft.Notes:null,
            Error=error,Complete=Microsoft.Complete,
            EditTask=_=>{try{Process.Start(new ProcessStartInfo("https://to-do.live.com/tasks/"){UseShellExecute=true});}catch(Exception ex){Microsoft.Errors[WidgetKind.Reminders]=ex.Message;MicrosoftChanged(WidgetKind.Reminders);}},
            EditNote=null
        };
    }
}
