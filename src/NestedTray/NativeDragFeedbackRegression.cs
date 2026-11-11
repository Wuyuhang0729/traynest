using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class NativeDragFeedbackRegression
{
    internal static int Run(string fixtureExe)
    {
        string root=Settings.DirectoryPath;Directory.CreateDirectory(root);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var frames=new List<object>();
        using var app=new TrayApplication(true);
        using var fixture=Process.Start(new ProcessStartInfo(Path.GetFullPath(fixtureExe)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={Path.Combine(root,"fixture"),"TN010 native drag fixture"}})!;
        app.Bridge.AllowedOwners!.Add(fixture.Id);
        IconRecord? record=null;Thread? driver=null;
        using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,90000,Timeout.Infinite);
        using var ready=new System.Windows.Forms.Timer{Interval=80};
        ready.Tick+=(_,_)=>
        {
            if(driver!=null)return;
            record=app.Bridge.Icons.Values.FirstOrDefault(r=>r.ProcessId==fixture.Id&&r.CanCollect);
            if(record==null)return;
            ready.Stop();
            driver=new Thread(()=>
            {
                Native.GetCursorPos(out var previous);nint foreground=Native.GetForegroundWindow();
                try
                {
                    if((Native.GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Mouse already pressed");
                    foreach(var mode in new[]{PanelAppearance.Light,PanelAppearance.Dark,PanelAppearance.Acrylic})
                    {
                        foreach(bool cancel in new[]{true,false})
                        {
                            string prefix=mode+(cancel?"-cancel":"-drop");
                            Ui(()=>{if(record!.Selected)app.Bridge.Restore(record);((Settings)typeof(TrayApplication).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(app)!).ChangeAppearance(mode);app.Panel.SetAppearance(mode);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);app.Panel.Open();});
                            Thread.Sleep(550);
                            Point start=Point.Empty;
                            Ui(()=>
                            {
                                NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);
                                var nodes=AutomationElement.FromHandle(NativeOverflow.Window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
                                var node=nodes.Cast<AutomationElement>().FirstOrDefault(n=>!n.Current.IsOffscreen&&n.Current.Name.StartsWith("TN010 native drag fixture",StringComparison.Ordinal))??throw new InvalidOperationException("Own fixture not visible in native tray");
                                var r=node.Current.BoundingRectangle;start=new Point((int)(r.Left+r.Width/2),(int)(r.Top+r.Height/2));
                            });
                                                        Native.SetForegroundWindow(NativeOverflow.Window);Thread.Sleep(250);
                            // First overflow opening can reflow after registering the entry.
                            // Reacquire the own source after its animation, never press stale coordinates.
                            Ui(()=>
                            {
                                if(!app.Panel.Visible)app.Panel.Open();
                                var nodes=AutomationElement.FromHandle(NativeOverflow.Window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
                                var own=nodes.Cast<AutomationElement>().Single(n=>!n.Current.IsOffscreen&&n.Current.Name.StartsWith("TN010 native drag fixture",StringComparison.Ordinal));
                                var rect=own.Current.BoundingRectangle;start=new Point((int)(rect.Left+rect.Width/2),(int)(rect.Top+rect.Height/2));
                            });
                            Thread.Sleep(250);Native.SetCursorPos(start.X,start.Y);Thread.Sleep(100);Mouse(2);Thread.Sleep(120);
                            Ui(()=>checks[prefix+"-own-source-recognized"]=app.HasDragCandidate);
                            MoveToPanel();Sample("entered",8,true);
                            Point alternate=Point.Empty;Ui(()=>alternate=new Point(app.Panel.Right-15,app.Panel.Bottom-15));Move(alternate);Sample("moving",8,true);
                            if(cancel)
                            {
                                Move(start);Thread.Sleep(150);Sample("left",3,false);
                                MoveToPanel();Sample("reentered",6,true);
                                Key(false);Thread.Sleep(120);Key(true);Sample("cancelled",3,false);Mouse(4);Thread.Sleep(150);
                                Ui(()=>checks[prefix+"-not-collected"]=!record!.Selected);
                            }
                            else
                            {
                                MoveToPanel();Sample("before-release",5,true);Mouse(4);
                                var wait=Stopwatch.StartNew();bool collected=false;
                                while(!collected&&wait.ElapsedMilliseconds<3500){Thread.Sleep(60);Ui(()=>collected=record!.Selected);}
                                Ui(()=>{checks[prefix+"-collected-and-saved"]=record!.Selected&&Settings.Load().Collected.Contains(record.Rule);checks[prefix+"-preview-cleared"]=!app.Panel.HasIncomingIconPreview;});
                            }
                            void Sample(string stage,int count,bool expected)
                            {
                                bool stable=true;
                                for(int i=0;i<count;i++)
                                {
                                    Thread.Sleep(35);
                                    Ui(()=>
                                    {
                                        var panel=app.Panel;bool shown=panel.HasIncomingIconPreview;stable&=shown==expected&&panel.Appearance==mode;
                                        frames.Add(new{Scenario=prefix,Stage=stage,Frame=i,Time=Environment.TickCount64,Preview=shown,Expected=expected,Panel=panel.Bounds.ToString(),panel.AcrylicActive,Appearance=panel.Appearance.ToString()});
                                        if(panel.Visible){using var shot=new Bitmap(panel.Width,panel.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(panel.Location,Point.Empty,panel.Size);                                        if(expected)
                                        {
                                            int colored=0;
                                            for(int y=8;y<Math.Min(shot.Height,36);y++)for(int x=8;x<Math.Min(shot.Width,36);x++)
                                            {var color=shot.GetPixel(x,y);if(Math.Max(color.R,Math.Max(color.G,color.B))-Math.Min(color.R,Math.Min(color.G,color.B))>60)colored++;}
                                            stable&=colored>=3;
                                        }
                                        shot.Save(Path.Combine(root,prefix+"-"+stage+"-"+i+".png"));}
                                    });
                                }
                                checks[prefix+"-"+stage+"-stable"]=stable;
                            }
                        }
                    }
                }
                catch(Exception error){errors.Add(error.ToString());}
                finally
                {
                    Mouse(4);Key(true);Native.SetCursorPos(previous.X,previous.Y);Native.SetForegroundWindow(foreground);
                    File.WriteAllText(Path.Combine(root,"native-drag-feedback-test.json"),JsonSerializer.Serialize(new{Passed=errors.Count==0&&checks.Count==39&&checks.Values.All(x=>x),Checks=checks,Errors=errors,Frames=frames,Scope="Actual native tray fixture source, physical enter/move/leave/reenter/Esc/drop with production application and independent profile; panel-only screen sequence, no unknown app callbacks"},DiagnosticStore.JsonOptions));
                    try{app.Panel.BeginInvoke((Action)app.Shutdown);}catch(InvalidOperationException){}
                }
                void MoveToPanel()
                {
                    for(int attempt=0;attempt<3;attempt++){Point target=Point.Empty;Ui(()=>target=new Point(app.Panel.Left+app.Panel.Width/2,app.Panel.Top+app.Panel.Height/2));Move(target);Thread.Sleep(180);}
                }
                void Move(Point target)
                {
                    Native.GetCursorPos(out var from);
                    for(int step=1;step<=8;step++){Native.SetCursorPos(from.X+(target.X-from.X)*step/8,from.Y+(target.Y-from.Y)*step/8);Thread.Sleep(25);}
                }
                void Ui(Action action)
                {
                    using var done=new ManualResetEventSlim();Exception? failure=null;
                    app.Panel.BeginInvoke((Action)(()=>{try{action();}catch(Exception e){failure=e;}finally{done.Set();}}));
                    if(!done.Wait(3500))throw new TimeoutException("Native drag UI did not respond");if(failure!=null)throw failure;
                }
            }){IsBackground=true};driver.Start();
        };
        ready.Start();Application.Run(app);driver?.Join(1500);
        if(record!=null)NativeTray.PostMessage(record.Window,0x10,0,0);
        if(!fixture.WaitForExit(2500)){fixture.Kill();fixture.WaitForExit(1000);}
        return errors.Count==0&&checks.Count==39&&checks.Values.All(x=>x)?0:1;
    }
    private static void Mouse(uint flags){var input=new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=flags}};if(Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>())!=1)throw new InvalidOperationException("Own drag input failed");}
    private static void Key(bool up){var input=new Native.Input{Type=1,Keyboard=new Native.KeyInput{Key=0x1B,Flags=up?2u:0u}};Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>());}
}
