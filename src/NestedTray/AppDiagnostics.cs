using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NestedTray;

internal static class ErrorCodes
{
    internal static string Message(string code)=>code switch
    {
        "TN0001"=>"TrayNest 已启动。",
        "TN0002"=>"TrayNest 已退出。",
        "TN0003"=>"异常退出后的图标恢复已完成。",
        "TN0010"=>"开机启动设置已写入并核验。",
        "TN0011"=>"系统任务栏已就绪，正在恢复收纳。",
        "TN0012"=>"启动后的收纳状态检查已完成。",
        "TN1004"=>"等待 Windows 任务栏就绪超时，请在任务栏出现后重试并附诊断报告。",
        "TN4002"=>"登录启动任务无法保存或核验，原有启动设置已尽量保留，请附诊断报告。",
        "TN1001"=>"当前环境不受支持，或 Windows 托盘尚未就绪。请使用 Windows 11 x64，并在任务栏出现后重试。",
        "TN1002"=>"收纳配置无法读取。原配置已保留，请导出诊断报告后反馈。",
        "TN4101"=>"无法启动保存的工具。请检查启动文件或从开始菜单打开。",
        "TN1003"=>"程序启动失败，请导出诊断报告后反馈。",
        "TN1101"=>"检测到未识别的托盘消息格式，对应消息已交给 Windows 原样处理。",
        "TN1102"=>"系统拖放接口不可用，部分拖动功能可能受限。",
        "TN2001"=>"Windows 托盘暂时无响应，已开始停止收纳并恢复图标。",
        "TN2010"=>"Windows 托盘暂时无响应，TrayNest 正在自动等待恢复，收纳配置和顺序已保留。",
        "TN2011"=>"Windows 托盘已恢复响应，正在同步图标和收纳状态。",
        "TN2002"=>"无法保持原图标隐藏，已开始停止收纳并恢复图标。",
        "TN2003"=>"检测到资源管理器重启，正在恢复图标并重新连接。",
        "TN2004"=>"托盘消息处理失败，已开始停止收纳并恢复图标。",
        "TN2005"=>"资源管理器在短时间内反复重启，已暂停自动重连。请检查系统托盘后手动重新打开 TrayNest。",
        "TN2101"=>"暂未取得这个图标的交互信息。请刷新图标或重启对应软件后重试。",
        "TN3001"=>"无法向原软件发送操作。请检查软件是否仍在运行，或是否以管理员权限运行。",
        "TN3002"=>"图标收纳或移回失败，请查看诊断报告。",
        "TN4001"=>"无法保存收纳配置或开机启动设置，请检查目录及账户权限。",
        "TN5001"=>"诊断报告无法保存，请选择其他可写入的目录。",
        "TN5002"=>"无法复制报告或打开反馈页面，请手动访问项目的 Issues 页面。",
        "TN6001"=>"异常退出后的图标恢复未全部完成，恢复清单已保留。",
        "TN6002"=>"图标恢复保护无法运行，请导出诊断报告。",
        "TN6003"=>"上次运行留下了未完成的恢复清单，保护进程正在检查并恢复图标。",
        "TN9001"=>"程序遇到未处理错误，已开始退出并尝试恢复图标。",
        "TN9002"=>"后台线程遇到致命错误，恢复保护进程将尝试恢复图标。",
        _=>"TrayNest 运行事件"
    };
}

internal sealed record ExceptionDetail(string Type,string HResult,int? NativeError,string[] Methods);
internal sealed record DiagnosticEvent(DateTimeOffset Utc,string Code,string EventId,string Severity,string Message,ExceptionDetail[] Exceptions,Dictionary<string,string> Context);

internal sealed class DiagnosticStore
{
    private readonly object sync=new();
    private readonly Queue<DiagnosticEvent> recent=new();
    private readonly string directory;
    private readonly int maxBytes,rotationCount,memoryLimit;
    internal bool LogAvailable{get;private set;}
    private DiagnosticEvent? lastProblem;
    internal DiagnosticEvent? LastProblem=>lastProblem??ReadRecent().LastOrDefault(value=>value.Severity is "Error" or "Warning");
    internal string LogDirectory=>directory;
    internal static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    internal DiagnosticStore(string root,int maxBytes=256*1024,int rotationCount=3,int memoryLimit=128)
    {directory=Path.Combine(root,"logs");this.maxBytes=maxBytes;this.rotationCount=rotationCount;this.memoryLimit=memoryLimit;}

    internal DiagnosticEvent Record(string code,string severity="Info",Exception? error=null,Dictionary<string,string>? context=null)
    {
        var details=new List<ExceptionDetail>();
        for(var current=error;current!=null&&details.Count<5;current=current.InnerException)
        {
            string[] methods=new StackTrace(current,false).GetFrames().Take(24).Select(frame=>frame.GetMethod()).Where(method=>method!=null)
                .Select(method=>Limit((method!.DeclaringType?.FullName??"")+"."+method.Name,240)).ToArray();
            details.Add(new(current.GetType().FullName??current.GetType().Name,$"0x{current.HResult:X8}",current is Win32Exception win32?win32.NativeErrorCode:null,methods));
        }
        // Exception messages, arguments, file names and line locations are deliberately
        // excluded: third-party exceptions can embed paths, document names or tokens.
        var entry=new DiagnosticEvent(DateTimeOffset.UtcNow,code,Guid.NewGuid().ToString("N")[..12],severity,ErrorCodes.Message(code),details.ToArray(),
            SafeContext(context??new()));
        lock(sync)
        {
            recent.Enqueue(entry);while(recent.Count>memoryLimit)recent.Dequeue();
            if(severity is "Error" or "Warning")lastProblem=entry;
            Write(entry);
        }
        return entry;
    }

    private void Write(DiagnosticEvent entry)
    {
        bool owned=false;
        try
        {
            string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())))[..20];
            using var mutex=new Mutex(false,"Local\\TrayNestLog-"+key);
            try{owned=mutex.WaitOne(200);}catch(AbandonedMutexException){owned=true;}
            if(!owned){LogAvailable=false;return;}
            try
            {
                Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"events.jsonl");
                string line=JsonSerializer.Serialize(entry)+"\n";
                if(File.Exists(path)&&new FileInfo(path).Length+Encoding.UTF8.GetByteCount(line)>maxBytes)
                    for(int index=rotationCount;index>=1;index--)
                    {
                        string previous=index==1?path:Path.Combine(directory,$"events.{index-1}.jsonl");
                        if(File.Exists(previous))File.Move(previous,Path.Combine(directory,$"events.{index}.jsonl"),true);
                    }
                File.AppendAllText(path,line,new UTF8Encoding(false));LogAvailable=true;
            }
            finally{mutex.ReleaseMutex();}
        }
        catch(Exception failure)when(failure is IOException or UnauthorizedAccessException or System.Security.SecurityException or WaitHandleCannotBeOpenedException)
        {LogAvailable=false;}
    }

    internal DiagnosticEvent[] ReadRecent()
    {
        lock(sync)
        {
            var found=new Dictionary<string,DiagnosticEvent>();
            try
            {
                for(int index=rotationCount;index>=0;index--)
                {
                    string path=Path.Combine(directory,index==0?"events.jsonl":$"events.{index}.jsonl");
                    if(!File.Exists(path))continue;
                    using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                    if(stream.Length>maxBytes+32*1024)continue;
                    using var reader=new StreamReader(stream);
                    while(reader.ReadLine() is string line)
                    {
                        if(line.Length>32*1024)continue;
                        try{var value=JsonSerializer.Deserialize<DiagnosticEvent>(line);if(value!=null&&ValidPersisted(value))found[value.EventId]=value;}catch(JsonException){}
                    }
                }
            }
            catch(Exception failure)when(failure is IOException or UnauthorizedAccessException){}
            foreach(var value in recent)found[value.EventId]=value;
            return found.Values.OrderBy(value=>value.Utc).TakeLast(memoryLimit).ToArray();
        }
    }

    private static bool ValidPersisted(DiagnosticEvent entry)=>entry.EventId is {Length:12} id&&id.All(char.IsAsciiHexDigit)
        &&entry.Code is {Length:6} code&&code.StartsWith("TN",StringComparison.Ordinal)&&code.AsSpan(2).ToString().All(char.IsAsciiDigit)
        &&entry.Severity is "Info" or "Warning" or "Error";

    internal string Report(Dictionary<string,object?>? state=null,DiagnosticEvent? problem=null)
    {
        var events=ReadRecent();
        // Rebuild persisted events through a whitelist rather than exporting raw log lines.
        var clean=events.Select(value=>new {value.Utc,value.Code,value.EventId,value.Severity,Message=ErrorCodes.Message(value.Code),
            Exceptions=value.Exceptions?.Take(5).Select(error=>new{Type=SafeValue(error.Type),HResult=SafeValue(error.HResult),error.NativeError,Methods=error.Methods?.Take(24).Select(SafeValue).ToArray()}),
            Context=SafeContext(value.Context??new())}).ToArray();
        return JsonSerializer.Serialize(new
        {
            SchemaVersion=1,GeneratedUtc=DateTimeOffset.UtcNow,ReportId=Guid.NewGuid().ToString("N")[..12],
            Application=new{Product="TrayNest",Version=typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"unknown",Runtime=RuntimeInformation.FrameworkDescription,
                ProcessArchitecture=RuntimeInformation.ProcessArchitecture.ToString(),OsArchitecture=RuntimeInformation.OSArchitecture.ToString(),SelfContained=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetDirectoryName(typeof(object).Assembly.Location),StringComparison.OrdinalIgnoreCase)},
            Windows=new{Version=Environment.OSVersion.Version.ToString(),Is64Bit=Environment.Is64BitOperatingSystem,RemoteSession=SystemInformation.TerminalServerSession},
            Displays=Screen.AllScreens.Select((screen,index)=>new{Index=index,screen.Primary,Width=screen.Bounds.Width,Height=screen.Bounds.Height,WorkWidth=screen.WorkingArea.Width,WorkHeight=screen.WorkingArea.Height}).ToArray(),
            Problem=problem==null?null:new {problem.Code,problem.EventId},LogAvailable,
            State=CleanState(state??new()),Events=clean,
            Privacy="不包含用户名、完整路径、图标提示、窗口标题、应用清单、配置内容或截图。请检查报告内容后再提交。"
        },JsonOptions);
    }

    private static Dictionary<string,object?> CleanState(Dictionary<string,object?> state)=>state.Take(40).ToDictionary(pair=>SafeValue(pair.Key),pair=>pair.Value switch
    {null=>null,bool or int or uint or long or double=>pair.Value,string text=>SafeValue(text),_=>"[unsupported value]"});

    private static Dictionary<string,string> SafeContext(IEnumerable<KeyValuePair<string,string>> fields)
    {
        var result=new Dictionary<string,string>();
        foreach(var pair in fields.Take(16))result[SafeValue(pair.Key)]=SafeValue(pair.Value);
        return result;
    }

    internal void Export(string destination,Dictionary<string,object?>? state=null,DiagnosticEvent? problem=null)
    {
        string report=Report(state,problem);
        string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,report,new UTF8Encoding(false));File.Move(temporary,destination,true);}
        finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }

    internal static string SafeValue(string? value)
    {
        string text=Limit(value??"",512);
        // Context is application-generated metadata. Treat any unexpected path or
        // credential-like value as private, including values read back from disk.
        if(text.Contains(":\\",StringComparison.Ordinal)||text.Contains(":/",StringComparison.Ordinal)||text.Contains("\\\\",StringComparison.Ordinal)||text.Contains("/Users/",StringComparison.OrdinalIgnoreCase)||text.Contains("/home/",StringComparison.OrdinalIgnoreCase)
            ||text.Contains("Bearer ",StringComparison.OrdinalIgnoreCase)||text.Contains("ghp_",StringComparison.Ordinal)||text.Contains("github_pat_",StringComparison.Ordinal)
            ||text.Contains("password=",StringComparison.OrdinalIgnoreCase)||text.Contains("token=",StringComparison.OrdinalIgnoreCase))return "[private value removed]";
        foreach(string privateText in new[]{Environment.UserName,Environment.MachineName})
            if(privateText.Length>=2)text=text.Replace(privateText,"[private]",StringComparison.OrdinalIgnoreCase);
            else if(text.Equals(privateText,StringComparison.OrdinalIgnoreCase))text="[private]";
        return text;
    }
    private static string Limit(string value,int length)=>value.Length>length?value[..length]:value;
}

internal static class AppDiagnostics
{
    private static DiagnosticStore? store;
    private static string? root;
    private static readonly Dictionary<string,long> warnings=new();
    internal static Func<Dictionary<string,object?>>? State;
    internal static DiagnosticStore Store
    {
        get
        {
            string profile=Settings.DirectoryPath;
            if(store==null||root!=profile){root=profile;store=new DiagnosticStore(profile);warnings.Clear();}
            return store;
        }
    }
    internal static DiagnosticEvent Error(string code,Exception? error=null)=>Store.Record(code,"Error",error);
    internal static void Info(string code,Dictionary<string,string>? context=null)=>Store.Record(code,"Info",context:context);
    internal static DiagnosticEvent? Warn(string code,string key="",Exception? error=null)
    {
        string identity=code+":"+key;long now=Environment.TickCount64;
        if(warnings.TryGetValue(identity,out long previous)&&now-previous<30000)return null;
        if(warnings.Count>128)warnings.Clear();warnings[identity]=now;
        return Store.Record(code,"Warning",error);
    }
    internal static Dictionary<string,object?> Snapshot()
    {try{return State?.Invoke()??new();}catch(Exception error){return new(){["SnapshotAvailable"]=false,["SnapshotError"]=error.GetType().Name};}}
}

internal sealed class TrayNestException(string code,Exception? inner=null):Exception(ErrorCodes.Message(code),inner)
{internal string Code{get;}=code;}

internal static class RestartThrottle
{
    internal static bool TryConsume(string root,DateTimeOffset now)
    {
        try
        {
            string path=Path.Combine(root,"restart-history.json");
            var previous=File.Exists(path)?JsonSerializer.Deserialize<DateTimeOffset[]>(File.ReadAllText(path))??[]:[];
            var recent=previous.Where(value=>value>=now.AddMinutes(-5)&&value<=now.AddMinutes(1)).ToList();
            if(recent.Count>=3)return false;
            recent.Add(now);Directory.CreateDirectory(root);
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(recent));File.Move(path+".tmp",path,true);return true;
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or JsonException)
        {AppDiagnostics.Error("TN2005",error);return false;}
    }
}
