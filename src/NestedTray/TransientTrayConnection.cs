namespace NestedTray;

// A missed synchronous send is not evidence that Explorer has exited.
// Keep the collector alive, avoid repeatedly blocking its callers, and probe
// the original window at a bounded rate until it responds again.
internal sealed class TransientTrayConnection
{
    private long nextProbe;
    internal bool Pending { get; private set; }
    internal int Attempts { get; private set; }
    internal event Action? Interrupted, Recovered;

    // Shell_NotifyIcon reports failure when our synchronous forward times out.
    // Keep the saved selection and retry after the transport recovers instead
    // of treating that secondary failure as a fatal inability to hide an icon.
    internal bool ReapplyHiddenState(Action apply)
    {
        if(Pending)return false;
        try {apply();return !Pending;}
        catch(InvalidOperationException)when(Pending){return false;}
    }

    internal nint Send(long now, Func<(bool Success,nuint Result)> send)
    {
        if(Pending)return 0;
        var result=send();
        if(result.Success)return unchecked((nint)result.Result);
        Pending=true;Attempts=0;nextProbe=now+1000;Interrupted?.Invoke();return 0;
    }

    internal bool Probe(long now, Func<bool> probe)
    {
        if(!Pending||now<nextProbe)return false;
        Attempts++;
        if(probe())
        {
            Pending=false;nextProbe=0;Recovered?.Invoke();return true;
        }
        // One second initially, then two, capped at five seconds even after
        // a long sleep or outage. No busy loop and no forced process exit.
        nextProbe=now+(Attempts<3?1000:Attempts<10?2000:5000);
        return false;
    }
}

internal static class TrayConnectionRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();
        var state=new TransientTrayConnection();int interrupted=0,recovered=0,sends=0,probes=0;
        state.Interrupted+=()=>interrupted++;state.Recovered+=()=>recovered++;
        checks["NormalReplyPreserved"]=state.Send(0,()=>{sends++;return(true,42u);})==42;
        checks["FirstTimeoutKeepsConnectionPending"]=state.Send(10,()=>{sends++;return(false,0u);})==0&&state.Pending;
        checks["SubsequentCallsDoNotBlockExplorerAgain"]=state.Send(20,()=>{sends++;return(true,4u);})==0&&sends==2;
        checks["NoImmediateRetry"]=!state.Probe(1009,()=>{probes++;return true;})&&probes==0;
        checks["OneWarningPerOutage"]=interrupted==1;
        checks["UnresponsiveProbeKeepsProgramRecoverable"]=!state.Probe(1010,()=>{probes++;return false;})&&state.Pending&&state.Attempts==1;
        checks["RecoveryAfterBriefStall"]=state.Probe(2010,()=>{probes++;return true;})&&!state.Pending&&recovered==1;
        checks["SuccessfulSendsResume"]=state.Send(2020,()=>{sends++;return(true,73u);})==73;
        state.Send(2030,()=> (false,0u));
        long now=3030;int bounded=0;
        for(int i=0;i<100000;i++){state.Probe(now,()=>{bounded++;return false;});now++;}
        checks["LongOutageHasBoundedRetries"]=state.Pending&&bounded<35&&bounded>10;
        int before=bounded;state.Probe(now-1,()=>{bounded++;return false;});
        checks["NoRetryStormAfterLongOutage"]=bounded==before;
        checks["ResumeAfterLongSleep"]=state.Probe(now+8*60*60*1000L,()=>true)&&!state.Pending&&recovered==2;
        checks["ExplicitNewOutageCanRecoverAgain"]=state.Send(now+9*60*60*1000L,()=> (false,0u))==0&&interrupted==3;
        var maintenance=new TransientTrayConnection();int stateWrites=0;
        checks["RehideTimeoutDoesNotEscapeAsFatalStateFailure"]=!maintenance.ReapplyHiddenState(()=>
        {stateWrites++;maintenance.Send(0,()=> (false,0u));throw new InvalidOperationException("Simulated Shell_NotifyIcon failure after a transport timeout");})&&maintenance.Pending;
        checks["PendingMaintenanceDoesNotSendMoreStateChanges"]=!maintenance.ReapplyHiddenState(()=>stateWrites++)&&stateWrites==1;
        checks["WakeAfterEightHoursCanRecoverMaintenance"]=maintenance.Probe(8*60*60*1000L,()=>true);
        checks["HiddenStateReappliedAfterWake"]=maintenance.ReapplyHiddenState(()=>stateWrites++)&&stateWrites==2&&!maintenance.Pending;
        bool realFailure=false;
        try{maintenance.ReapplyHiddenState(()=>throw new InvalidOperationException("Non-transport state failure"));}
        catch(InvalidOperationException){realFailure=true;}
        checks["NonTransportStateFailureIsStillReported"]=realFailure;
        bool unexpected=false;
        try{maintenance.ReapplyHiddenState(()=>{maintenance.Send(0,()=> (false,0u));throw new IOException("Journal failure");});}
        catch(IOException){unexpected=true;}
        checks["UnrelatedErrorsAreNotSuppressed"]=unexpected;
        NativeRecoveryCheck(checks);
        bool pass=checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(directory,"tray-connection-test.json"),System.Text.Json.JsonSerializer.Serialize(new{Passed=pass,Checks=checks},DiagnosticStore.JsonOptions));
        return pass?0:1;
    }

    private static void NativeRecoveryCheck(Dictionary<string,bool> checks)
    {
        using var ready=new ManualResetEventSlim();nint handle=0;Exception? failure=null;
        var thread=new Thread(()=>
        {
            try{using var window=new SlowTestWindow();handle=window.Handle;ready.Set();Application.Run();}
            catch(Exception error){failure=error;ready.Set();}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        try
        {
            if(!ready.Wait(3000)||failure!=null||handle==0)throw new InvalidOperationException("Isolated liveness test window unavailable");
            var connection=new TransientTrayConnection();
            bool applied=connection.ReapplyHiddenState(()=>
            {
                connection.Send(0,()=>{bool ok=Native.SendMessageTimeoutW(handle,0x8000+77,0,0,0x22,40,out nuint reply)!=0;return(ok,reply);});
                if(connection.Pending)throw new InvalidOperationException("State change failed because native forwarding timed out");
            });
            checks["ActualNativeTimeoutDuringRehideDoesNotTerminate"]=!applied&&connection.Pending;
            checks["ActualNativeTimeoutKeepsConnectionPending"]=connection.Pending;
            var resume=System.Diagnostics.Stopwatch.StartNew();bool responsive=false;
            while(!responsive&&resume.ElapsedMilliseconds<3000)
            {
                responsive=connection.Probe(1000+resume.ElapsedMilliseconds,()=>Native.SendMessageTimeoutW(handle,0,0,0,0x22,150,out _)!=0);
                if(!responsive)Thread.Sleep(20);
            }
            checks["ActualNativeResponsivenessResumesConnection"]=responsive&&!connection.Pending;
        }
        catch{checks["IsolatedNativeCheckCompleted"]=false;}
        finally{if(handle!=0)NativeTray.PostMessage(handle,0x10,0,0);thread.Join(1000);}
    }

    private sealed class SlowTestWindow:NativeWindow,IDisposable
    {
        internal SlowTestWindow()=>CreateHandle(new CreateParams{Caption="TrayNest isolated liveness check",Parent=new nint(-3)});
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x8000+77){Thread.Sleep(150);message.Result=1;return;}
            if(message.Msg==0x10){DestroyHandle();Application.ExitThread();return;}
            base.WndProc(ref message);
        }
        public void Dispose(){if(Handle!=0)DestroyHandle();}
    }
}
