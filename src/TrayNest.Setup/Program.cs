using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using TrayNest.Installation;

namespace TrayNest.Setup;

internal sealed record ProtectionState(string Id,string Target,string Backup,string Profile,bool Existed,bool Completed);
internal sealed record RegistryEntry(string Name,int Kind,JsonElement Value);
internal sealed record ShellState(string Key,bool Existed,RegistryEntry[] Values,byte[]? Desktop,byte[]? Menu);
internal static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int MessageBoxW(nint owner,string text,string caption,uint type);
    [DllImport("WebView2Loader.dll",CharSet=CharSet.Unicode)]private static extern int GetAvailableCoreWebView2BrowserVersionString(string? path,out nint version);
    [STAThread]private static int Main(string[] args)
    {
        string? errorPath=null;
        try
        {
            if(args.Length<1)throw new IOException("安装保护组件应由 TrayNest 或安装向导调用。");
            if(args[0]=="--probe")
            {
                int result=GetAvailableCoreWebView2BrowserVersionString(null,out nint version);
                if(version!=0)Marshal.FreeCoTaskMem(version);
                return result==0&&version!=0?0:1;
            }
            if(args.Length<4)throw new IOException("安装保护参数不完整。");
            string target=Path.GetFullPath(args[1]).TrimEnd('\\'),profile=Path.GetFullPath(args[2]).TrimEnd('\\');
            string statePath=Path.GetFullPath(args[3]);errorPath=statePath+".error.txt";
            if(InstallationPaths.Overlaps(target,profile)||Path.GetPathRoot(target)==target)throw new IOException("程序目录与配置目录必须分开。");
            InstallationPaths.ValidateParents(target);
            switch(args[0])
            {
                case "--prepare":
                    ProcessMaintenance.StopAsync(target,profile,CancellationToken.None).GetAwaiter().GetResult();
                    bool existed=Directory.Exists(target);
                    if(existed&&(!File.Exists(Path.Combine(target,"NestedTray.exe"))||!File.Exists(Path.Combine(target,"NestedTray.runtimeconfig.json"))))throw new IOException("目标目录不是 TrayNest 程序目录，请选择其他位置。");
                    string id=Guid.NewGuid().ToString("N"),backup=target+".previous-"+id;
                    Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                    if(existed)
                    {
                        Directory.CreateDirectory(backup);
                        foreach(string file in Package.EnumerateFiles(target))
                        {
                            string relative=Path.GetRelativePath(target,file);string copy=Path.Combine(backup,relative);
                            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(file,copy);
                            if(Package.Hash(file)!=Package.Hash(copy))throw new IOException("旧版备份校验失败，安装尚未开始。");
                        }
                    }
                    foreach(string name in new[]{"settings.json","settings.json.bak"})if(File.Exists(Path.Combine(profile,name)))File.Copy(Path.Combine(profile,name),statePath+"."+name,true);
                    SnapshotShell(statePath,args.Length>5?args[5]:"TrayNest_is1");
                    Save(statePath,new(id,target,backup,profile,existed,false));return 0;
                case "--verify":case "--verify-only":
                    var state=Read(statePath,target,profile);
                    var preserved=state.Existed?Package.EnumerateFiles(state.Backup).ToDictionary(p=>Path.GetRelativePath(state.Backup,p).Replace('\\','/'),Package.Hash,StringComparer.OrdinalIgnoreCase):null;
                    Package.Verify(target,args.Length>4?args[4]:null,installed:true,preserved:preserved);
                    if(args[0]=="--verify")ProcessMaintenance.StartAndVerifyAsync(target,CancellationToken.None,profile).GetAwaiter().GetResult();
                    Save(statePath,state with{Completed=true});return 0;
                case "--restore":
                    var failed=Read(statePath,target,profile);
                    if(failed.Completed)return 0;
                    if(failed.Existed&&!Directory.Exists(failed.Backup))throw new IOException("原版备份缺失，不能自动恢复。");
                    ProcessMaintenance.StopAsync(target,profile,CancellationToken.None).GetAwaiter().GetResult();
                    if(Directory.Exists(target))Directory.Move(target,target+".failed-"+failed.Id);
                    if(failed.Existed)
                    {
                        Directory.Move(failed.Backup,target);
                    }
                    RestoreShell(statePath,target);
                    Save(statePath,failed with{Completed=true});
                    return 0;
                case "--install":case "--uninstall":
                    string installer=statePath;
                    if(!File.Exists(installer))throw new IOException("安装或卸载入口缺失。");
                    ProcessMaintenance.StopAsync(target,profile,CancellationToken.None).GetAwaiter().GetResult();
                    var start=new ProcessStartInfo(installer){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal};
                    start.ArgumentList.Add("/NORESTART");start.ArgumentList.Add("/NOCLOSEAPPLICATIONS");
                    if(args[0]=="--install")start.ArgumentList.Add("/DIR="+target);
                    Process.Start(start);return 0;
                case "--cleanup-startup":
                    CleanupStartup(target);return 0;
                default:throw new IOException("未知的安装保护操作。");
            }
        }
        catch(Exception error)
        {
            if(errorPath is not null){try{Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);File.WriteAllText(errorPath,error.Message,new System.Text.UTF8Encoding(true));}catch{}}
            if(args.FirstOrDefault() is "--install" or "--uninstall")MessageBoxW(0,error.Message,"TrayNest",0x10);
            return 1;
        }
    }
    private static void Save(string path,ProtectionState state){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(state));File.Move(path+".tmp",path,true);}
    private const string UninstallRoot=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
    private static string DesktopLink=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"TrayNest.lnk");
    private static string MenuLink=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"TrayNest","TrayNest.lnk");
    private static void SnapshotShell(string statePath,string name)
    {
        if(name is not ("TrayNest_is1" or "TrayNest.TN017.Tests_is1"))throw new IOException("安装登记范围无效。");
        using var key=Registry.CurrentUser.OpenSubKey(UninstallRoot+name);
        var values=key?.GetValueNames().Select(n=>new RegistryEntry(n,(int)key.GetValueKind(n),JsonSerializer.SerializeToElement(key.GetValue(n,null,RegistryValueOptions.DoNotExpandEnvironmentNames)))).ToArray()??[];
        byte[]? Read(string path)=>File.Exists(path)?File.ReadAllBytes(path):null;
        File.WriteAllText(statePath+".shell.json",JsonSerializer.Serialize(new ShellState(name,key is not null,values,Read(DesktopLink),Read(MenuLink))));
    }
    private static void RestoreShell(string path,string target)
    {
        var shell=JsonSerializer.Deserialize<ShellState>(File.ReadAllText(path+".shell.json"))??throw new IOException("安装登记备份缺失。");
        if(shell.Key is not ("TrayNest_is1" or "TrayNest.TN017.Tests_is1"))throw new IOException("安装登记范围无效。");
        Registry.CurrentUser.DeleteSubKeyTree(UninstallRoot+shell.Key,false);
        if(shell.Existed)
        {
            using var key=Registry.CurrentUser.CreateSubKey(UninstallRoot+shell.Key);
            foreach(var item in shell.Values)
            {
                var kind=(RegistryValueKind)item.Kind;
                object value=kind switch{RegistryValueKind.DWord=>item.Value.GetInt32(),RegistryValueKind.QWord=>item.Value.GetInt64(),RegistryValueKind.Binary=>item.Value.Deserialize<byte[]>()!,RegistryValueKind.MultiString=>item.Value.Deserialize<string[]>()!,_=>item.Value.GetString()!};
                key.SetValue(item.Name,value,kind);
            }
        }
        void RestoreLink(string link,byte[]? previous)
        {
            if(previous is not null){Directory.CreateDirectory(Path.GetDirectoryName(link)!);File.WriteAllBytes(link,previous);return;}
            if(!File.Exists(link))return;
            dynamic automation=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true)!)!;object? shortcut=null;
            try{shortcut=automation.CreateShortcut(link);if(string.Equals((string)((dynamic)shortcut).TargetPath,Path.Combine(target,"NestedTray.exe"),StringComparison.OrdinalIgnoreCase))File.Delete(link);}
            finally{if(shortcut is not null)Marshal.FinalReleaseComObject(shortcut);Marshal.FinalReleaseComObject(automation);}
        }
        RestoreLink(DesktopLink,shell.Desktop);RestoreLink(MenuLink,shell.Menu);
    }
    private static ProtectionState Read(string path,string target,string profile)
    {
        var state=JsonSerializer.Deserialize<ProtectionState>(File.ReadAllText(path))??throw new IOException("安装保护记录缺失。");
        if(!Guid.TryParseExact(state.Id,"N",out _)||state.Target!=target||state.Profile!=profile||state.Backup!=target+".previous-"+state.Id)throw new IOException("安装保护记录与目标不匹配。");
        InstallationPaths.ValidateParents(state.Backup);return state;
    }
    private static void CleanupStartup(string target)
    {
        string executable=Path.Combine(target,"NestedTray.exe");
        dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true)!)!;object? folder=null,task=null;
        try
        {
            service.Connect();folder=service.GetFolder("\\");string name="TrayNest-Logon-"+System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            try{task=((dynamic)folder).GetTask(name);}catch(COMException e)when((uint)e.HResult==0x80070002){}
            if(task is not null)
            {
                var xml=System.Xml.Linq.XDocument.Parse((string)((dynamic)task).Xml);var ns=xml.Root!.Name.Namespace;
                if(string.Equals(xml.Root.Element(ns+"Actions")?.Element(ns+"Exec")?.Element(ns+"Command")?.Value,executable,StringComparison.OrdinalIgnoreCase))((dynamic)folder).DeleteTask(name,0);
            }
        }
        finally{if(task is not null)Marshal.FinalReleaseComObject(task);if(folder is not null)Marshal.FinalReleaseComObject(folder);Marshal.FinalReleaseComObject(service);}
        using var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
        if(string.Equals(run?.GetValue("NestedTray") as string,"\""+executable+"\"",StringComparison.OrdinalIgnoreCase))run!.DeleteValue("NestedTray",false);
    }
}
