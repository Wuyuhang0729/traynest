using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NestedTray;
internal sealed class TrayBridge:IDisposable
{
    internal readonly Dictionary<string,IconRecord> Icons=new();
    internal event Action? Changed;
    internal event Action<string,Exception?>? Faulted;
    internal Func<IconRecord,Rectangle>? Bounds;
    internal HashSet<int>? AllowedOwners;
    internal readonly Dictionary<string,int> PacketLayouts=new();
    private readonly Dictionary<string,byte[]> testPackets=new();
    internal int HiddenStateReapplications;
    internal int UnrecognizedPacketCount;
    private readonly Settings settings;
    private readonly RecoveryJournal recovery;
    private readonly Native.Procedure procedure;
    private readonly nint module=Native.GetModuleHandleW(null);
    private nint explorer,receiver;
    private bool changingState,disposing,faulted;
    internal bool RestoredAll{get;private set;}=true;
    internal TrayBridge(Settings settings,RecoveryJournal recovery)
    {this.settings=settings;this.recovery=recovery;procedure=Receive;}
    internal void Start()
    {
        explorer=Native.FindWindowW("Shell_TrayWnd",null);
        Native.GetWindowThreadProcessId(explorer,out uint owner);
        if(explorer==0||!Process.GetProcessById((int)owner).ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("没有找到原生 Explorer 托盘，或已有其他托盘接管程序运行。请先退出其他托盘管理工具。");
        if(!Native.GetWindowRect(explorer,out var rect))throw new InvalidOperationException("无法读取系统任务栏位置。");
        var cls=new Native.WindowClass{Instance=module,ClassName="Shell_TrayWnd",Callback=procedure};
        if(Native.RegisterClassW(ref cls)==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        receiver=Native.CreateWindowExW(0x08000088,cls.ClassName,"NestedTray message receiver",0x80000000,rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top,0,0,module,0);
        if(receiver==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if(Native.FindWindowW("Shell_TrayWnd",null)!=receiver)throw new InvalidOperationException("托盘消息入口不可用，已停止启动。");
    }
    internal bool ExplorerAlive=>explorer!=0&&Native.IsWindow(explorer);
    internal nint ExplorerWindow=>explorer;
    internal void Maintain()
    {
        if(disposing||receiver==0)return;
        if(!ExplorerAlive){Fail("TN2003");return;}
        if(Native.FindWindowW("Shell_TrayWnd",null)!=receiver)
            Native.SetWindowPos(receiver,new nint(-1),0,0,0,0,0x213);
        long now=Environment.TickCount64;
        foreach(var record in Icons.Values.Where(value=>value.Selected&&value.RehideAfter<=now).ToArray())
        {
            record.RehideAfter=now+1000;
            Native.GetWindowThreadProcessId(record.Window,out uint owner);
            if(!Native.IsWindow(record.Window)||owner!=record.ProcessId)continue;
            try {SetState(record,1);HiddenStateReapplications++;}
            catch(InvalidOperationException error){Fail("TN2002",error);return;}
        }
    }
    private void Fail(string code,Exception? error=null) {if(!faulted){faulted=true;Faulted?.Invoke(code,error);}}
    private nint Forward(uint message,nuint wp,nint lp)
    {
        if(Native.SendMessageTimeoutW(explorer,message,wp,lp,0x22,750,out nuint result)==0)
        {if(!disposing)Fail("TN2001");return 0;}
        return unchecked((nint)result);
    }
    private nint ForwardBytes(nuint tag,byte[] bytes,nuint wp)
    {
        nint buffer=Marshal.AllocHGlobal(bytes.Length),header=Marshal.AllocHGlobal(Marshal.SizeOf<Native.CopyData>());
        try {Marshal.Copy(bytes,0,buffer,bytes.Length);Marshal.StructureToPtr(new Native.CopyData{Tag=tag,Size=(uint)bytes.Length,Bytes=buffer},header,false);return Forward(0x4A,wp,header);}
        finally {Marshal.FreeHGlobal(header);Marshal.FreeHGlobal(buffer);}
    }
    private nint Receive(nint hwnd,uint message,nuint wp,nint lp)
    {
        try
        {
            if(message==0x4A&&lp!=0)
            {
                var copy=Marshal.PtrToStructure<Native.CopyData>(lp);
                if(copy.Tag==1)
                {
                    string shape=$"tag=1,length={copy.Size},inner={(copy.Size>=12&&copy.Bytes!=0?Marshal.ReadInt32(copy.Bytes,8):0)}";
                    if(PacketLayouts.ContainsKey(shape)||PacketLayouts.Count<12)PacketLayouts[shape]=PacketLayouts.GetValueOrDefault(shape)+1;
                    if(copy.Size!=1484){UnrecognizedPacketCount++;AppDiagnostics.Warn("TN1101",shape);}
                }
                if(copy.Tag==3&&copy.Size==40&&copy.Bytes!=0)
                {
                    byte[] query=new byte[40];Marshal.Copy(copy.Bytes,query,0,40);
                    uint op=BitConverter.ToUInt32(query,4);
                    if(BitConverter.ToUInt32(query,0)==0x34753423&&op is 1 or 2)
                    {
                        nint source=unchecked((nint)(int)BitConverter.ToUInt32(query,16));uint id=BitConverter.ToUInt32(query,20);Guid guid=new(query.AsSpan(24,16));
                        if(Icons.TryGetValue(IconRecord.Identity(source,id,guid),out var selected)&&selected.Selected)
                        {
                            Rectangle rect=Bounds?.Invoke(selected)??Rectangle.Empty;
                            if(!rect.IsEmpty){int x=op==1?rect.Left:rect.Width,y=op==1?rect.Top:rect.Height;return unchecked((nint)(uint)(((y&0xFFFF)<<16)|(x&0xFFFF)));}
                        }
                    }
                }
                if(copy.Tag==1&&copy.Size==1484&&copy.Bytes!=0)
                {
                    byte[] bytes=new byte[copy.Size];Marshal.Copy(copy.Bytes,bytes,0,bytes.Length);
                    var packet=TrayPacket.Parse((ulong)copy.Tag,bytes);
                    if(packet==null){UnrecognizedPacketCount++;AppDiagnostics.Warn("TN1101","unrecognized-layout");}
                    if(packet!=null)
                    {
                        Native.GetWindowThreadProcessId(packet.Window,out uint process);
                        if(process==Environment.ProcessId)return Forward(message,wp,lp);
                        if(AllowedOwners!=null&&!AllowedOwners.Contains((int)process))return Forward(message,wp,lp);
                        string key=IconRecord.Identity(packet.Window,packet.Id,packet.Guid);
                        if(AllowedOwners!=null&&packet.Operation is 0 or 1)testPackets[key]=(byte[])bytes.Clone();
                        if(packet.Operation==2)
                        {
                            nint result=Forward(message,wp,lp);
                            if(Icons.Remove(key,out var deleted)){deleted.Dispose();SaveRecovery();Changed?.Invoke();}
                            return result;
                        }
                        if(packet.Operation is 0 or 1)
                        {
                            if(!Icons.TryGetValue(key,out var record))
                            {
                                // An update may arrive before registration; keep it until callback data arrives.
                                string exe=System.Text.Encoding.Unicode.GetString(bytes,964,520).Split('\0')[0];
                                if(string.IsNullOrWhiteSpace(exe)){try {exe=Process.GetProcessById((int)process).MainModule?.FileName??"";}catch{exe="";}}
                                string rule=exe.ToLowerInvariant()+"|"+(packet.Guid!=Guid.Empty?"g:"+packet.Guid.ToString("D"):"i:"+packet.Id);
                                record=new IconRecord{Key=key,Rule=rule,Executable=exe,Window=packet.Window,Id=packet.Id,Guid=packet.Guid,ProcessId=(int)process};
                                Icons.Add(key,record);
                            }
                            if(packet.Operation==0){record.RegistrationKnown=true;record.Window=packet.Window;record.Id=packet.Id;record.ProcessId=(int)process;}
                            if((packet.Flags&1)!=0)record.Callback=packet.Callback;
                            if((packet.Flags&4)!=0)record.Tooltip=packet.Tip;
                            if((packet.Flags&2)!=0)record.UpdateImage(packet.Image);
                            uint previousState=record.OriginalState;
                            if(!changingState&&(packet.Flags&8)!=0&&(BitConverter.ToUInt32(bytes,292)&1)!=0)
                                record.OriginalState=BitConverter.ToUInt32(bytes,288)&1;
                            bool select=settings.Collected.Contains(record.Rule)&&record.CanCollect;
                            bool changed=record.Selected!=select;record.Selected=select;
                            if(changed||record.Selected&&(packet.Operation==0||previousState!=record.OriginalState))SaveRecovery();
                            if(record.Selected)
                            {
                                BitConverter.GetBytes(packet.Flags|8).CopyTo(bytes,20);
                                BitConverter.GetBytes(BitConverter.ToUInt32(bytes,288)|1).CopyTo(bytes,288);
                                BitConverter.GetBytes(BitConverter.ToUInt32(bytes,292)|1).CopyTo(bytes,292);
                                nint result=ForwardBytes(copy.Tag,bytes,wp);Changed?.Invoke();return result;
                            }
                            Changed?.Invoke();
                        }
                        else if(packet.Operation==4&&Icons.TryGetValue(key,out var record))record.Version=packet.Version;
                    }
                }
                return Forward(message,wp,lp);
            }
            if(message>=0x400||message is 0x111 or 0x7F)return Forward(message,wp,lp);
            return Native.DefWindowProcW(hwnd,message,wp,lp);
        }
        catch(Exception error)
        {
            Fail("TN2004",error);
            return message==0x4A?Forward(message,wp,lp):Native.DefWindowProcW(hwnd,message,wp,lp);
        }
    }
    private void SaveRecovery()=>recovery.Write(Icons.Values.Where(value=>value.Selected).Select(value=>value.Recovery()));
    internal Rectangle ReadOriginalBounds(IconRecord record)
    {
        var saved=Bounds;Bounds=null;
        try{return Native.GetIconRect(record.Window,record.Id,record.Guid);}
        finally{Bounds=saved;}
    }
    internal void Collect(IconRecord record)
    {
        if(!record.CanCollect)throw new TrayNestException("TN2101");
        bool added=!settings.Collected.Contains(record.Rule),attempted=false;
        if(added)settings.Collected.Add(record.Rule);
        try
        {
            record.Selected=true;record.RehideAfter=Environment.TickCount64+450;SaveRecovery();
            attempted=true;SetState(record,1);settings.Save();Changed?.Invoke();
        }
        catch
        {
            if(added)settings.Collected.Remove(record.Rule);
            record.Selected=false;
            try{if(attempted)SetState(record,record.OriginalState);SaveRecovery();}
            catch(Exception undo){record.Selected=true;AppDiagnostics.Error("TN6001",undo);Fail("TN6001",undo);}
            Changed?.Invoke();throw;
        }
    }
    internal void ResetVisibleForOwnTest(IconRecord record)
    {
        if(AllowedOwners?.Contains(record.ProcessId)!=true||!testPackets.TryGetValue(record.Key,out var original))throw new InvalidOperationException("Only the fixture may reset visibility in this test.");
        var bytes=(byte[])original.Clone();
        BitConverter.GetBytes(1u).CopyTo(bytes,4);
        BitConverter.GetBytes(8u|(record.Guid==Guid.Empty?0u:0x20u)).CopyTo(bytes,20);
        BitConverter.GetBytes(0u).CopyTo(bytes,288);BitConverter.GetBytes(1u).CopyTo(bytes,292);
        ForwardBytes(1,bytes,(nuint)record.Window);
    }
    internal void Restore(IconRecord record,bool forget=true)
    {
        var previousRules=settings.Collected.ToList();bool previousSelected=record.Selected;
        try
        {
            if(forget)settings.Collected.Remove(record.Rule);
            record.Selected=false;SetState(record,record.OriginalState);
            if(forget)settings.Save();SaveRecovery();
        }
        catch
        {
            settings.Collected=previousRules;record.Selected=previousSelected;
            try{SetState(record,previousSelected?1:record.OriginalState);SaveRecovery();}
            catch(Exception undo){AppDiagnostics.Error("TN6001",undo);Fail("TN6001",undo);}
            Changed?.Invoke();throw;
        }
        if(forget)foreach(var sibling in Icons.Values.Where(value=>value.Selected&&value.Rule==record.Rule&&value!=record).ToArray())Restore(sibling,false);
        Changed?.Invoke();
    }
    private void SetState(IconRecord record,uint state)
    {
        var data=NativeTray.New(record.Window,record.Id,record.Guid);data.Flags=8|(record.Guid==Guid.Empty?0u:0x20u);data.StateMask=1;data.State=state;
        changingState=true;
        try {if(!NativeTray.Notify(1,ref data)&&Native.IsWindow(record.Window))throw new InvalidOperationException("系统未接受图标状态变更。");}
        finally {changingState=false;}
    }
    internal void ForwardInput(IconRecord record,uint code,Rectangle rect)
    {
        if(!Native.IsWindow(record.Window)||record.Callback==0)throw new TrayNestException("TN3001");
        int x=rect.Left+rect.Width/2,y=rect.Top+rect.Height/2;
        if(record.Version>=4)
        {
            uint anchor=unchecked((uint)(((y&0xFFFF)<<16)|(x&0xFFFF)));
            uint[] events=code==0x400?[0x201u,0x202u,0x400u]:code==0x203?[0x203u,0x202u]:[code];
            foreach(uint value in events)if(!NativeTray.PostMessage(record.Window,record.Callback,anchor,(nint)((record.Id<<16)|value)))throw new TrayNestException("TN3001",new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }
        else
        {
            uint[] events=code==0x7B?[0x204u,0x205u]:code==0x400?[0x201u,0x202u]:[0x203u,0x202u];
            foreach(uint value in events)if(!NativeTray.PostMessage(record.Window,record.Callback,record.Id,(nint)value))throw new TrayNestException("TN3001",new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }
    }
    internal void RefreshExisting()
    {NativeTray.PostMessage(new nint(0xFFFF),Native.RegisterWindowMessageW("TaskbarCreated"),0,0);}
    internal void PruneClosed()
    {
        bool changed=false;
        foreach(var item in Icons.Values.Where(value=>!Native.IsWindow(value.Window)).ToArray())
        {Icons.Remove(item.Key);item.Dispose();changed=true;}
        if(changed){SaveRecovery();Changed?.Invoke();}
    }
    public void Dispose()
    {
        if(disposing)return;disposing=true;
        try
        {
            // Remove only our receiver first; restoration then goes directly to real Explorer.
            if(receiver!=0){Native.DestroyWindow(receiver);receiver=0;}
            Native.UnregisterClassW("Shell_TrayWnd",module);
            foreach(var record in Icons.Values.Where(value=>value.Selected).ToArray())
                try {record.Selected=false;SetState(record,record.OriginalState);}catch(Exception error){RestoredAll=false;AppDiagnostics.Error("TN6001",error); /* Watchdog keeps the journal. */ }
        }
        finally {foreach(var record in Icons.Values)record.Dispose();Icons.Clear();GC.KeepAlive(procedure);}
    }
}
