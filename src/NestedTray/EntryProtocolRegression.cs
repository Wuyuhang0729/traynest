using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;

// Runs on an undisplayed private desktop. The real Shell_NotifyIconW transport
// reaches our Shell_TrayWnd fixture, never the user's Explorer or tray receiver.
internal static class EntryProtocolRegression
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern nint CreateDesktopW(string name,nint device,nint mode,uint flags,uint access,nint security);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(nint desktop);
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [ThreadStatic]private static nint testDesktop;
    internal static bool OnPrivateDesktop=>testDesktop!=0&&GetThreadDesktop(GetCurrentThreadId())==testDesktop;

    internal static int Run(string directory)
    {
        directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",directory);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var cases=new Dictionary<string,object>();nint privateDesktop=0;
        nint parentDesktop=GetThreadDesktop(GetCurrentThreadId());
        var worker=new Thread(()=>
        {
            try
            {
                privateDesktop=CreateDesktopW("TrayNestProtocol-"+Guid.NewGuid().ToString("N"),0,0,0,0x01FF,0);
                if(privateDesktop==0||!SetThreadDesktop(privateDesktop))throw new Win32Exception(Marshal.GetLastWin32Error());
                testDesktop=privateDesktop;
                checks["WorkerOnPrivateDesktop"]=GetThreadDesktop(GetCurrentThreadId())==privateDesktop&&privateDesktop!=parentDesktop;
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
                checks["NativeDataSize976"]=Marshal.SizeOf<NativeTray.Data>()==976;
                checks["NativeDataOffsets"]=Marshal.OffsetOf<NativeTray.Data>(nameof(NativeTray.Data.Window))==8
                    &&Marshal.OffsetOf<NativeTray.Data>(nameof(NativeTray.Data.Icon))==32
                    &&Marshal.OffsetOf<NativeTray.Data>(nameof(NativeTray.Data.Info))==304
                    &&Marshal.OffsetOf<NativeTray.Data>(nameof(NativeTray.Data.Version))==816
                    &&Marshal.OffsetOf<NativeTray.Data>(nameof(NativeTray.Data.Guid))==952;
                using var shell=new ShellFixture();
                checks["TransportTargetIsFixture"]=Native.FindWindowW("Shell_TrayWnd",null)==shell.Handle;

                shell.Reset(rejectAdd:2);
                var elapsed=System.Diagnostics.Stopwatch.StartNew();
                using(var entry=new EntryIcon(registrationTimeout:1600))
                {
                    checks["RealNotifyRetriesWhileShellWindowAlreadyExists"]=shell.Adds==3&&shell.Registered&&elapsed.ElapsedMilliseconds>=400;
                    checks["VersionConfiguredAfterActualAdd"]=shell.Operations.SequenceEqual(new uint[]{0,0,0,4});
                }
                checks["SuccessfulEntryDisposedFromFixture"]=!shell.Registered;
                cases["RejectedAddThenReady"]=shell.Operations.ToArray();

                shell.Reset(rejectVersion:1);
                using(var entry=new EntryIcon(registrationTimeout:1200))
                    checks["RealVersionRejectionRollsBackBeforeRetry"]=shell.Registered&&shell.Operations.SequenceEqual(new uint[]{0,4,2,0,4});
                cases["VersionRejectedOnce"]=shell.Operations.ToArray();

                shell.Reset(rejectAdd:int.MaxValue);bool threw=false;elapsed.Restart();
                try{using var entry=new EntryIcon(registrationTimeout:400);}
                catch(InvalidOperationException){threw=true;}
                checks["RealPersistentRejectionIsBounded"]=threw&&elapsed.ElapsedMilliseconds>=400&&elapsed.ElapsedMilliseconds<4000;
                checks["FailedEntryWindowDestroyed"]=shell.LastEntry!=0&&!Native.IsWindow(shell.LastEntry);
                checks["FailureLeavesNoFixtureRegistration"]=!shell.Registered;
                cases["PersistentRejection"]=shell.Operations.ToArray();

                shell.Reset();
                using var duplicateWindow=new Control();nint callback=duplicateWindow.Handle;
                using var icon=AppIcon.Load();
                Guid identity=Guid.NewGuid();var data=NativeTray.New(callback,701,identity);
                data.Flags=1|2|4|0x20;data.CallbackMessage=0x8000+41;data.Icon=icon.Handle;
                bool first=NativeTray.Notify(0,ref data),second=NativeTray.Notify(0,ref data);
                checks["RealDuplicateIdentityCanBeDistinguished"]=first&&!second&&shell.Registered;
                NativeTray.Notify(2,ref data);
                cases["DuplicateIdentity"]=shell.Operations.ToArray();

                shell.Reset();
                var settings=new Settings();var journal=new RecoveryJournal();
                var rule=ExecutableIdentity.Normalize(Environment.ProcessPath!).ToLowerInvariant()+"|g:"+identity.ToString("D");
                settings.Collected.Add(rule);
                using(var bridge=new TrayBridge(settings,journal){AllowedOwners=new(){Environment.ProcessId}})
                {
                    bridge.StartForPrivateFixture(shell.Handle);string? fault=null;bridge.Faulted+=(code,_)=>fault=code;
                    checks["BridgeInterceptsActualPrivateRegistration"]=NativeTray.Notify(0,ref data)&&bridge.Icons.Count==1&&bridge.Icons.Values.Single().Selected;
                    var source=bridge.Icons.Values.Single();
                    shell.DelayModify(1800);source.RehideAfter=0;bridge.Maintain();
                    checks["ActualStateTransportTimeoutDoesNotFault"]=bridge.Reconnecting&&fault==null;
                    var timeoutTrace=LastStateFailure();
                    checks["TimeoutTraceShowsActualReceiverAndTransport"]=timeoutTrace?.GetValueOrDefault("ReceivedPackets")=="1"
                        &&timeoutTrace.GetValueOrDefault("Forwarding")=="replies=0,zero=0,timeouts=1,suppressed=0"&&timeoutTrace.GetValueOrDefault("Pending")=="True";
                    checks["TimedOutSourceWindowStillAlive"]=Native.IsWindow(callback);
                    if(!shell.DelayFinished.Wait(4000))throw new TimeoutException("Fixture delay did not finish");
                    var recovery=System.Diagnostics.Stopwatch.StartNew();
                    while(bridge.Reconnecting&&recovery.ElapsedMilliseconds<4000){Thread.Sleep(50);bridge.Maintain();}
                    checks["ActualStateTransportRecovers"]=!bridge.Reconnecting&&fault==null;
                    shell.RejectModifyOnce=true;source.RehideAfter=0;bridge.Maintain();
                    checks["ActualZeroReplyIsDistinctFromTimeout"]=fault=="TN2002"&&!bridge.Reconnecting&&bridge.ExplorerAlive&&Native.IsWindow(callback)&&shell.Registered;
                    var rejectTrace=LastStateFailure();
                    checks["RejectionTraceShowsZeroReplyWithoutTimeout"]=rejectTrace?.GetValueOrDefault("ReceivedPackets")=="1"
                        &&rejectTrace.GetValueOrDefault("Forwarding")=="replies=1,zero=1,timeouts=0,suppressed=0"&&rejectTrace.GetValueOrDefault("Pending")=="False";
                    cases["StateTimeoutThenExplicitReject"]=shell.Operations.ToArray();
                }
                journal.Complete();

                shell.Reset();
                var secondSource=data;secondSource.Id=702;secondSource.Guid=Guid.NewGuid();
                var lifecycleSettings=new Settings();lifecycleSettings.Collected.Add(rule);
                lifecycleSettings.Collected.Add(ExecutableIdentity.Normalize(Environment.ProcessPath!).ToLowerInvariant()+"|g:"+secondSource.Guid.ToString("D"));
                using(var bridge=new TrayBridge(lifecycleSettings,journal){AllowedOwners=new(){Environment.ProcessId}})
                {
                    bridge.StartForPrivateFixture(shell.Handle);string? fault=null;bridge.Faulted+=(code,_)=>fault=code;
                    if(!NativeTray.Notify(0,ref data)||!NativeTray.Notify(0,ref secondSource))throw new InvalidOperationException("Own lifecycle sources did not register");
                    using var deletion=new ReentrantDelete(secondSource,privateDesktop);shell.OnNextModify=deletion.Send;
                    foreach(var record in bridge.Icons.Values)record.RehideAfter=0;
                    bridge.Maintain();
                    checks["DeleteDuringAnotherStateWriteDoesNotFault"]=deletion.Deleted&&fault==null&&!bridge.Reconnecting;
                    checks["DeletedSourceIsNotRecreatedFromMaintenanceSnapshot"]=bridge.Icons.Count==1&&!bridge.Icons.ContainsKey(IconRecord.Identity(secondSource.Window,secondSource.Id,secondSource.Guid));
                    cases["ReentrantDeleteOfNextSource"]=new{Operations=shell.Operations.ToArray(),deletion.Deleted,Fault=fault,Pending=bridge.Reconnecting,Records=bridge.Icons.Count};
                    checks["ConcurrentDeletePreservesRemainingRecoveryItem"]=RecoveryCount(journal)==1;
                    checks["ConcurrentDeleteDoesNotCountRetiredReapplication"]=bridge.HiddenStateReapplications==1;
                    checks["ConcurrentDeletePreservesSavedRules"]=lifecycleSettings.Collected.Count==2;
                    checks["ConcurrentDeleteCanReregisterAndRecollect"]=NativeTray.Notify(0,ref secondSource)
                        &&bridge.Icons.TryGetValue(IconRecord.Identity(secondSource.Window,secondSource.Id,secondSource.Guid),out var readded)&&readded.Selected;
                }
                journal.Complete();

                shell.Reset();
                using(var bridge=new TrayBridge(settings,journal){AllowedOwners=new(){Environment.ProcessId}})
                {
                    bridge.StartForPrivateFixture(shell.Handle);string? fault=null;bridge.Faulted+=(code,_)=>fault=code;
                    if(!NativeTray.Notify(0,ref data))throw new InvalidOperationException("Own source did not register");
                    using var deletion=new ReentrantDelete(data,privateDesktop);shell.OnNextModify=deletion.Send;
                    bridge.Icons.Values.Single().RehideAfter=0;bridge.Maintain();
                    checks["DeleteDuringOwnStateWriteDoesNotFault"]=deletion.Deleted&&fault==null&&!bridge.Reconnecting&&bridge.Icons.Count==0;
                    checks["ConcurrentCurrentDeleteClearsOnlyRuntimeRecovery"]=RecoveryCount(journal)==0&&settings.Collected.Contains(rule);
                    checks["CurrentDeleteDoesNotCountRetiredReapplication"]=bridge.HiddenStateReapplications==0;
                    cases["ReentrantDeleteOfCurrentSource"]=new{Operations=shell.Operations.ToArray(),deletion.Deleted,Fault=fault,Pending=bridge.Reconnecting,Records=bridge.Icons.Count};
                }
                journal.Complete();

                shell.Reset();
                using(var bridge=new TrayBridge(settings,journal){AllowedOwners=new(){Environment.ProcessId}})
                {
                    bridge.StartForPrivateFixture(shell.Handle);string? fault=null;bridge.Faulted+=(code,_)=>fault=code;
                    if(!NativeTray.Notify(0,ref data))throw new InvalidOperationException("Source registration failed");
                    nint contender=shell.CreateContender();
                    checks["CompetingReceiverBecomesShellCandidate"]=Native.FindWindowW("Shell_TrayWnd",null)==contender;
                    bool deleted=NativeTray.Notify(2,ref data);
                    checks["BypassedDeleteLeavesStaleBridgeRecord"]=deleted&&!shell.Registered&&bridge.Icons.Count==1;
                    bridge.Icons.Values.Single().RehideAfter=0;bridge.Maintain();
                    checks["StaleRecordReproducesMaintenanceFailure"]=fault=="TN2002"&&!bridge.Reconnecting&&bridge.ExplorerAlive&&Native.IsWindow(callback);
                    cases["DeleteBypassesReceiver"]=shell.Operations.ToArray();
                    shell.DestroyContender();
                }
                journal.Complete();

                shell.Reset();
                using(var bridge=new TrayBridge(new Settings(),journal){AllowedOwners=new(){Environment.ProcessId}})
                {
                    bridge.StartForPrivateFixture(shell.Handle);
                    if(!NativeTray.Notify(0,ref data))throw new InvalidOperationException("Source registration failed");
                    shell.CreateContender();shell.RejectModifyOnce=true;bool rejected=false;
                    try{bridge.Collect(bridge.Icons.Values.Single());}catch(InvalidOperationException){rejected=true;}
                    var bypassTrace=LastStateFailure();
                    checks["BypassedStateIsNotMisreportedAsExplorerReply"]=rejected&&bypassTrace?.GetValueOrDefault("ReceivedPackets")=="0"
                        &&bypassTrace.GetValueOrDefault("Forwarding")=="replies=0,zero=0,timeouts=0,suppressed=0"&&bypassTrace.GetValueOrDefault("Pending")=="False";
                    shell.DestroyContender();
                }
                journal.Complete();
            }
            catch(Exception error){errors.Add(error.ToString());}
        }){IsBackground=true};
        worker.SetApartmentState(ApartmentState.STA);worker.Start();
        bool ended=worker.Join(15000);
        if(!ended)
        {
            // Do not inspect collections which a stalled background fixture may
            // still be changing. Exiting this test process ends only its worker.
            File.WriteAllText(Path.Combine(directory,"entry-protocol-test.json"),JsonSerializer.Serialize(new{
                Passed=false,Errors=new[]{"Private protocol worker did not finish within 15 seconds"}},DiagnosticStore.JsonOptions));
            return 1;
        }
        checks["PrivateWorkerCompleted"]=true;
        if(ended&&privateDesktop!=0)checks["PrivateDesktopReleased"]=CloseDesktop(privateDesktop);
        checks["CallerDesktopUnchanged"]=GetThreadDesktop(GetCurrentThreadId())==parentDesktop;
        bool passed=ended&&errors.Count==0&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(directory,"entry-protocol-test.json"),JsonSerializer.Serialize(new{
            Passed=passed,Checks=checks,Cases=cases,Errors=errors,
            Scope="Actual Shell_NotifyIconW on an undisplayed private desktop and own receiver; mechanism reproduction, not proof of the original Windows failure cause"},DiagnosticStore.JsonOptions));
        return passed?0:1;
    }

    private static Dictionary<string,string>? LastStateFailure()=>AppDiagnostics.Store.ReadRecent()
        .LastOrDefault(e=>e.Context.GetValueOrDefault("Operation")=="StateNativeFailure")?.Context;
    private static int RecoveryCount(RecoveryJournal journal)=>JsonSerializer.Deserialize<RecoveryItem[]>(File.ReadAllText(journal.PathName))!.Length;

    private sealed class ReentrantDelete(NativeTray.Data source,nint desktop):IDisposable
    {
        private Thread? sender;
        private readonly ManualResetEventSlim done=new();
        private Exception? error;
        internal bool Deleted;
        internal void Send()
        {
            sender=new Thread(()=>
            {
                try{if(!SetThreadDesktop(desktop))throw new Win32Exception(Marshal.GetLastWin32Error());Deleted=NativeTray.Notify(2,ref source);}
                catch(Exception failure){error=failure;}
                finally{done.Set();}
            }){IsBackground=true};
            sender.SetApartmentState(ApartmentState.STA);sender.Start();
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while(!done.IsSet&&clock.ElapsedMilliseconds<500){Application.DoEvents();Thread.Sleep(5);}
        }
        public void Dispose()
        {
            if(sender!=null&&!sender.Join(3000))throw new TimeoutException("Own deletion sender did not finish");
            done.Dispose();if(error!=null)throw new InvalidOperationException("Own deletion sender failed",error);
        }
    }

    private sealed class ShellFixture:IDisposable
    {
        private readonly Native.Procedure procedure;
        private readonly nint module=Native.GetModuleHandleW("coreclr.dll");
        private readonly List<uint> operations=new();
        internal uint[] Operations{get{lock(operations)return operations.ToArray();}}
        private readonly Thread thread;
        private readonly ManualResetEventSlim ready=new();
        internal readonly ManualResetEventSlim DelayFinished=new(true);
        private Exception? startupError;
        private int delayModify;
        internal bool RejectModifyOnce;
        private nint contender;
        internal nint Handle{get;private set;}
        internal nint LastEntry;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> registered=new();
        internal bool Registered=>!registered.IsEmpty;
        internal Action? OnNextModify;
        internal int Adds;
        private int rejectAdds,rejectVersions;
        internal ShellFixture()
        {
            procedure=Receive;
            nint desktop=testDesktop;
            thread=new Thread(()=>
            {
                try{
                    if(!SetThreadDesktop(desktop))throw new Win32Exception(Marshal.GetLastWin32Error());
                    var cls=new Native.WindowClass{Instance=module,ClassName="Shell_TrayWnd",Callback=procedure};
                    if(Native.RegisterClassW(ref cls)==0)throw new Win32Exception(Marshal.GetLastWin32Error());
                    Handle=Native.CreateWindowExW(0,"Shell_TrayWnd","TrayNest private protocol fixture",0x80000000,0,0,400,40,0,0,module,0);
                    if(Handle==0)throw new Win32Exception(Marshal.GetLastWin32Error());
                    ready.Set();Application.Run();
                }catch(Exception error){startupError=error;ready.Set();}
                finally{if(Handle!=0)Native.DestroyWindow(Handle);Handle=0;Native.UnregisterClassW("Shell_TrayWnd",module);}
            }){IsBackground=true};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();
            if(!ready.Wait(3000)||startupError!=null)throw new InvalidOperationException("Private receiver thread failed",startupError);
        }
        internal void Reset(int rejectAdd=0,int rejectVersion=0)
        {lock(operations)operations.Clear();Adds=0;registered.Clear();LastEntry=0;rejectAdds=rejectAdd;rejectVersions=rejectVersion;RejectModifyOnce=false;OnNextModify=null;}
        internal void DelayModify(int milliseconds){DelayFinished.Reset();Interlocked.Exchange(ref delayModify,milliseconds);}
        internal nint CreateContender()
        {
            contender=Native.CreateWindowExW(8,"Shell_TrayWnd","TrayNest competing private receiver",0x80000000,0,0,400,40,0,0,module,0);
            if(contender==0)throw new Win32Exception(Marshal.GetLastWin32Error());return contender;
        }
        internal void DestroyContender(){if(contender!=0){Native.DestroyWindow(contender);contender=0;}}
        private nint Receive(nint window,uint message,nuint wp,nint lp)
        {
            if(message==0x10){Native.DestroyWindow(window);Application.ExitThread();return 0;}
            if(message!=0x4A||lp==0)return Native.DefWindowProcW(window,message,wp,lp);
            if(window!=Handle)return Native.SendMessageTimeoutW(Handle,message,wp,lp,0x22,750,out nuint reply)!=0?(nint)reply:0;
            var copy=Marshal.PtrToStructure<Native.CopyData>(lp);
            if(copy.Tag!=1||copy.Size!=1484||copy.Bytes==0)return 0;
            var bytes=new byte[copy.Size];Marshal.Copy(copy.Bytes,bytes,0,bytes.Length);
            var packet=TrayPacket.Parse(1,bytes);if(packet==null)return 0;
            lock(operations)operations.Add(packet.Operation);LastEntry=packet.Window;
            string key=IconRecord.Identity(packet.Window,packet.Id,packet.Guid);
            switch(packet.Operation)
            {
                case 0:
                    Adds++;if(rejectAdds-->0)return 0;
                    return registered.TryAdd(key,0)?1:0;
                case 4:return registered.ContainsKey(key)&&rejectVersions--<=0?1:0;
                case 2:return registered.TryRemove(key,out _)?1:0;
                case 1:
                    int delay=Interlocked.Exchange(ref delayModify,0);
                    if(delay>0){try{Thread.Sleep(delay);}finally{DelayFinished.Set();}}
                    var callback=OnNextModify;OnNextModify=null;callback?.Invoke();
                    if(RejectModifyOnce){RejectModifyOnce=false;return 0;}return registered.ContainsKey(key)?1:0;
                default:return registered.ContainsKey(key)?1:0;
            }
        }
        public void Dispose(){DestroyContender();if(Handle!=0)NativeTray.PostMessage(Handle,0x10,0,0);if(!thread.Join(4000))throw new TimeoutException("Private receiver did not stop");ready.Dispose();DelayFinished.Dispose();GC.KeepAlive(procedure);}
    }
}
