using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class EntryHoverRegression
{
    internal static int Run(string fixtureExe)
    {
        string root=Settings.DirectoryPath;Directory.CreateDirectory(root);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,25000,Timeout.Infinite);
        using var app=new TrayApplication(true);
        using var fixture=Process.Start(new ProcessStartInfo(Path.GetFullPath(fixtureExe)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={Path.Combine(root,"fixture"),"NestedTray entry-hover fixture"}})!;
        app.Bridge.AllowedOwners!.Add(fixture.Id);
        Thread? driver=null;IconRecord? record=null;
        using var ready=new System.Windows.Forms.Timer{Interval=100};
        ready.Tick+=(_,_)=>
        {
            if(driver!=null)return;
            record=app.Bridge.Icons.Values.FirstOrDefault(value=>value.ProcessId==fixture.Id&&value.CanCollect);
            if(record==null)return;
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);
            var from=Native.GetIconRect(record.Window,record.Id,record.Guid);var entry=app.Entry.Bounds;
            if(from.IsEmpty||entry.IsEmpty)return;
            ready.Stop();
            driver=new Thread(()=>
            {
                Native.GetCursorPos(out var previous);
                try
                {
                    if((Native.GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Mouse button in use; controlled drag was not started");
                    foreach(bool initiallyOpen in new[]{false,true})
                    {
                        Ui(()=>
                        {
                            if(record!.Selected)app.Bridge.Restore(record);
                            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);
                            if(initiallyOpen)app.Panel.Open();else app.Panel.Hide();
                            from=Native.GetIconRect(record.Window,record.Id,record.Guid);entry=app.Entry.Bounds;
                        });
                        Thread.Sleep(250);
                        Ui(()=>NativeOverflow.TryOpen(app.Bridge.ExplorerWindow));
                        // Wait for the native overflow's opening animation before
                        // freezing screen coordinates for the physical gesture.
                        Thread.Sleep(250);
                        Ui(()=>
                        {
                            if(!NativeOverflow.Visible)throw new InvalidOperationException("Native overflow closed before the controlled drag");
                            var nodes=AutomationElement.FromHandle(NativeOverflow.Window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
                            var node=nodes.Cast<AutomationElement>().FirstOrDefault(n=>!n.Current.IsOffscreen&&n.Current.Name.StartsWith("NestedTray entry-hover fixture",StringComparison.Ordinal));
                            if(node==null)throw new InvalidOperationException("Fixture is not visibly available before the controlled drag");
                            var rect=node.Current.BoundingRectangle;
                            from=Rectangle.FromLTRB((int)rect.Left,(int)rect.Top,(int)rect.Right,(int)rect.Bottom);
                            entry=app.Entry.Bounds;
                        });
                        var a=new Point(from.Left+from.Width/2,from.Top+from.Height/2);
                        var b=new Point(entry.Left+entry.Width/2,entry.Top+entry.Height/2);
                        Native.SetForegroundWindow(NativeOverflow.Window);
                        Native.SetCursorPos(a.X,a.Y);Thread.Sleep(120);
                        var hit=Native.GetAncestor(Native.WindowFromPoint(new Native.Point{X=a.X,Y=a.Y}),2);
                        var hitClass=new System.Text.StringBuilder(256);Native.GetClassNameW(hit,hitClass,256);Native.GetCursorPos(out var actual);
                        File.WriteAllText(Path.Combine(root,initiallyOpen?"source-open.json":"source-closed.json"),JsonSerializer.Serialize(new{From=from.ToString(),Entry=entry.ToString(),Hit=hit.ToInt64(),HitClass=hitClass.ToString(),Overflow=NativeOverflow.Window.ToInt64(),Explorer=app.Bridge.ExplorerWindow.ToInt64(),CursorX=actual.X,CursorY=actual.Y,Down=Native.GetAsyncKeyState(1)},DiagnosticStore.JsonOptions));
                        Button(2);Thread.Sleep(180);
                        for(int i=1;i<=12;i++){Native.SetCursorPos(a.X+(b.X-a.X)*i/12,a.Y+(b.Y-a.Y)*i/12);Thread.Sleep(45);}
                        Thread.Sleep(500);
                        string prefix=initiallyOpen?"OpenPanel":"ClosedPanel";
                        Ui(()=>
                        {
                            checks[prefix+"RecognizesSource"]=app.HasDragCandidate;
                            checks[prefix+"HoverExpandsWithoutBlocking"]=app.Panel.Visible;
                            // Exercise the resize/reposition path while Explorer still owns the drag.
                            app.Panel.SetIncomingIcon(record);app.Panel.SetIncomingIcon(null);
                            checks[prefix+"RepositionRemainsResponsive"]=true;
                        });
                        Button(4);
                        var settle=Stopwatch.StartNew();bool collected=false;
                        while(!collected&&settle.ElapsedMilliseconds<4000)
                        {Thread.Sleep(100);Ui(()=>collected=record!.Selected);}
                        Ui(()=>{checks[prefix+"DropOnEntryCollects"]=record!.Selected&&Settings.Load().Collected.Contains(record.Rule);app.Entry.DiagnosticsRequested?.Invoke();});
                        if(!collected)throw new InvalidOperationException("Entry drop did not finish within its protocol grace period");
                        Thread.Sleep(1100);
                        Ui(()=>{var surface=NativeOverflow.SurfaceBounds(NativeOverflow.Window,(int)Math.Round(4*app.Panel.DeviceDpi/96.0));checks[prefix+"BottomAlignsAfterNativeReflow"]=app.Panel.Visible&&app.Panel.Bottom==surface.Bottom;});
                    }
                }
                catch(Exception error){errors.Add(error.ToString());}
                finally
                {
                    Button(4);Native.SetCursorPos(previous.X,previous.Y);
                    File.WriteAllText(Path.Combine(root,"entry-hover-test.json"),JsonSerializer.Serialize(new{Passed=errors.Count==0&&checks.Count==10&&checks.Values.All(v=>v),Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));
                    try{app.Panel.BeginInvoke((Action)app.Shutdown);}catch(InvalidOperationException){}
                }
                void Ui(Action action)
                {
                    using var done=new ManualResetEventSlim();Exception? failure=null;
                    app.Panel.BeginInvoke((Action)(()=>{try{action();}catch(Exception ex){failure=ex;}finally{done.Set();}}));
                    if(!done.Wait(2500))throw new TimeoutException("Product UI stopped responding during entry hover");
                    if(failure!=null)throw failure;
                }
            }){IsBackground=true};
            driver.Start();
        };
        ready.Start();Application.Run(app);driver?.Join(2000);
        if(record!=null)NativeTray.PostMessage(record.Window,0x10,0,0);
        if(!fixture.WaitForExit(2000)){fixture.Kill();fixture.WaitForExit(1000);}
        return errors.Count==0&&checks.Count==10&&checks.Values.All(v=>v)?0:1;
    }
    private static void Button(uint flags)
    {var input=new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=flags}};if(Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>())!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
}
