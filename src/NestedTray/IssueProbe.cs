using System.Diagnostics;
using System.Text.Json;
namespace NestedTray;
internal static class IssueProbe
{
 internal static int Run(string directory)
 {
  Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(directory));
  using var bridge=new TrayBridge(new Settings(),new RecoveryJournal());
  var names=new[]{"ClickShow","TranslucentTB","TrafficMonitor"};
  bridge.AllowedOwners=Process.GetProcesses().Where(p=>names.Contains(p.ProcessName)).Select(p=>p.Id).ToHashSet();bridge.Start();
  Native.EnumWindows((h,_)=>{Native.GetWindowThreadProcessId(h,out uint pid);if(bridge.AllowedOwners.Contains((int)pid))NativeTray.PostMessage(h,Native.RegisterWindowMessageW("TaskbarCreated"),0,0);return true;},0);
  var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<1800){Application.DoEvents();Thread.Sleep(10);}
  File.WriteAllText(Path.Combine(directory,"icons.private.json"),JsonSerializer.Serialize(bridge.Icons.Values.Select(r=>new{r.Rule,r.Executable,ApplicationId=ProcessExecutable.ApplicationId(r.ProcessId),r.Tooltip,r.ProcessId,r.Callback,r.Id,r.Guid,r.Version,r.RegistrationKnown,r.CanCollect}),DiagnosticStore.JsonOptions));
  if(Environment.GetEnvironmentVariable("TRAYNEST_PROBE_CLICK")=="1")
  {
   var icon=bridge.Icons.Values.Single(r=>Path.GetFileName(r.Executable).Equals("ClickShow.exe",StringComparison.OrdinalIgnoreCase));
   bridge.Collect(icon);var saved=SavedTool.FromRecord(icon);
   bridge.ForwardInput(icon,0x7B,new Rectangle(500,500,20,20));
   var wait=Stopwatch.StartNew();System.Windows.Automation.AutomationElement? exit=null;
   while(wait.ElapsedMilliseconds<2500&&exit==null)
   {
    Application.DoEvents();Thread.Sleep(30);
    exit=System.Windows.Automation.AutomationElement.RootElement.FindFirst(System.Windows.Automation.TreeScope.Descendants,new System.Windows.Automation.AndCondition(new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ProcessIdProperty,icon.ProcessId),new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty,"退出(Exit)")));
   }
   if(exit==null)throw new InvalidOperationException("ClickShow normal exit menu not found");
   var bounds=exit.Current.BoundingRectangle;Native.GetCursorPos(out var cursor);
   try{Native.SetCursorPos((int)(bounds.Left+bounds.Width/2),(int)(bounds.Top+bounds.Height/2));
    Native.SendInput(1,[new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=2}}],System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
    Native.SendInput(1,[new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=4}}],System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
    var clicked=Stopwatch.StartNew();while(clicked.ElapsedMilliseconds<120){Application.DoEvents();Thread.Sleep(10);}}
   finally{Native.SetCursorPos(cursor.X,cursor.Y);}
   wait.Restart();while(Process.GetProcessesByName("ClickShow").Length>0&&wait.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(20);}
   bridge.PruneClosed();bridge.AllowedOwners=null;var launch=new ToolLauncher().Launch(saved);wait.Restart();
   while(wait.ElapsedMilliseconds<2200){Application.DoEvents();bridge.PruneClosed();Thread.Sleep(15);}
   var current=bridge.Icons.Values.Where(r=>r.Rule==icon.Rule).ToArray();bool passed=launch.Started&&current.Length==1&&current[0].Selected&&current[0].ProcessId!=icon.ProcessId;
   File.WriteAllText(Path.Combine(directory,"click-launch.json"),JsonSerializer.Serialize(new{Passed=passed,launch.Started,Count=current.Length,Selected=current.FirstOrDefault()?.Selected,Pid=current.FirstOrDefault()?.ProcessId},DiagnosticStore.JsonOptions));return passed?0:1;
  }
  if(Environment.GetEnvironmentVariable("TRAYNEST_PROBE_LAUNCH")=="1")
  {
   var ttb=bridge.Icons.Values.Single(r=>Path.GetFileName(r.Executable).Equals("TranslucentTB.exe",StringComparison.OrdinalIgnoreCase));
   bridge.Collect(ttb);var saved=SavedTool.FromRecord(ttb);saved.Executable+=".old-version";saved.LaunchTarget=saved.Executable;
   nint tray=Native.FindWindowW("TrayWindow","TranslucentTB");Native.GetWindowThreadProcessId(tray,out uint owner);
   if(owner!=(uint)ttb.ProcessId)throw new InvalidOperationException("TTB owner mismatch");
   NativeTray.PostMessage(tray,0x10,0,0);var wait=Stopwatch.StartNew();while(Process.GetProcessesByName("TranslucentTB").Length>0&&wait.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(20);}
   bridge.AllowedOwners=null;var launch=new ToolLauncher().Launch(saved);wait.Restart();
   while(wait.ElapsedMilliseconds<2500){Application.DoEvents();bridge.PruneClosed();Thread.Sleep(15);}
   var current=bridge.Icons.Values.Where(r=>r.Guid==ttb.Guid).ToArray();
   bool passed=launch.Started&&saved.ApplicationId.Length>0&&current.Length==1&&current[0].Selected&&current[0].ProcessId!=ttb.ProcessId;
   File.WriteAllText(Path.Combine(directory,"launch.json"),JsonSerializer.Serialize(new{Passed=passed,launch.Started,saved.ApplicationId,Count=current.Length,Selected=current.FirstOrDefault()?.Selected,NewPid=current.FirstOrDefault()?.ProcessId,NewRule=current.FirstOrDefault()?.Rule,OldRule=ttb.Rule,CanCollect=current.FirstOrDefault()?.CanCollect},DiagnosticStore.JsonOptions));
   return passed?0:1;
  }
  return 0;
 }
}
