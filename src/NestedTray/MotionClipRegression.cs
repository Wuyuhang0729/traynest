using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class MotionClipRegression
{
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]
    private static extern int ReadCloak(nint window,uint attribute,out int value,int size);
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var errors=new List<string>();var frames=new List<object>();
        try
        {
            using var panel=new TrayPanel{FixedColumns=5,Placement=PopupPlacement.Above,EntryRectangle=new Rectangle(300,450,16,16),DismissSettings=new(){DurationMs=600}};
            using var record=new IconRecord{Selected=true,Key="clip",Image=SystemIcons.Information.ToBitmap()};panel.Render([record]);
            foreach(var mode in new[]{PanelAppearance.Acrylic,PanelAppearance.Dark,PanelAppearance.Light})
            {
                panel.SetAppearance(mode);bool firstHeld=true,allBounded=true,nativeStepsBounded=true;int count=0,nativeSteps=0,shows=0;
                bool PhysicalBounds()
                {
                    if(!Native.IsWindowVisible(panel.Handle))return true;
                    int edge=Math.Min(panel.MotionOriginEdge,Screen.FromPoint(panel.Location).WorkingArea.Bottom);
                    Native.GetWindowRect(panel.Handle,out var actual);
                    int result=Native.DwmGetWindowAttribute(panel.Handle,9,out var frame,16);
                    bool bounded=actual.Bottom<=edge&&(result<0||frame.Bottom<=edge);
                    if(!bounded)frames.Add(new{Phase="physical-failure",Mode=mode.ToString(),edge,Actual=Native.Rect(actual).ToString(),Dwm=Native.Rect(frame).ToString(),panel.IsOpening,panel.IsDismissing});
                    return bounded;
                }
                void NativeStep(object? sender,EventArgs e)
                {
                    if(!panel.Visible||!panel.IsAnimating)return;
                    nativeStepsBounded&=PhysicalBounds();nativeSteps++;
                }
                void Visible(object? sender,EventArgs e)
                {
                    if(!panel.Visible)return;
                    firstHeld&=ReadCloak(panel.Handle,14,out int cloak,4)>=0&&(cloak&1)!=0&&PhysicalBounds();shows++;
                }
                void Frame(Point position,long time)
                {
                    bool bounded=PhysicalBounds();allBounded&=bounded;count++;
                    frames.Add(new{Mode=mode.ToString(),panel.IsOpening,panel.IsDismissing,position.Y,Height=panel.Height,Edge=panel.MotionOriginEdge,Bounded=bounded});
                }
                panel.LocationChanged+=NativeStep;panel.SizeChanged+=NativeStep;
                panel.VisibleChanged+=Visible;panel.MotionFrame+=Frame;panel.Open();
                checks[mode+"-ZeroExposureNotShown"]=!Native.IsWindowVisible(panel.Handle)&&panel.IsOpening&&panel.WantsVisible;
                panel.Dismiss();checks[mode+"-PendingShowCanCancel"]=!panel.IsOpening&&!Native.IsWindowVisible(panel.Handle);
                panel.Open();Pump(800);
                checks[mode+"-FirstNativeShowHeld"]=firstHeld&&shows>0;
                checks[mode+"-SettledSurfaceUncloaked"]=ReadCloak(panel.Handle,14,out int settled,4)>=0&&settled==0;
                panel.Dismiss();Pump(65);panel.Open();Pump(750);panel.Dismiss();Pump(300);
                panel.VisibleChanged-=Visible;panel.MotionFrame-=Frame;
                panel.LocationChanged-=NativeStep;panel.SizeChanged-=NativeStep;
                checks[mode+"-AllMovingPhysicalFramesBounded"]=allBounded&&count>=4;
                checks[mode+"-NativeBoundsNotificationsRemainBounded"]=nativeStepsBounded&&nativeSteps>=2;
                checks[mode+"-HiddenSurfaceCloaked"]=ReadCloak(panel.Handle,14,out int hidden,4)>=0&&(hidden&1)!=0;
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count==0&&checks.Values.All(x=>x);
        File.WriteAllText(Path.Combine(directory,"motion-clip-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Frames=frames,Errors=errors,Scope="Own production panel, actual Win32/DWM physical bounds at visibility, native bounds notifications and reversing frames; DWM cloak at native Show/hidden states. Supersedes experimental per-frame HRGN checks; does not prove the natural strip is absent."},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Pump(int ms){var c=Stopwatch.StartNew();while(c.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
}
