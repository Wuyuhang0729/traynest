using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class PresentationHandoffRegression
{
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]
    private static extern int ReadCloak(nint window,uint attribute,out int value,int size);
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();var samples=new List<object>();var timing=new List<object>();
        var records=Enumerable.Range(0,14).Select(i=>new IconRecord{Key="handoff:"+i,Selected=true,Image=SystemIcons.Information.ToBitmap()}).ToArray();
        try
        {
            using var anchor=new Form{ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(80,500,520,40),TopMost=true};
            anchor.Show();
            using var panel=new TrayPanel{FixedColumns=5,Placement=PopupPlacement.Above,EntryRectangle=new Rectangle(310,500,16,16),TaskbarWindow=anchor.Handle};
            panel.Render(records);
            foreach(var mode in new[]{PanelAppearance.Acrylic,PanelAppearance.Dark,PanelAppearance.Light})
            {
                panel.SetAppearance(mode);bool prepared=true,layer=true,bounded=true,ordinary=true;int shows=0,frames=0,topmostFrames=0;
                var intervals=new List<double>();long previous=0;bool previousOpening=false;
                bool BehindAnchor()
                {
                    for(nint h=Native.GetWindow(anchor.Handle,2);h!=0;h=Native.GetWindow(h,2))if(h==panel.Handle)return true;
                    return false;
                }
                void Visible(object? sender,EventArgs args)
                {
                    if(!panel.Visible)return;
                    int hr=ReadCloak(panel.Handle,14,out int cloak,4);
                    bool behind=BehindAnchor();shows++;
                    // A native Show is not yet safe to compose: WinForms has
                    // just raised its topmost window ahead of the anchor.
                    prepared&=hr>=0&&(cloak&1)!=0;
                    samples.Add(new{Mode=mode.ToString(),Phase="native-show",Cloak=cloak,Behind=behind,panel.Bounds});
                }
                void Frame(Point point,long time)
                {
                    if(!panel.Visible)return;
                    if(previous!=0&&previousOpening==panel.IsOpening)intervals.Add(Stopwatch.GetElapsedTime(previous,time).TotalMilliseconds);
                    previous=time;previousOpening=panel.IsOpening;
                    ReadCloak(panel.Handle,14,out int cloak,4);
                    bool topmost=(Native.GetWindowLongPtrW(anchor.Handle,-20).ToInt64()&8)!=0;
                    bool correct=topmost?BehindAnchor():(Native.GetWindowLongPtrW(panel.Handle,-20).ToInt64()&8)!=0;
                    if(topmost)topmostFrames++;
                    layer&=cloak==0&&correct;
                    if(!correct&&samples.Count<100)samples.Add(new{Mode=mode.ToString(),Phase="layer-failure",Cloak=cloak,AnchorStyle=Native.GetWindowLongPtrW(anchor.Handle,-20).ToInt64(),PanelStyle=Native.GetWindowLongPtrW(panel.Handle,-20).ToInt64(),panel.Bounds});
                    Native.GetWindowRect(panel.Handle,out var actual);
                    bounded&=actual.Bottom<=panel.MotionOriginEdge;
                    frames++;
                }
                panel.VisibleChanged+=Visible;panel.MotionFrame+=Frame;
                // Cover both fresh native Shows and exits reversed near their
                // last thin frame, using the production duration and icon load.
                foreach(int interval in new[]{25,65,130,185,205,225,275})
                {
                    Native.SetWindowPos(anchor.Handle,new nint(-1),0,0,0,0,0x13);
                    panel.Open();Pump(310);panel.Dismiss();Pump(interval);panel.Open();Pump(310);panel.Dismiss();Pump(270);
                    ordinary&=ReadCloak(panel.Handle,14,out int hidden,4)>=0&&(hidden&1)!=0;
                }
                panel.VisibleChanged-=Visible;panel.MotionFrame-=Frame;
                checks[mode+"-NativeShowHeldUntilPrepared"]=prepared&&shows>=7;
                checks[mode+"-ComposedFramesRespectAnchorBand"]=layer&&frames>=14&&topmostFrames>=7;
                checks[mode+"-VisiblePhysicalHostBounded"]=bounded;
                checks[mode+"-HiddenSurfaceRetired"]=ordinary;
                var sorted=intervals.Order().ToArray();
                if(sorted.Length>0)timing.Add(new{Mode=mode.ToString(),Intervals=sorted.Length,MedianMs=sorted[sorted.Length/2],P95Ms=sorted[(int)Math.Floor((sorted.Length-1)*.95)],MaximumMs=sorted[^1]});
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        bool passed=errors.Count==0&&checks.Values.All(x=>x);
        File.WriteAllText(Path.Combine(directory,"presentation-handoff-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Samples=samples,Timing=timing,Errors=errors,Scope="Own anchor and 14 icons; native visibility handoff, actual DWM cloak state and physical bounds at production-duration interval reversals. No mouse, Explorer takeover or user settings. Frame intervals describe this fixture only, not perceived smoothness."},DiagnosticStore.JsonOptions));
        return passed?0:1;
    }
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
}
