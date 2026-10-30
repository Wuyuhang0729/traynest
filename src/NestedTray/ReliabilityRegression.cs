using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class ReliabilityRegression
{
    private sealed record Fixture(long Window,uint Id,Guid Guid,uint Callback,uint Version);
    internal static int Run(string fixtureExe)
    {
        string root=Settings.DirectoryPath;Directory.CreateDirectory(root);
        var checks=new Dictionary<string,object?>();var errors=new List<string>();checks["Errors"]=errors;
        bool startupBefore=Settings.StartupEnabled;
        Process? fixture=null,collector=null;TrayApplication? app=null;
        try
        {
            app=new TrayApplication(true);
            fixture=StartFixture(fixtureExe,Path.Combine(root,"fixture"));app.Bridge.AllowedOwners!.Add(fixture.Id);
            Wait(()=>app.Bridge.Icons.Values.Any(value=>value.ProcessId==fixture.Id&&value.Callback!=0));
            var record=app.Bridge.Icons.Values.Single(value=>value.ProcessId==fixture.Id);
            NativeTray.PostMessage(record.Window,0x8000+905,1,0);Pump(80);
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Wait(()=>!app.Bridge.ReadOriginalBounds(record).IsEmpty);
            Directory.CreateDirectory(Settings.FilePath+".tmp");
            bool saveFailed=false;try{app.Bridge.Collect(record);}catch(TrayNestException error){saveFailed=error.Code=="TN4001";}
            Directory.Delete(Settings.FilePath+".tmp");
            checks["FailedCollectRestoresOriginalAndRule"]=saveFailed&&!record.Selected&&!app.Bridge.ReadOriginalBounds(record).IsEmpty&&!Settings.Load().Collected.Contains(record.Rule);
            app.Bridge.Collect(record);Wait(()=>record.Selected&&app.Bridge.ReadOriginalBounds(record).IsEmpty);
            Directory.CreateDirectory(Settings.FilePath+".tmp");
            bool restoreFailed=false;try{app.Bridge.Restore(record);}catch(TrayNestException error){restoreFailed=error.Code=="TN4001";}
            Directory.Delete(Settings.FilePath+".tmp");
            checks["FailedRestoreRetainsCollectedStateAndRule"]=restoreFailed&&record.Selected&&app.Bridge.ReadOriginalBounds(record).IsEmpty&&Settings.Load().Collected.Contains(record.Rule);
            app.Bridge.Restore(record);Wait(()=>!record.Selected&&!app.Bridge.ReadOriginalBounds(record).IsEmpty);
            checks["RetryAfterPermissionFailureWorks"]=!Settings.Load().Collected.Contains(record.Rule);
            var stale=new IconRecord{Window=0,Callback=1};bool rejected=false;
            try{app.Bridge.ForwardInput(stale,0x400,new Rectangle(0,0,40,40));}catch(TrayNestException error){rejected=error.Code=="TN3001";}
            checks["UnavailableOwnerHasActionableCode"]=rejected;
            byte[] malformed=new byte[1484],copy=(byte[])malformed.Clone();
            checks["UnknownProtocolRejectedWithoutMutation"]=TrayPacket.Parse(1,malformed)==null&&copy.SequenceEqual(malformed)&&TrayPacket.Parse(1,new byte[64])==null;
            checks["DiagnosticsMenuAvailable"]=app.OwnMenu.Items.Cast<ToolStripItem>().Any(item=>item.Text=="诊断与反馈…");
            app.Shutdown();app.Dispose();app=null;
            NativeTray.PostMessage(record.Window,0x10,0,0);Wait(()=>fixture.HasExited);fixture.Dispose();fixture=null;

            // The driver owns both processes. Only the deliberately created collector
            // is killed; the independent watchdog must restore the driver's fixture.
            string crashRoot=Path.Combine(root,"crash"),fixtureRoot=Path.Combine(root,"crash-fixture");Directory.CreateDirectory(crashRoot);
            fixture=StartFixture(fixtureExe,fixtureRoot);Wait(()=>File.Exists(Path.Combine(fixtureRoot,"fixture.json")));
            var manifest=JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(fixtureRoot,"fixture.json")))!;
            collector=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                ArgumentList={"--self-test-crash-collector",crashRoot,fixture.Id.ToString(),Path.GetFullPath(fixtureExe)}})!;
            string ready=Path.Combine(crashRoot,"collector-ready.json");Wait(()=>File.Exists(ready),8000);
            using(var status=JsonDocument.Parse(File.ReadAllText(ready)))checks["CrashFixtureWasCollected"]=status.RootElement.GetProperty("Hidden").GetBoolean();
            collector.Kill();Wait(()=>collector.HasExited);
            Wait(()=>!Directory.GetFiles(crashRoot,"recovery-*.json").Any()&&!Native.GetIconRect((nint)manifest.Window,manifest.Id,manifest.Guid).IsEmpty,8000);
            checks["ActualCollectorCrashRestoresOriginal"]=true;
            var recoveryEvents=new DiagnosticStore(crashRoot).ReadRecent();
            checks["CrashRecoveryIsDiagnosable"]=recoveryEvents.Any(value=>value.Code=="TN6003")&&recoveryEvents.Any(value=>value.Code=="TN0003");
            NativeTray.PostMessage((nint)manifest.Window,0x10,0,0);Wait(()=>fixture.HasExited);
            nint receiver=Native.FindWindowW("Shell_TrayWnd",null);Native.GetWindowThreadProcessId(receiver,out uint owner);
            checks["ExplorerReceiverRestored"]=owner!=0&&Process.GetProcessById((int)owner).ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase);
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally
        {
            app?.Dispose();
            if(collector!=null&&!collector.HasExited){collector.Kill();collector.WaitForExit(1000);}
            if(fixture!=null&&!fixture.HasExited){fixture.Kill();fixture.WaitForExit(1000);}
            collector?.Dispose();fixture?.Dispose();
            checks["StartupUnchanged"]=Settings.StartupEnabled==startupBefore;
            bool pass=errors.Count==0&&!checks.Values.Any(value=>value is false);checks["Passed"]=pass;
            File.WriteAllText(Path.Combine(root,"reliability-test.json"),JsonSerializer.Serialize(checks,DiagnosticStore.JsonOptions));
        }
        return checks["Passed"] is true?0:1;
    }

    internal static int CrashCollector(int fixtureId,string expectedExecutable)
    {
        using var fixture=Process.GetProcessById(fixtureId);
        if(!string.Equals(fixture.MainModule?.FileName,Path.GetFullPath(expectedExecutable),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(expectedExecutable).Equals("TrayFixture.exe",StringComparison.OrdinalIgnoreCase))return 2;
        using var app=new TrayApplication(true);app.Bridge.AllowedOwners!.Add(fixtureId);
        // The fixture uses a message-only owner, so obtain its verified manifest.
        string parentRoot=Path.GetDirectoryName(Settings.DirectoryPath)!;
        var manifest=JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(parentRoot,"crash-fixture","fixture.json")))!;
        nint source=(nint)manifest.Window;Native.GetWindowThreadProcessId(source,out uint ownerPid);if(ownerPid!=fixtureId)return 2;
        // Only this verified test fixture is asked to resubmit callback metadata.
        NativeTray.PostMessage(source,0x8000+906,0,0);
        using var tick=new System.Windows.Forms.Timer{Interval=30};var clock=Stopwatch.StartNew();bool ready=false;
        tick.Tick+=(_,_)=>
        {
            if(clock.ElapsedMilliseconds>15000){tick.Stop();app.Shutdown();return;}
            if(ready)return;
            var record=app.Bridge.Icons.Values.FirstOrDefault(value=>value.ProcessId==fixtureId&&value.Callback!=0);
            if(record==null)return;
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);
            if(app.Bridge.ReadOriginalBounds(record).IsEmpty)return;
            NativeTray.PostMessage(source,0x8000+905,1,0);app.Bridge.Collect(record);ready=true;
            string path=Path.Combine(Settings.DirectoryPath,"collector-ready.json");
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new{Hidden=app.Bridge.ReadOriginalBounds(record).IsEmpty}));File.Move(path+".tmp",path,true);
        };
        tick.Start();Application.Run(app);return ready?0:1;
    }
    private static Process StartFixture(string executable,string directory)=>Process.Start(new ProcessStartInfo(Path.GetFullPath(executable)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={directory,"NestedTray reliability fixture"}})!;
    private static void Wait(Func<bool> condition,int timeout=4000){var time=Stopwatch.StartNew();while(!condition()){if(time.ElapsedMilliseconds>timeout)throw new TimeoutException("Reliability step timed out.");Pump(20);}}
    private static void Pump(int milliseconds){var time=Stopwatch.StartNew();while(time.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
}
