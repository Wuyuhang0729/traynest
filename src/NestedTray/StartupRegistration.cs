using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;

namespace NestedTray;

internal static class StartupRegistration
{
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static string TaskName=>"TrayNest-Logon-"+WindowsIdentity.GetCurrent().User!.Value;
    internal static string Executable=>Environment.ProcessPath!;
    internal static bool Enabled
    {
        get
        {
            try {string? xml=ReadTask();if(xml!=null)return Matches(xml,Executable,WindowsIdentity.GetCurrent().User!.Value);}
            catch(Exception error)when(error is COMException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { /* A startup status query must not prevent the tray application from opening. */ }
            using var key=Registry.CurrentUser.OpenSubKey(RunKey);
            if(!string.Equals(key?.GetValue("NestedTray") as string,"\""+Executable+"\"",StringComparison.OrdinalIgnoreCase))return false;
            using var approval=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            return approval?.GetValue("NestedTray") is not byte[] bytes||bytes.Length==0||bytes[0] is 2 or 6;
        }
        set
        {
            try
            {
                WithFolder(folder=>
                {
                    if(value)
                    {
                        string sid=WindowsIdentity.GetCurrent().User!.Value;
                        dynamic task=folder.RegisterTask(TaskName,Xml(Executable,sid),6,sid,null,3,null);
                        try {if(!Matches((string)task.Xml,Executable,sid))throw new IOException("Startup registration verification failed.");}
                        finally {Marshal.FinalReleaseComObject(task);}
                    }
                    else {try{folder.DeleteTask(TaskName,0);}catch(Exception error)when((uint)error.HResult==0x80070002){}}
                    return 0;
                });
                // Remove the previous method only after the replacement has been verified.
                using var key=Registry.CurrentUser.OpenSubKey(RunKey,true);key?.DeleteValue("NestedTray",false);
                AppDiagnostics.Info("TN0010",new(){["Enabled"]=value.ToString(),["Backend"]="LogonTask"});
            }
            catch(Exception error)when(error is COMException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {throw new TrayNestException("TN4002",error);}
        }
    }

    private static T WithFolder<T>(Func<dynamic,T> action)
    {
        dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true)!)!;
        object? folder=null;
        try {service.Connect();folder=service.GetFolder("\\");return action(folder);}
        finally {if(folder!=null)Marshal.FinalReleaseComObject(folder);Marshal.FinalReleaseComObject(service);}
    }
    internal static string? ReadTask(string? name=null)=>WithFolder<string?>(folder=>
    {
        object? task=null;
        try {task=folder.GetTask(name??TaskName);return (string)((dynamic)task).Xml;}
        catch(Exception error)when((uint)error.HResult==0x80070002){return null;}
        finally {if(task!=null)Marshal.FinalReleaseComObject(task);}
    });
    internal static string Xml(string executable,string sid)
    {
        XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name,object? content)=>new(ns+name,content);
        return new XDocument(new XElement(ns+"Task",new XAttribute("version","1.2"),
            E("RegistrationInfo",E("Description","TrayNest · 当前用户登录后启动并恢复已保存的收纳顺序")),
            E("Triggers",new XElement(ns+"LogonTrigger",E("Enabled","true"),E("Delay","PT10S"),E("UserId",sid))),
            E("Principals",new XElement(ns+"Principal",new XAttribute("id","User"),E("UserId",sid),E("LogonType","InteractiveToken"),E("RunLevel","LeastPrivilege"))),
            E("Settings",new object[]{E("MultipleInstancesPolicy","IgnoreNew"),E("DisallowStartIfOnBatteries","false"),E("StopIfGoingOnBatteries","false"),E("StartWhenAvailable","true"),E("RunOnlyIfNetworkAvailable","false"),E("Enabled","true"),E("ExecutionTimeLimit","PT0S")}),
            new XElement(ns+"Actions",new XAttribute("Context","User"),new XElement(ns+"Exec",E("Command",executable),E("Arguments","--startup"),E("WorkingDirectory",Path.GetDirectoryName(executable)))))).ToString();
    }
    internal static bool Matches(string xml,string executable,string sid)
    {
        try
        {
            var doc=XDocument.Parse(xml);XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
            var task=doc.Root!;var trigger=task.Element(ns+"Triggers")?.Elements().SingleOrDefault();
            var principal=task.Element(ns+"Principals")?.Elements().SingleOrDefault();var exec=task.Element(ns+"Actions")?.Elements().SingleOrDefault();
            return (task.Element(ns+"Settings")?.Element(ns+"Enabled")?.Value??"true")=="true"&&trigger?.Name==ns+"LogonTrigger"
                &&(trigger.Element(ns+"Enabled")?.Value??"true")=="true"&&SameUser(trigger.Element(ns+"UserId")?.Value,sid)
                &&SameUser(principal?.Element(ns+"UserId")?.Value,sid)&&principal!.Element(ns+"LogonType")?.Value=="InteractiveToken"
                &&(principal.Element(ns+"RunLevel")?.Value??"LeastPrivilege")=="LeastPrivilege"&&exec?.Name==ns+"Exec"
                &&string.Equals(exec.Element(ns+"Command")?.Value,executable,StringComparison.OrdinalIgnoreCase)
                &&exec.Element(ns+"Arguments")?.Value=="--startup";
        }
        catch(Exception error)when(error is System.Xml.XmlException or InvalidOperationException){return false;}
    }
    private static bool SameUser(string? value,string sid)
    {
        if(value==sid)return true;
        if(string.IsNullOrWhiteSpace(value))return false;
        try{return ((SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier))).Value==sid;}
        catch(Exception error)when(error is IdentityNotMappedException or System.Security.SecurityException or ArgumentException){return false;}
    }
    internal static int Configure(string mode,string report)
    {
        try
        {
            if(mode=="enable")Enabled=true;else if(mode=="disable")Enabled=false;else if(mode!="status")throw new ArgumentException("Unknown startup operation.");
            File.WriteAllText(report,JsonSerializer.Serialize(new{Enabled,Backend=ReadTask()==null?"LegacyRun":"LogonTask",TaskName},DiagnosticStore.JsonOptions));return 0;
        }
        catch(Exception error)
        {
            var problem=AppDiagnostics.Error(error is TrayNestException known?known.Code:"TN4002",error);
            File.WriteAllText(report,JsonSerializer.Serialize(new{Enabled=false,problem.Code,problem.EventId,Error=error.GetType().Name,HResult=error.HResult,InnerType=error.InnerException?.GetType().Name,InnerHResult=error.InnerException?.HResult},DiagnosticStore.JsonOptions));return 1;
        }
    }
}
