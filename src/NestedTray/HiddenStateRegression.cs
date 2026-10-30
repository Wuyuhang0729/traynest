using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class HiddenStateRegression
{
    internal static int Run(string fixtureExe)
    {
        Directory.CreateDirectory(Settings.DirectoryPath);
        var report=new Dictionary<string,object?>{["Scope"]="Own fixture only; no mouse or keyboard injection, startup writes or discovery broadcast."};
        var errors=new List<string>();report["Errors"]=errors;
        using var app=new TrayApplication(true);
        string folder=Path.Combine(Settings.DirectoryPath,"fixture");
        using var fixture=Process.Start(new ProcessStartInfo(Path.GetFullPath(fixtureExe)){UseShellExecute=false,CreateNoWindow=true,ArgumentList={folder,"NestedTray hidden-state fixture"}})!;
        app.Bridge.AllowedOwners!.Add(fixture.Id);
        try
        {
            Wait(()=>app.Bridge.Icons.Values.Any(value=>value.ProcessId==fixture.Id&&value.Callback!=0));
            var record=app.Bridge.Icons.Values.Single(value=>value.ProcessId==fixture.Id);
            NativeTray.PostMessage(record.Window,0x8000+905,1,0);Pump(100);
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Wait(()=>Visible());
            report["FixtureInitiallyVisible"]=true;
            app.Bridge.Collect(record);Wait(()=>!Visible());report["InitialCollectionHidesOriginal"]=true;
            // Simulate a visibility change made directly by the native host,
            // bypassing this tool's notification receiver and image updates.
            record.RehideAfter=Environment.TickCount64+900;
            app.Bridge.ResetVisibleForOwnTest(record);Wait(()=>Visible());
            report["NativeHostCanResetVisibleWhileRuleRemainsSelected"]=record.Selected;
            int before=app.Bridge.HiddenStateReapplications;
            Wait(()=>!Visible()&&app.Bridge.HiddenStateReapplications>before);
            report["MaintenanceHidesResurfacedOriginal"]=true;
            report["ExactlyOneCollectedRecord"]=app.Bridge.Icons.Values.Count(value=>value.Selected&&value.ProcessId==fixture.Id)==1;
            app.Bridge.Restore(record);Wait(()=>Visible());report["RestoreStillReturnsOriginal"]=true;
            bool Visible()
            {
                nint overflow=NativeOverflow.Window;if(overflow==0||!Native.IsWindowVisible(overflow))return false;
                var nodes=AutomationElement.FromHandle(overflow).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
                foreach(AutomationElement node in nodes)if(!node.Current.IsOffscreen&&node.Current.Name.StartsWith("NestedTray hidden-state fixture",StringComparison.Ordinal))return true;
                return false;
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally
        {
            app.Dispose();if(!fixture.HasExited){fixture.Kill();fixture.WaitForExit(1000);}
            File.WriteAllText(Path.Combine(Settings.DirectoryPath,"hidden-state-test.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        }
        return errors.Count==0&&!report.Values.Any(value=>value is false)?0:1;
    }
    private static void Wait(Func<bool> ready){var time=Stopwatch.StartNew();while(!ready()){if(time.ElapsedMilliseconds>4000)throw new TimeoutException("Hidden state step timed out.");Pump(20);}}
    private static void Pump(int ms){var time=Stopwatch.StartNew();while(time.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
}
