using System.Diagnostics;
using System.Drawing.Imaging;

namespace NestedTray;

internal sealed class SavedTool
{
    public string Rule {get;set;}="";
    public string Name {get;set;}="";
    public string Executable {get;set;}="";
    public string LaunchTarget {get;set;}="";
    public string ApplicationId {get;set;}="";
    internal int ApplicationIdOwner;
    public string IconPng {get;set;}="";
    internal SavedTool Copy()=>(SavedTool)MemberwiseClone();
    internal static SavedTool FromRecord(IconRecord record)
    {
        var tool=new SavedTool{Rule=record.Rule,Executable=record.Executable,LaunchTarget=record.Executable,ApplicationId=ProcessExecutable.ApplicationId(record.ProcessId),
            Name=string.IsNullOrWhiteSpace(record.Tooltip)?Path.GetFileNameWithoutExtension(record.Executable):record.Tooltip};
        if(record.Image!=null)
        {
            using var bitmap=new Bitmap(record.Image,new Size(32,32));using var bytes=new MemoryStream();
            bitmap.Save(bytes,ImageFormat.Png);tool.IconPng=Convert.ToBase64String(bytes.ToArray());
        }
        return tool;
    }
    internal Bitmap ReadImage()
    {
        if(!string.IsNullOrEmpty(IconPng))try{using var bytes=new MemoryStream(Convert.FromBase64String(IconPng));using var image=Image.FromStream(bytes);if(image.Width<=256&&image.Height<=256)return new Bitmap(image);}catch(Exception error)when(error is ArgumentException or FormatException or OutOfMemoryException){}
        if(File.Exists(Executable))try{using var icon=Icon.ExtractAssociatedIcon(Executable);if(icon!=null)return icon.ToBitmap();}catch(Exception error)when(error is ArgumentException or IOException or System.ComponentModel.Win32Exception){}
        return SystemIcons.Application.ToBitmap();
    }
}

internal sealed class ToolLibrary(Settings settings):IDisposable
{
    private readonly Dictionary<string,IconRecord> offline=new();
    private readonly Dictionary<string,IconRecord> active=new();
    internal bool EnsureEntries(IEnumerable<IconRecord> records)
    {
        var live=records.Where(record=>record.Selected).GroupBy(record=>record.Rule).ToDictionary(group=>group.Key,group=>group.First());
        bool changed=false;
        foreach(string rule in settings.Collected)
        {
            var saved=settings.Tools.FirstOrDefault(tool=>tool.Rule==rule);
            if(saved==null)
            {
                if(live.TryGetValue(rule,out var record))saved=SavedTool.FromRecord(record);
                else {int split=rule.LastIndexOf('|');string exe=split>=0?rule[..split]:"";saved=new SavedTool{Rule=rule,Executable=exe,LaunchTarget=exe,Name=Path.GetFileNameWithoutExtension(exe)};}
                settings.Tools.Add(saved);changed=true;
            }
            else if(live.TryGetValue(rule,out var record)&&string.IsNullOrEmpty(saved.IconPng)&&record.Image!=null)
            {
                var captured=SavedTool.FromRecord(record);saved.IconPng=captured.IconPng;saved.Name=captured.Name;
                if(string.IsNullOrEmpty(saved.Executable))saved.Executable=record.Executable;
                if(string.IsNullOrEmpty(saved.LaunchTarget))saved.LaunchTarget=record.Executable;
                changed=true;
            }
        }
        foreach(var tool in settings.Tools)if(string.IsNullOrEmpty(tool.ApplicationId)&&live.TryGetValue(tool.Rule,out var owner)&&tool.ApplicationIdOwner!=owner.ProcessId)
        {tool.ApplicationIdOwner=owner.ProcessId;string id=ProcessExecutable.ApplicationId(owner.ProcessId);if(id.Length>0){tool.ApplicationId=id;changed=true;}}
        return changed;
    }
    internal IconRecord[] Snapshot(IEnumerable<IconRecord> records)
    {
        var all=records.Where(record=>record.Selected).GroupBy(record=>record.Rule).ToDictionary(group=>group.Key,group=>group.ToArray());
        var result=new List<IconRecord>();
        foreach(var tool in settings.Tools.Where(tool=>settings.Collected.Contains(tool.Rule)))
        {
            if(all.TryGetValue(tool.Rule,out var candidates))
            {
                var record=active.TryGetValue(tool.Rule,out var previous)&&candidates.Contains(previous)?previous:candidates[0];
                active[tool.Rule]=record;result.Add(record);
            }
            else
            {
                active.Remove(tool.Rule);
                if(!offline.TryGetValue(tool.Rule,out var record))
                {
                    record=new IconRecord{Key="offline:"+tool.Rule,Rule=tool.Rule,Tooltip=tool.Name,Executable=tool.Executable,Selected=true,IsOffline=true,Image=tool.ReadImage()};
                    offline.Add(tool.Rule,record);
                }
                result.Add(record);
            }
        }
        foreach(var key in offline.Keys.Where(key=>!settings.Collected.Contains(key)).ToArray()){offline[key].Dispose();offline.Remove(key);active.Remove(key);}
        return result.ToArray();
    }
    internal void Reorder(string rule,int index)
    {
        var saved=settings.Tools.FirstOrDefault(tool=>tool.Rule==rule);if(saved==null)return;
        var previous=settings.Tools.ToList();settings.Tools.Remove(saved);settings.Tools.Insert(Math.Clamp(index,0,settings.Tools.Count),saved);
        try{settings.Save();}catch{settings.Tools=previous;throw;}
    }
    internal void Forget(string rule)
    {
        var previous=settings.Tools.ToList();var rules=settings.Collected.ToList();
        settings.Tools.RemoveAll(tool=>tool.Rule==rule);settings.Collected.Remove(rule);
        try{settings.Save();}catch{settings.Tools=previous;settings.Collected=rules;throw;}
    }
    public void Dispose(){foreach(var record in offline.Values)record.Dispose();offline.Clear();active.Clear();}
}

internal readonly record struct ToolLaunchResult(bool Started,string Message);
internal enum ToolLaunchState { Idle, Waiting, RunningWithoutIcon, InvalidTarget, Failed, TimedOut }
internal readonly record struct ToolLaunchStatus(ToolLaunchState State,string Message,long Until=0);
internal sealed class ToolLauncher
{
    private readonly Dictionary<string,ToolLaunchStatus> states=new();
    private readonly Dictionary<string,string> statesExecutable=new();
    internal Func<long> Clock=()=>Environment.TickCount64;
    internal Func<string,bool> IsRunning=ExecutableRunning;
    internal Action<string> Start=path=>{using var process=Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)??""});};
    internal ToolLaunchStatus Status(string rule)
    {
        if(!states.TryGetValue(rule,out var value))return new(ToolLaunchState.Idle,"未运行或图标暂不可用；单击尝试启动，右键管理。");
        if(value.State==ToolLaunchState.RunningWithoutIcon&&statesExecutable.TryGetValue(rule,out var executable)&&!IsRunning(executable))
        {states.Remove(rule);statesExecutable.Remove(rule);return new(ToolLaunchState.Idle,"程序进程已退出；单击可重新启动。");}
        if(value.State is ToolLaunchState.Waiting or ToolLaunchState.RunningWithoutIcon&&Clock()>=value.Until)
            states[rule]=value=new(ToolLaunchState.TimedOut,"仍未发现图标，可右键检查启动文件。");
        return value;
    }
    internal bool IsPending(string rule)=>Status(rule).State is ToolLaunchState.Waiting or ToolLaunchState.RunningWithoutIcon;
    internal void Registered(string rule){states.Remove(rule);statesExecutable.Remove(rule);}
    private ToolLaunchResult Result(string rule,ToolLaunchState state,string message,bool started=false)
    {states[rule]=new(state,message,state is ToolLaunchState.Waiting or ToolLaunchState.RunningWithoutIcon?Clock()+12000:0);return new(started,message);}
    internal ToolLaunchResult Launch(SavedTool tool)
    {
        if(IsPending(tool.Rule))return new(false,Status(tool.Rule).Message);
        statesExecutable[tool.Rule]=tool.Executable;
        string path=tool.LaunchTarget;
        bool packaged=!string.IsNullOrEmpty(tool.ApplicationId)&&string.Equals(path,tool.Executable,StringComparison.OrdinalIgnoreCase);
        if(!packaged&&(!Path.IsPathFullyQualified(path)||!File.Exists(path)||!(Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase))))
            return Result(tool.Rule,ToolLaunchState.InvalidTarget,"无法从此处启动。请右键此图标设置有效的程序或快捷方式。");
        try
        {
            if(IsRunning(tool.Executable))return Result(tool.Rule,ToolLaunchState.RunningWithoutIcon,"检测到该程序的进程，但尚未发现托盘图标；不代表程序已正常启动。");
            if(packaged)PackagedTool.Start(tool.ApplicationId);else Start(path);return Result(tool.Rule,ToolLaunchState.Waiting,"正在启动，等待图标回到原位。",true);
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException or System.Security.SecurityException or System.Runtime.InteropServices.COMException)
        {AppDiagnostics.Error("TN4101",error);return Result(tool.Rule,ToolLaunchState.Failed,"启动未成功。请检查启动文件，或从开始菜单打开该软件。");}
    }
    private static bool ExecutableRunning(string executable)
    {
        if(!Path.IsPathFullyQualified(executable))return false;
        foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using(process)try{if(string.Equals(ProcessExecutable.Read(process.Id),executable,StringComparison.OrdinalIgnoreCase))return true;}catch(Exception error)when(error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}
        }
        return false;
    }
}
