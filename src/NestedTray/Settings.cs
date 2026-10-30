using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace NestedTray;
internal sealed class Settings
{
    public List<string> Collected {get;set;}=new();
    internal static string DirectoryPath=>Environment.GetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NestedTray");
    internal static string FilePath=>Path.Combine(DirectoryPath,"settings.json");
    internal static Settings Load()
    {
        if(!File.Exists(FilePath))return new();
        try {return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))??new();}
        catch(Exception e) when(e is IOException or JsonException) {throw new InvalidOperationException("收纳配置无法读取，已停止启动以免覆盖原配置。",e);}
    }
    internal void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(FilePath+".tmp",FilePath,true);
    }
    internal static bool StartupEnabled
    {
        get {using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return string.Equals(key?.GetValue("NestedTray")as string,"\""+Environment.ProcessPath+"\"",StringComparison.OrdinalIgnoreCase);}
        set {using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(value)key.SetValue("NestedTray","\""+Environment.ProcessPath+"\"");else key.DeleteValue("NestedTray",false);}
    }
}

internal sealed record RecoveryItem(long Window,uint Id,Guid Guid,int ProcessId,uint OriginalState);
internal sealed class RecoveryJournal
{
    internal readonly string PathName=Path.Combine(Settings.DirectoryPath,"recovery-"+Environment.ProcessId+".json");
    private Process? watcher;
    internal void Start()
    {
        Directory.CreateDirectory(Settings.DirectoryPath);
        Write(Array.Empty<RecoveryItem>());
        watcher=Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--recover-after-exit",Environment.ProcessId.ToString(),PathName}});
        if(watcher==null)throw new InvalidOperationException("无法启动图标恢复保护进程。");
    }
    internal void Write(IEnumerable<RecoveryItem> items)
    {
        File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(items));File.Move(PathName+".tmp",PathName,true);
    }
    internal void Complete() {File.Delete(PathName);watcher?.Dispose();}
    internal static void Watch(int parent,string path)
    {
        try {using var process=Process.GetProcessById(parent);process.WaitForExit();}catch(ArgumentException){}
        if(!File.Exists(path))return;
        try
        {
            var items=JsonSerializer.Deserialize<RecoveryItem[]>(File.ReadAllText(path))??[];
            bool allRestored=true;
            foreach(var item in items)
            {
                nint window=(nint)item.Window;
                Native.GetWindowThreadProcessId(window,out uint owner);
                if(!Native.IsWindow(window)||owner!=item.ProcessId)continue;
                var data=NativeTray.New(window,item.Id,item.Guid);data.Flags=8|(item.Guid==Guid.Empty?0u:0x20u);data.StateMask=1;data.State=item.OriginalState;
                bool restored=false;
                for(int attempt=0;attempt<20;attempt++)
                {
                    if(NativeTray.Notify(1,ref data)){restored=true;break;}
                    if(!Native.IsWindow(window)){restored=true;break;}
                    Thread.Sleep(250);
                }
                allRestored&=restored;
            }
            if(allRestored)File.Delete(path);
        }
        catch(Exception e) when(e is IOException or JsonException) { /* Keep recovery file for manual inspection. */ }
    }
}
