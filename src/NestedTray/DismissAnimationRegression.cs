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
        r["ApprovedParametersFixed"]=approved.Enabled&&approved.DurationMs==160&&approved.DistanceDip==24&&!approved.Fade&&approved.Curve=="加速下沉"&&approved.Strength==2.4;
        panel.Render([]);nint beforeOpenForeground=Native.GetForegroundWindow();panel.Open();Point openingStart=panel.Location;nint openingHandle=panel.Handle;
        if(SystemAnimation.Enabled)
        {
            r["OpeningBeginsBelowFinalPosition"]=panel.IsOpening&&!panel.IsDismissing&&panel.Visible;
            Pump(65);r["OpeningMovesUpWithoutFade"]=panel.Top<openingStart.Y&&panel.Opacity==1;
            r["OpeningPreservesHandleAndDragAvailability"]=panel.Handle==openingHandle&&panel.Enabled;
        }
        Pump(200);Point approvedHome=panel.Location;
        r["OpeningFinishesAtOriginalPosition"]=panel.Visible&&!panel.IsOpening&&!panel.IsDismissing;
        if(SystemAnimation.Enabled)r["OpeningUsesApprovedDistance"]=openingStart.Y-approvedHome.Y==(int)Math.Round(24*panel.DeviceDpi/96.0);
        r["OpeningDoesNotStealForeground"]=Native.GetForegroundWindow()==beforeOpenForeground;
        panel.Dismiss();Pump(70);
        if(SystemAnimation.Enabled)r["ApprovedAnimationMovesWithoutFade"]=panel.IsDismissing&&panel.Top>approvedHome.Y&&panel.Opacity==1;
        Pump(160);r["ApprovedAnimationFinishes"]=!panel.Visible&&!panel.IsDismissing;
        panel.Render([]);panel.Open();Pump(220);nint handle=panel.Handle,foreground=Native.GetForegroundWindow();
        panel.DismissSettings=new DismissOptions{DurationMs=180,DistanceDip=36,Fade=true};Point home=panel.Location;
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
