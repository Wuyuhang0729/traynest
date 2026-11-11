using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class DismissAnimationRegression
{
    internal static int Run(string directory)
    {
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",directory);Directory.CreateDirectory(directory);
        var r=new Dictionary<string,object?>{["SystemClientAnimation"]=SystemAnimation.Read(0x1042),["SystemMenuAnimation"]=SystemAnimation.Read(0x1002),["SystemMenuFade"]=SystemAnimation.Read(0x1012),["NativeDurationOrCurveAvailable"]=false};
        using var panel=new TrayPanel();var work=Screen.PrimaryScreen!.WorkingArea;panel.EntryRectangle=new Rectangle(work.Right-40,work.Bottom-8,16,16);
        var approved=panel.DismissSettings;
        r["WindowsPopupProfile"]=approved.Enabled&&approved.DurationMs==250&&!approved.Fade&&approved.Curve=="Windows弹出"&&approved.ClosingDuration==215;
        r["BezierSolvesTimeAxis"]=Math.Abs(DismissOptions.CubicBezier(.2375,.1,.9,.2,1)-.8375)<.00001;
        var frames=new List<(Point Point,long Time)>();panel.MotionFrame+=(point,time)=>frames.Add((point,time));
        panel.Render([]);nint beforeOpenForeground=Native.GetForegroundWindow();panel.Open();Point openingStart=panel.Location;nint openingHandle=panel.Handle;
        // Zero-exposure frames now stay hidden and clipped above the taskbar.
        bool startsUnexposed=!Native.IsWindowVisible(openingHandle)&&panel.WantsVisible;
        if(SystemAnimation.Enabled)
        {
            r["OpeningBeginsUnexposed"]=panel.IsOpening&&!panel.IsDismissing&&startsUnexposed;
            Pump(65);r["OpeningSlidesFromEdge"]=panel.Top<openingStart.Y&&panel.Opacity==1;
            r["OpeningPreservesHandleAndDragAvailability"]=panel.Handle==openingHandle&&panel.Enabled;
        }
        Pump(400);Point approvedHome=panel.Location;
        r["OpeningFinishesAtOriginalPosition"]=panel.Visible&&!panel.IsOpening&&!panel.IsDismissing;
        if(SystemAnimation.Enabled)
        {
            r["OpeningStartsWithoutTaskbarExposure"]=startsUnexposed&&panel.MotionOriginEdge==work.Bottom;
            r["OpeningFollowsOneMonotonePath"]=frames.Count>=6&&frames.All(frame=>frame.Point.X==approvedHome.X)&&frames.Zip(frames.Skip(1)).All(pair=>pair.Second.Point.Y<=pair.First.Point.Y);
            var gaps=frames.Zip(frames.Skip(1)).Select(pair=>Stopwatch.GetElapsedTime(pair.First.Time,pair.Second.Time).TotalMilliseconds).Order().ToArray();
            r["OpeningFrameCount"]=frames.Count;r["OpeningFrameGapP95Ms"]=gaps.Length>0?gaps[(int)((gaps.Length-1)*.95)]:0;
            Pump(350);r["NoLateAlignmentSnap"]=panel.Location==approvedHome;
        }
        r["OpeningDoesNotStealForeground"]=Native.GetForegroundWindow()==beforeOpenForeground;
        panel.Dismiss();Pump(70);
        if(SystemAnimation.Enabled)r["PopupSlidesBackToEdge"]=panel.IsDismissing&&panel.Top>approvedHome.Y&&panel.Opacity==1;
        Pump(160);r["ApprovedAnimationFinishes"]=!panel.Visible&&!panel.IsDismissing;
        panel.Render([]);panel.Open();Pump(430);nint handle=panel.Handle,foreground=Native.GetForegroundWindow();
        panel.DismissSettings=new DismissOptions{DurationMs=180,DistanceDip=36,Fade=true,Curve="加速下沉"};Point home=panel.Location;
        panel.Dismiss();r["StartsWithoutWaitingForNativeHidden"]=panel.IsDismissing||!panel.Visible;
        if(SystemAnimation.Enabled)
        {
            r["ClosingIsNotInteractive"]=!panel.Enabled;
            Pump(75);r["MovesDownDuringClose"]=panel.Top>home.Y;r["FadesDuringClose"]=panel.Opacity<1;
            Pump(160);
        }
        r["FullyHiddenAfterDuration"]=!panel.Visible&&!panel.IsDismissing;
        r["StateRestoredAfterClose"]=panel.Location==home&&panel.Opacity==1&&panel.Enabled;
        r["HandlePreservedForDropReceiver"]=panel.Handle==handle;
        r["DoesNotStealForeground"]=Native.GetForegroundWindow()==foreground;
        panel.Open();panel.Dismiss();Pump(20);panel.Open();Pump(220);
        r["ReopenCancelsOldClose"]=panel.Visible&&!panel.IsDismissing&&panel.Enabled&&panel.Opacity==1;
        panel.Hide();panel.Open();Pump(35);Point interrupted=panel.Location;panel.Dismiss();
        r["ClosingDuringOpeningDoesNotJump"]=panel.Location==interrupted;Pump(220);
        r["ClosingDuringOpeningCompletes"]=!panel.Visible&&!panel.IsOpening&&!panel.IsDismissing;
        panel.DismissSettings=new DismissOptions{DurationMs=0};panel.Open();r["ZeroDurationOpensImmediately"]=panel.Visible&&!panel.IsOpening;
        panel.DismissSettings.DurationMs=0;panel.Dismiss();r["ZeroDurationHidesImmediately"]=!panel.Visible;
        panel.DismissSettings=new DismissOptions{Enabled=false};panel.Open();r["DisabledAnimationOpensImmediately"]=panel.Visible&&!panel.IsOpening;
        panel.Dismiss();r["DisabledAnimationHidesImmediately"]=!panel.Visible;
        r["TemporaryTunerRemoved"]=typeof(TrayPanel).Assembly.GetType("NestedTray.AnimationTuner")==null;
        bool pass=r.Where(value=>value.Key!="NativeDurationOrCurveAvailable"&&!value.Key.StartsWith("System")).All(value=>value.Value is not false);
        r["Passed"]=pass;File.WriteAllText(Path.Combine(directory,"dismiss-animation-test.json"),JsonSerializer.Serialize(r,new JsonSerializerOptions{WriteIndented=true}));return pass?0:1;
    }
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
}
