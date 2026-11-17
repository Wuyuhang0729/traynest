using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;

internal static class FullscreenLifecycleRegression
{
    internal static int Run(string directory,string fixtureExecutable,int monitorIndex=-1)
    {
        directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",directory);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();var samples=new List<object>();
        TrayApplication? app=null;Process? fixture=null;string? rule=null;
        var sent=new HashSet<string>();
        using var diagnosticGuard=new System.Windows.Forms.Timer{Interval=100};
        diagnosticGuard.Tick+=(_,_)=>
        {
            if(DiagnosticUi.OpenWindows==0)return;
            errors.Add("Production application opened a diagnostic failure window during the fixture");
            foreach(var form in Application.OpenForms.Cast<Form>().Where(f=>f.Text.StartsWith("TrayNest · TN")).ToArray())form.Close();
        };
        diagnosticGuard.Start();
        try
        {
            app=new TrayApplication(testMode:true);
            var info=new ProcessStartInfo(Path.GetFullPath(fixtureExecutable)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            info.ArgumentList.Add(directory);info.ArgumentList.Add("--lifecycle");
            if(monitorIndex>=0){info.ArgumentList.Add("--monitor");info.ArgumentList.Add(monitorIndex.ToString());}
            fixture=Process.Start(info)??throw new InvalidOperationException("Own fullscreen fixture did not start");
            app.Bridge.AllowedOwners!.Add(fixture.Id);
            var timer=Stopwatch.StartNew();bool ready=false;
            while(!fixture.HasExited&&timer.ElapsedMilliseconds<30000)
            {
                Application.DoEvents();
                var source=app.Bridge.Icons.Values.FirstOrDefault(icon=>icon.ProcessId==fixture.Id);
                if(!ready&&source is {CanCollect:true,Version:4})
                {
                    app.Bridge.Collect(source);rule=source.Rule;
                    ready=source.Selected;checks["CollectedOwnSourceBeforeFullscreen"]=ready;
                    if(ready)File.WriteAllText(Path.Combine(directory,"collected.ready"),"collected");
                }
                string trace="";
                try
                {
                    // The native fixture keeps its writer open throughout the run.
                    using var stream=new FileStream(Path.Combine(directory,"fixture.private.jsonl"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                    using var reader=new StreamReader(stream);
                    trace=reader.ReadToEnd();
                }
                catch(IOException){}
                bool exclusive=trace.Contains("\"Phase\":\"ExclusiveRequested\"")&&!trace.Contains("\"Phase\":\"ExclusiveExited\"");
                bool borderless=trace.Contains("\"Phase\":\"BorderlessFullscreen\"")&&!trace.Contains("\"Phase\":\"ExclusiveRequested\"");
                if(borderless&&trace.Contains("\"Phase\":\"BorderlessSourceDeleted\"")&&!trace.Contains("\"Phase\":\"BorderlessSourceReadded\"")&&source==null)
                    checks["DeleteDuringBorderlessRemovesRuntimeRecord"]=true;
                if(exclusive&&trace.Contains("\"Phase\":\"ExclusiveSourceDeleted\"")&&!trace.Contains("\"Phase\":\"ExclusiveSourceReadded\"")&&source==null)
                    checks["DeleteDuringExclusiveRemovesRuntimeRecord"]=true;
                if(source is {Selected:true,Version:4})
                {
                    string phase=trace.Contains("\"Phase\":\"ReturnedToDesktop\"")?"Returned":exclusive?"Exclusive":borderless?"Borderless":"Windowed";
                    if(sent.Add(phase))app.Bridge.ForwardInput(source,0x400,new Rectangle(100,100,40,40));
                    if(borderless&&trace.Contains("\"Phase\":\"BorderlessSourceReadded\""))checks["RuleRecollectsAfterBorderlessReregistration"]=source.Rule==rule;
                    if(exclusive&&trace.Contains("\"Phase\":\"ExclusiveSourceReadded\""))checks["RuleRecollectsAfterExclusiveReregistration"]=source.Rule==rule;
                }
                samples.Add(new{Ms=timer.ElapsedMilliseconds,Selected=source?.Selected??false,SourcePresent=source!=null,app.Bridge.Reconnecting,app.Bridge.ExplorerAlive,ExclusivePhase=exclusive});
                Thread.Sleep(20);
            }
            if(!fixture.HasExited)throw new TimeoutException("Own fixture did not finish within 30 seconds");
            for(int i=0;i<15;i++){Application.DoEvents();Thread.Sleep(20);}
            checks["FixtureExitedNormally"]=fixture.ExitCode==0;
            checks["SourceRemovedAtExit"]=!app.Bridge.Icons.Values.Any(icon=>icon.ProcessId==fixture.Id);
            checks["SourceRulePreservedInIsolatedSettings"]=rule!=null&&Settings.Load().Collected.Contains(rule);
            checks["OnlyOwnSourceWasCollected"]=Settings.Load().Collected.Count==1;
            using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"fixture-result.json")));
            var data=result.RootElement;
            checks["ActualExclusiveFullscreenEntered"]=data.GetProperty("ExclusiveActuallyEntered").GetBoolean();
            checks["DisplayModeRestored"]=data.GetProperty("DisplayModeRestored").GetBoolean();
            checks["MonitorCountRestored"]=data.GetProperty("MonitorCountUnchanged").GetBoolean();
            checks["WindowCreatedOnSelectedMonitor"]=data.GetProperty("WindowOnTarget").GetBoolean();
            checks["ExclusiveActuallyOnSelectedMonitor"]=data.GetProperty("ExclusiveOnTarget").GetBoolean();
            checks["ReturnedToPrimaryMonitor"]=data.GetProperty("ReturnedToPrimary").GetBoolean();
            checks["RequestedMonitorHonored"]=monitorIndex<0||data.GetProperty("TargetIndex").GetInt32()==monitorIndex;
            checks["NotCancelled"]=!data.GetProperty("Cancelled").GetBoolean();
            checks["ForwardedClicksReachOriginalWindow"]=data.GetProperty("TrayClicks").GetInt32()>=3;
            checks["ForwardedClickDuringActualExclusive"]=data.GetProperty("ExclusiveClicks").GetInt32()>=1;
            foreach(string required in new[]{"DeleteDuringBorderlessRemovesRuntimeRecord","DeleteDuringExclusiveRemovesRuntimeRecord","RuleRecollectsAfterBorderlessReregistration","RuleRecollectsAfterExclusiveReregistration"})
                checks.TryAdd(required,false);
            checks["NoCollectorFatalEvents"]=!AppDiagnostics.Store.ReadRecent().Any(e=>e.Code is "TN1003" or "TN2002" or "TN2003" or "TN2004" or "TN2005" or "TN6001" or "TN9001");
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally
        {
            if(fixture!=null&&!fixture.HasExited){fixture.CloseMainWindow();if(!fixture.WaitForExit(5000))errors.Add("Own fixture remains running; no forced termination used");}
            fixture?.Dispose();app?.Dispose();diagnosticGuard.Stop();
        }
        bool passed=errors.Count==0&&checks.Count>0&&checks.Values.All(v=>v);
        File.WriteAllText(Path.Combine(directory,"fullscreen-lifecycle-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Samples=samples,Scope="Own source; isolated settings; actual native collection, callback forwarding and DXGI fullscreen; no real mouse input"},DiagnosticStore.JsonOptions));
        return passed?0:1;
    }
}
