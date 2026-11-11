using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class PresentationRegression
{
    internal static int Run(string root)
    {
        Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(root));
        new Settings{FollowNativeOverflow=true,ExpansionOrder=NativeExpansionOrder.AfterNative,Placement=PopupPlacement.Above,Appearance=PanelAppearance.Acrylic}.Save();
        var checks=new Dictionary<string,bool>();var errors=new List<string>();var metrics=new Dictionary<string,object>();
        using var app=new TrayApplication(true);using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,60000,Timeout.Infinite);
        try
        {
            CloseNative();app.Panel.Hide();Pump(200);
            var samples=new List<(Point Point,long Time,double Opacity)>();var visibleMoves=new List<Point>();
            bool track=true;long shownAt=0;var clock=Stopwatch.StartNew();
            
            app.Panel.VisibleChanged+=(_,_)=>{if(track&&app.Panel.Visible)shownAt=clock.ElapsedMilliseconds;};
            app.Panel.LocationChanged+=(_,_)=>{if(track&&app.Panel.Visible)visibleMoves.Add(app.Panel.Location);};
            app.Panel.MotionFrame+=(point,time)=>
            {
                if(!track||!app.Panel.IsOpening)return;
                samples.Add((point,time,app.Panel.Opacity));
            };
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(180);
            checks["DoesNotAppearDuringNativeEntrance"]=!app.Panel.Visible&&app.Panel.IsPreparing;
            Wait(()=>app.Panel.Visible&&!app.Panel.IsAnimating,2200);
            checks["WaitsForNativeEntranceAndMeasuredAnchor"]=shownAt>=NativePanelGeometry.NativeEntranceGuardMs&&app.Panel.HasMeasuredAnchor;
            Point final=app.Panel.Location;var surface=NativeOverflow.SurfaceBounds(NativeOverflow.Window,(int)Math.Round(4*app.Panel.DeviceDpi/96.0));
            checks["FirstEntranceAlreadyTargetsFinalAlignment"]=app.Panel.Left==surface.Left&&app.Panel.Bottom==surface.Top-8;
            checks["OneContinuousEntrancePath"]=samples.Count>=6&&samples.All(frame=>frame.Point.X==final.X)&&samples.Zip(samples.Skip(1)).All(pair=>pair.Second.Point.Y<=pair.First.Point.Y);
            var gaps=samples.Zip(samples.Skip(1)).Select(pair=>Stopwatch.GetElapsedTime(pair.First.Time,pair.Second.Time).TotalMilliseconds).Order().ToArray();
            metrics["FrameCount"]=samples.Count;metrics["FrameGapMedianMs"]=gaps.Length>0?gaps[gaps.Length/2]:0;metrics["FrameGapP95Ms"]=gaps.Length>0?gaps[(int)((gaps.Length-1)*.95)]:0;metrics["FrameGapMaxMs"]=gaps.Length>0?gaps[^1]:0;metrics["ShownAfterMs"]=shownAt;
            checks["NoLongFrameStallDuringEntrance"]=gaps.Length>0&&gaps[(int)((gaps.Length-1)*.95)]<45&&gaps[^1]<90;
            int moves=visibleMoves.Count;Pump(750);checks["NoSecondAlignmentAfterEntrance"]=app.Panel.Location==final&&visibleMoves.Count==moves;
            using(var screenshot=new Bitmap(app.Panel.Width,app.Panel.Height)){using(var graphics=Graphics.FromImage(screenshot))graphics.CopyFromScreen(app.Panel.Location,Point.Empty,app.Panel.Size);screenshot.Save(Path.Combine(root,"settled-panel.png"));}
            track=false;app.Entry.Open?.Invoke();Pump(20);var interrupted=app.Panel.Location;double opacity=app.Panel.Opacity;app.Entry.Open?.Invoke();
            checks["QuickReopenDoesNotJumpOrFlash"]=app.Panel.Location==interrupted&&Math.Abs(app.Panel.Opacity-opacity)<.02;
            Wait(()=>!app.Panel.IsAnimating,1500);CloseNative();Pump(180);
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(100);CloseNative();Pump(700);
            checks["ClosingNativeCancelsPendingEntrance"]=!app.Panel.Visible&&!app.Panel.IsPreparing;
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(80);app.Entry.Open?.Invoke();Pump(800);
            checks["ManualCloseCancelsPendingEntrance"]=!app.Panel.Visible&&!app.Panel.IsPreparing;
            app.Panel.Open(true);checks["DragPreviewDoesNotWaitForAccessibility"]=app.Panel.Visible&&!app.Panel.IsPreparing&&app.Panel.Opacity==1;
            Pump(250);CloseNative();Pump(160);
            app.OwnMenu.Items.OfType<ToolStripMenuItem>().Single().PerformClick();Pump(100);
            var window=Application.OpenForms.OfType<SettingsWindow>().Single();window.SelectPage(0);
            WebSettingsTest.Page(window,1);
            checks["OrderSelectorVisibleForFollow"]=WebSettingsTest.Visible(window,"RowNativeExpansionOrder");
            WebSettingsTest.Set(window,"NativeExpansionOrder",0);checks["SimultaneousSelectionPersists"]=Settings.Load().ExpansionOrder==NativeExpansionOrder.Simultaneous;
            window.WindowState=FormWindowState.Minimized;CloseNative();Pump(180);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(65);
            checks["SimultaneousAppearsWithoutSequentialWait"]=app.Panel.Visible&&!app.Panel.IsPreparing;
            CloseNative();Pump(300);
            bool origin=true,layer=true;int openingFrames=0,exitsAtOrigin=0;var upperTrace=new List<object>();
            app.Panel.VisibleChanged+=(_,_)=>{
                if(app.Panel.Visible&&app.Panel.Placement==PopupPlacement.Above)
                    origin&=app.Panel.Bottom<=app.Panel.MotionOriginEdge&&app.Panel.MotionOriginEdge>=Screen.FromHandle(app.Panel.Handle).WorkingArea.Bottom-8;
            };
            app.Panel.MotionFrame+=(_,_)=>{
                if(app.Panel.Placement!=PopupPlacement.Above)return;
                upperTrace.Add(new{Time=Stopwatch.GetTimestamp(),Bounds=app.Panel.Bounds.ToString(),Opening=app.Panel.IsOpening,Closing=app.Panel.IsDismissing,Origin=app.Panel.MotionOriginEdge,NativeVisible=NativeOverflow.Visible});
                if(app.Panel.IsDismissing&&app.Panel.Bottom==app.Panel.MotionOriginEdge&&app.Panel.Height<=2)exitsAtOrigin++;
                if(!NativeOverflow.Visible)return;
                openingFrames++;
                bool found=false;
                for(nint handle=Native.GetWindow(NativeOverflow.Window,2);handle!=0;handle=Native.GetWindow(handle,2))
                    if(handle==app.Panel.Handle){found=true;break;}
                layer&=found;
            };
            var settledOffsets=new List<int>(); metrics["RepeatedOpenOffsetsPx"]=settledOffsets;
            foreach(var placement in new[]{PopupPlacement.Left,PopupPlacement.Above})
            {
                app.Panel.SetPlacement(placement);
                for(int i=0;i<4;i++)
                {
                    Wait(()=>!NativeOverflow.Visible,1600); Pump(60); NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);
                    Wait(()=>app.Panel.Visible&&!app.Panel.IsAnimating,1800);
                    Pump(180); // Compare settled edges, after Explorer finishes its own entrance.
                    var edge=NativeOverflow.SurfaceBounds(NativeOverflow.Window,(int)Math.Round(4*app.Panel.DeviceDpi/96.0));
                    settledOffsets.Add(placement==PopupPlacement.Left?app.Panel.Bottom-edge.Bottom:app.Panel.Bottom-edge.Top+8);
                    if(i==1)
                    {
                        var resting=app.Panel.Location;
                        if(placement==PopupPlacement.Above)
                        {
                            app.OwnMenu.Show(new Point(app.Panel.Left+12,app.Panel.Top+12));Pump(60);
                            var point=new Native.Point{X=app.OwnMenu.Left+10,Y=app.OwnMenu.Top+10};
                            checks["UpperPanelMenuRemainsInFront"]=app.OwnMenu.Visible&&app.Panel.Visible&&Native.GetAncestor(Native.WindowFromPoint(point),2)==app.OwnMenu.Handle;
                            app.OwnMenu.Close();
                            var rapid=new ScreenMotionProbe(app.Panel.Bounds,edge,Screen.FromRectangle(edge).WorkingArea.Bottom);
                            rapid.Record(Path.Combine(root,"desktop-rapid-Above.csv"),()=>{app.Panel.Dismiss();Pump(90);app.Panel.Open(simultaneous:true);Pump(35);app.Panel.Dismiss();Pump(45);app.Panel.Open(simultaneous:true);});
                            Wait(()=>!app.Panel.IsAnimating,1200);
                        }
                        app.Panel.Dismiss();Pump(55);app.Panel.Open(simultaneous:true);Pump(25);
                        var repeated=app.Panel.Location;app.Panel.Open(simultaneous:true);
                        checks["RepeatedTriggerPreservesMotion"+placement]=app.Panel.Location==repeated&&app.Panel.IsOpening;
                        Wait(()=>!app.Panel.IsAnimating,1200);
                        foreach(int delay in new[]{55,90,135})
                        {
                            app.Panel.Dismiss();Pump(delay);
                            if(!app.Panel.IsDismissing)throw new InvalidOperationException("Expected an unfinished exit");
                            var moving=app.Panel.Location;
                            app.Panel.Open(simultaneous:true);
                            if(app.Panel.Location!=moving)throw new InvalidOperationException("Reversal jumped");
                            Wait(()=>!app.Panel.IsAnimating,1200);
                            if(app.Panel.Location!=resting)throw new InvalidOperationException("Reversal changed the resting edge");
                        }
                        // Explorer can ignore a toggle during its own exit. Reopen the
                        // moment its window hides while the collection is still closing.
                        CloseNative();Wait(()=>!NativeOverflow.Visible,1200);
                        Wait(()=>app.Panel.IsDismissing||!app.Panel.Visible,300);
                        metrics["CollectionClosingAtNativeReopen"+placement]=app.Panel.IsDismissing;
                        ToggleNative();
                        Wait(()=>NativeOverflow.Visible&&app.Panel.Visible&&!app.Panel.IsAnimating,2000);
                        Pump(180);
                        if(app.Panel.Location!=resting)throw new InvalidOperationException("Native reversal shifted the resting edge");
                    }
                    if(i==2)
                    {
                        var probe=new ScreenMotionProbe(app.Panel.Bounds,edge,Screen.FromRectangle(edge).WorkingArea.Bottom);
                        probe.Record(Path.Combine(root,$"desktop-exit-{placement}.csv"),CloseNative);
                        Wait(()=>!NativeOverflow.Visible,1500);
                        probe.Record(Path.Combine(root,$"desktop-entrance-{placement}.csv"),()=>NativeOverflow.TryOpen(app.Bridge.ExplorerWindow));
                    }
                    CloseNative();Pump(320);
                }
            }
            metrics["RepeatedOpenOffsetsPx"]=settledOffsets;
            File.WriteAllText(Path.Combine(root,"upper-motion-frames.json"),JsonSerializer.Serialize(upperTrace,DiagnosticStore.JsonOptions));
            checks["UpperExitReturnsToSameOrigin"]=exitsAtOrigin>=4;
            checks["UpperEntranceStartsAtTrayEntry"]=origin;
            checks["NativePanelInFrontDuringMotion"]=layer&&openingFrames>=6;
            checks["RepeatedOpenMatchesNativeEdge"]=settledOffsets.All(offset=>offset==0);
            window.WindowState=FormWindowState.Normal;window.Activate();WebSettingsTest.Set(window,"NativeExpansionOrder",1);
            WebSettingsTest.Set(window,"ExpansionMode",2);
            checks["OrderSelectorHiddenOutsideFollow"]=!WebSettingsTest.Visible(window,"RowNativeExpansionOrder");
            window.WindowState=FormWindowState.Minimized;CloseNative();Pump(160);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(50);app.Entry.Open?.Invoke();
            checks["ManualOpenIgnoresSequentialOrder"]=app.Panel.WantsVisible&&!app.Panel.IsPreparing;
            CloseNative();Pump(160);window.Close();
        }
        catch(Exception error){errors.Add(error.ToString());metrics["NativeVisibleAtFailure"]=NativeOverflow.Visible;metrics["CollectionVisibleAtFailure"]=app.Panel.Visible;metrics["CollectionAnimatingAtFailure"]=app.Panel.IsAnimating;}
        bool passed=errors.Count==0&&checks.Count>=22&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(root,"presentation-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Metrics=metrics,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;
        void CloseNative()
        {
            if(!NativeOverflow.Visible)return;
            ToggleNative();
        }
        void ToggleNative()
        {
            var nodes=AutomationElement.FromHandle(app.Bridge.ExplorerWindow).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
            foreach(AutomationElement node in nodes)if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons")((InvokePattern)node.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
    }
    private static void Wait(Func<bool> ready,int timeout){var clock=Stopwatch.StartNew();while(!ready()&&clock.ElapsedMilliseconds<timeout)Pump(10);if(!ready())throw new TimeoutException("Presentation did not settle");}
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(2);}}
}
