using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text.Json;

namespace NestedTray;

internal sealed class NativeMaterialException(string message):Exception(message);
internal sealed class NativeMaterialController:IDisposable
{
    private sealed class Payload
    {
        public int Protocol {get;set;}
        public string Dll {get;set;}="";
        public string Bootstrap {get;set;}="";
        public string DllSHA256 {get;set;}="";
        public string BootstrapSHA256 {get;set;}="";
    }
    private readonly SemaphoreSlim connection=new(1,1);
    private readonly CancellationTokenSource lifetime=new();
    private readonly object sync=new();
    private MemoryMappedFile? mapping;
    private MemoryMappedViewAccessor? view;
    private nint rootWindow;
    private bool disposed;
    private (int Mode,bool Light,bool Transparency)? lastPolicy;
    internal string Status {get;private set;}="未启用";
    internal bool Connected {get{lock(sync)return view?.ReadInt32(40)==1&&view.ReadInt32(44)>0;}}
    internal int LastError {get{lock(sync)return view?.ReadInt32(56)??0;}}
    internal object BackendState {get{lock(sync)return new{Connected=view?.ReadInt32(40),Targets=view?.ReadInt32(44),Applied=view?.ReadInt32(48),Error=view?.ReadInt32(56),OriginalAcrylic=view?.ReadInt32(92),OriginalSource=view?.ReadInt32(96),OriginalFallback=view?.ReadInt32(100),InstalledFallback=view?.ReadInt32(104)};}}
    internal async Task ConnectAsync(nint target,int explorerPid)
    {
        await connection.WaitAsync(lifetime.Token);
        try
        {
            if(disposed)throw new ObjectDisposedException(nameof(NativeMaterialController));
            if(rootWindow==target&&Connected)return;
            ClearConnection();
            string directory=Path.Combine(AppContext.BaseDirectory,"native");
            var payload=JsonSerializer.Deserialize<Payload>(await File.ReadAllTextAsync(Path.Combine(directory,"bridge.json"),lifetime.Token))??throw new NativeMaterialException("材质组件不完整，请重新解压完整安装包。");
            if(payload.Protocol!=1)throw new NativeMaterialException("材质组件版本不匹配，请更新完整安装包。");
            string dll=Verify(directory,payload.Dll,payload.DllSHA256),bootstrap=Verify(directory,payload.Bootstrap,payload.BootstrapSHA256);
            string cache=Path.Combine(Settings.DirectoryPath,"native-cache");Directory.CreateDirectory(cache);
            string cachedDll=Path.Combine(cache,payload.Dll);
            if(!File.Exists(cachedDll))
            {
                string temporary=cachedDll+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{File.Copy(dll,temporary);try{File.Move(temporary,cachedDll);}catch(IOException)when(File.Exists(cachedDll)){} }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            dll=Verify(cache,payload.Dll,payload.DllSHA256);
            string name="Local\\TrayNest.Material."+Environment.ProcessId+"."+Guid.NewGuid().ToString("N");
            lock(sync)
            {
                mapping=MemoryMappedFile.CreateNew(name,128);view=mapping.CreateViewAccessor();rootWindow=target;
                view.Write(0,0x544e4d31u);view.Write(4,1u);view.Write(8,(uint)Environment.ProcessId);view.Write(12,(uint)explorerPid);view.Write(16,target.ToInt64());
            }
            Status="正在连接主面板…";
            var start=new ProcessStartInfo(bootstrap){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true};
            start.ArgumentList.Add(explorerPid.ToString());start.ArgumentList.Add(name);start.ArgumentList.Add(dll);
            using var process=Process.Start(start)??throw new NativeMaterialException("无法启动材质组件。");
            var output=process.StandardOutput.ReadToEndAsync(lifetime.Token);var errors=process.StandardError.ReadToEndAsync(lifetime.Token);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);deadline.CancelAfter(10000);
            try{await process.WaitForExitAsync(deadline.Token);}
            catch(OperationCanceledException){if(!process.HasExited)process.Kill();throw new NativeMaterialException("连接主面板超时，已保留原来的外观。");}
            await output;await errors;
            var ready=Stopwatch.StartNew();while(process.ExitCode==0&&!Connected&&ready.ElapsedMilliseconds<1500)await Task.Delay(50,lifetime.Token);
            if(process.ExitCode!=0||!Connected)throw new NativeMaterialException("未能连接主面板。若正在使用任务栏美化工具，请检查是否存在冲突。");
            lock(sync)if(view?.ReadInt32(68)!=1)throw new NativeMaterialException("已加载的材质组件版本不匹配，当前保留系统外观。");
            Status="主面板已连接";
        }
        catch{ClearConnection();throw;}
        finally{connection.Release();}
    }
    private static string Verify(string directory,string name,string expected)
    {
        if(string.IsNullOrWhiteSpace(name)||Path.GetFileName(name)!=name||expected.Length!=64)throw new NativeMaterialException("材质组件清单无效。");
        string path=Path.Combine(directory,name);
        using var stream=File.OpenRead(path);string hash=Convert.ToHexString(SHA256.HashData(stream));
        if(!hash.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new NativeMaterialException("材质组件校验失败，请重新解压完整安装包。");
        return path;
    }
    internal int Apply(PanelAppearance appearance,bool enabled,bool light,bool transparency,bool highContrast)
    {
        int mode=!enabled||highContrast||appearance==PanelAppearance.FollowSystem?0:appearance==PanelAppearance.Light?1:appearance==PanelAppearance.Dark?2:3;
        var next=(mode,light,transparency);
        lock(sync)
        {
            if(view==null)return 0;
            if(lastPolicy==next)return view.ReadInt32(24);
            int sequence=view.ReadInt32(24);if((sequence&1)!=0)sequence++;
            view.Write(24,sequence+1);view.Write(28,mode);view.Write(32,light?1:0);view.Write(36,transparency?1:0);
            Thread.MemoryBarrier();view.Write(24,sequence+2);lastPolicy=next;
            Status=mode==0?"已恢复系统外观":"正在同步主面板…";return sequence+2;
        }
    }
    internal async Task WaitForAppliedAsync(int sequence)
    {
        var clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<2500)
        {
            await Task.Delay(50,lifetime.Token);
            lock(sync)
            {
                if(disposed||view==null)throw new ObjectDisposedException(nameof(NativeMaterialController));
                if(view.ReadInt32(52)==sequence)
                {
                    if(view.ReadInt32(56)!=0)throw new NativeMaterialException("系统未能应用该材质，已恢复原来的外观。");
                    if(view.ReadInt32(48)>0||view.ReadInt32(60)==1){Status=view.ReadInt32(60)==1?"已恢复系统外观":"两个面板的材质已同步";return;}
                }
            }
        }
        throw new NativeMaterialException("主面板暂时没有响应材质设置，已保留原来的外观。");
    }
    internal void Restore(){Apply(PanelAppearance.FollowSystem,false,true,false,false);}
    private void ClearConnection()
    {
        lock(sync){Restore();view?.Dispose();view=null;mapping?.Dispose();mapping=null;rootWindow=0;lastPolicy=null;}
    }
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();ClearConnection();}
}
