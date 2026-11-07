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
        using var app=new TrayApplication(true);using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,30000,Timeout.Infinite);
        try
        {
            CloseNative();app.Panel.Hide();Pump(200);
            var samples=new List<(Point Point,long Time,double Opacity)>();var visibleMoves=new List<Point>();
            bool track=true;long shownAt=0;var clock=Stopwatch.StartNew();
            int capturedFades=0;
            app.Panel.VisibleChanged+=(_,_)=>{if(track&&app.Panel.Visible)shownAt=clock.ElapsedMilliseconds;};
            app.Panel.LocationChanged+=(_,_)=>{if(track&&app.Panel.Visible)visibleMoves.Add(app.Panel.Location);};
            app.Panel.MotionFrame+=(point,time)=>
            {
                if(!track||!app.Panel.IsOpening)return;
                samples.Add((point,time,app.Panel.Opacity));
                if(capturedFades<3&&app.Panel.Opacity>.2+capturedFades*.25&&app.Panel.Opacity<.98)
                {
                    using var frame=new Bitmap(app.Panel.Width,app.Panel.Height);using(var graphics=Graphics.FromImage(frame))graphics.CopyFromScreen(app.Panel.Location,Point.Empty,app.Panel.Size);
                    frame.Save(Path.Combine(root,$"fade-{++capturedFades}.private.png"));
                }
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
            var order=window.Controls.Find("NativeExpansionOrder",true).OfType<ComboBox>().Single();
            checks["OrderSelectorVisibleForFollow"]=window.Controls.Find("ExpansionOrderRow",true).Single().Visible;
            order.SelectedIndex=0;checks["SimultaneousSelectionPersists"]=Settings.Load().ExpansionOrder==NativeExpansionOrder.Simultaneous;
            window.WindowState=FormWindowState.Minimized;CloseNative();Pump(180);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(65);
            checks["SimultaneousAppearsWithoutSequentialWait"]=app.Panel.Visible&&!app.Panel.IsPreparing;
            CloseNative();Pump(200);window.WindowState=FormWindowState.Normal;window.Activate();order.SelectedIndex=1;
            window.Controls.Find("ExpansionMode",true).OfType<ComboBox>().Single().SelectedIndex=2;
            checks["OrderSelectorHiddenOutsideFollow"]=!window.Controls.Find("ExpansionOrderRow",true).Single().Visible;
            window.WindowState=FormWindowState.Minimized;CloseNative();Pump(160);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(50);app.Entry.Open?.Invoke();
            checks["ManualOpenIgnoresSequentialOrder"]=app.Panel.Visible&&!app.Panel.IsPreparing;
            CloseNative();Pump(160);window.Close();
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count==0&&checks.Count==15&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(root,"presentation-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Metrics=metrics,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;
        void CloseNative()
        {
            if(!NativeOverflow.Visible)return;
            var nodes=AutomationElement.FromHandle(app.Bridge.ExplorerWindow).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
            foreach(AutomationElement node in nodes)if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons")((InvokePattern)node.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
    }
    private static void Wait(Func<bool> ready,int timeout){var clock=Stopwatch.StartNew();while(!ready()&&clock.ElapsedMilliseconds<timeout)Pump(10);if(!ready())throw new TimeoutException("Presentation did not settle");}
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(2);}}
}
