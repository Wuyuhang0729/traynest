using System.Windows.Automation;

namespace NestedTray;

internal sealed record NativePanelSnapshot(nint Window,Rectangle Host,Rectangle Surface,NativeTrayLayout Layout,bool Measured);

// Cross-process accessibility work never runs on the animation/drag thread.
internal sealed class NativePanelGeometry:IDisposable
{
    private Task<NativePanelSnapshot?>? measuring;
    private int cycle, measuringCycle;
    private NativePanelSnapshot? latest;
    private NativePanelSnapshot? candidate;
    private nint observedWindow;
    private Rectangle observedHost;
    private long visibleSince,stableSince,nextSample,refreshUntil;
    private bool disposed;
    internal const int NativeEntranceGuardMs=367;
    internal NativePanelSnapshot? Latest=>latest;
    internal long VisibleSince=>visibleSince;
    internal void Refresh()=>refreshUntil=Environment.TickCount64+700;
    internal void InterruptMeasurement()
    {
        // Keep the last settled surface. Discard samples spanning a direction change.
        cycle++;
        candidate=null;
        stableSince=visibleSince=Environment.TickCount64;
        Refresh();
    }
    internal void Tick(bool allowMeasurement)
    {
        if(disposed)return;
        long now=Environment.TickCount64;nint window=NativeOverflow.Window;
        if(window==0||!Native.IsWindowVisible(window)||!Native.GetWindowRect(window,out var raw))
        {observedWindow=0;visibleSince=0;return;}
        var host=Native.Rect(raw);
        if(window!=observedWindow){cycle++;candidate=null;observedWindow=window;observedHost=host;visibleSince=now;stableSince=now;Refresh();}
        if(observedHost!=host){observedHost=host;stableSince=now;Refresh();}
        if(measuring is {IsCompleted:true})
        {
            var sample=measuring.GetAwaiter().GetResult();measuring=null;
            if(measuringCycle==cycle&&sample!=null&&sample.Window==window&&sample.Host==host)
            {
                // Explorer reports moving icon bounds inside a stationary host during a slide.
                if(candidate==sample)latest=sample;
                else stableSince=now;
                candidate=sample;
            }
        }
        if(!allowMeasurement||measuring!=null||now-visibleSince<NativeEntranceGuardMs||now<nextSample||now>refreshUntil&&latest is {Measured:true}&&latest.Host==host)return;
        measuringCycle=cycle;nextSample=now+65;measuring=Task.Run(()=>Measure(window,host));
    }
    internal NativePanelSnapshot? CachedOrEstimate()
    {
        nint window=NativeOverflow.Window;
        if(window==0||!Native.IsWindowVisible(window)||!Native.GetWindowRect(window,out var raw))return null;
        var host=Native.Rect(raw);
        if(latest is {Measured:true}&&latest.Window==window&&latest.Host.Size==host.Size)
        {var surface=latest.Surface;surface.Offset(host.Left-latest.Host.Left,host.Top-latest.Host.Top);return latest with{Host=host,Surface=surface};}
        var layout=NativeTrayLayout.Measure(window,Native.GetDpiForWindow(window),false);
        // Derive the transparent host margin from the native grid, not the DWM host bounds.
        int left=host.Left+(host.Width-layout.Surface.Width)/2,top=host.Top+(host.Height-layout.Surface.Height)/2;
        return new(window,host,new Rectangle(left,top,layout.Surface.Width,layout.Surface.Height),layout,false);
    }
    internal async Task<NativePanelSnapshot?> PrepareAsync(Func<bool> cancelled)
    {
        Refresh();long began=Environment.TickCount64;
        while(!disposed&&!cancelled())
        {
            Tick(true);if(observedWindow==0)return null;
            long now=Environment.TickCount64;
            if(latest is {Measured:true}&&latest.Window==observedWindow&&latest.Host==observedHost&&now-visibleSince>=NativeEntranceGuardMs&&now-stableSince>=80)return latest;
            if(now-began>=1200)return CachedOrEstimate();
            await Task.Delay(16);
        }
        return null;
    }
    private static NativePanelSnapshot? Measure(nint window,Rectangle host)
    {
        try
        {
            uint dpi=Native.GetDpiForWindow(window);int padding=(int)Math.Round(4*Math.Max(96,dpi)/96.0);
            var request=new CacheRequest{TreeScope=TreeScope.Element};
            foreach(var property in new[]{AutomationElement.BoundingRectangleProperty,AutomationElement.IsOffscreenProperty,AutomationElement.AutomationIdProperty,AutomationElement.ControlTypeProperty})request.Add(property);
            AutomationElementCollection nodes;
            using(request.Activate())nodes=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new OrCondition(new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Image)));
            var cells=new List<Rectangle>();var imageSizes=new List<int>();
            foreach(AutomationElement node in nodes)
            {
                var value=node.Cached;var rect=value.BoundingRectangle;
                if(value.IsOffscreen||rect.IsEmpty||Math.Abs(rect.Width-rect.Height)>1)continue;
                if(value.AutomationId=="NotifyItemIcon"&&rect.Width>=20&&rect.Width<=100)cells.Add(Rectangle.FromLTRB((int)Math.Floor(rect.Left),(int)Math.Floor(rect.Top),(int)Math.Ceiling(rect.Right),(int)Math.Ceiling(rect.Bottom)));
                if(value.ControlType==ControlType.Image&&rect.Width>=8&&rect.Width<=64)imageSizes.Add((int)Math.Round(rect.Width));
            }
            var surface=NativeOverflow.SurfaceFromCells(cells,padding);
            if(cells.Count==0||!host.Contains(surface)||!Native.GetWindowRect(window,out var final)||Native.Rect(final)!=host)return null;
            int cell=cells.Select(value=>value.Width).Order().ElementAt(cells.Count/2);
            int icon=imageSizes.Count>0?imageSizes.Order().ElementAt(imageSizes.Count/2):Native.GetSystemMetricsForDpi(49,Math.Max(96,dpi));
            return new(window,host,surface,new NativeTrayLayout(icon,cell,padding,surface.Size),true);
        }
        catch(Exception error)when(error is System.Runtime.InteropServices.COMException or ElementNotAvailableException or InvalidOperationException){return null;}
    }
    public void Dispose()=>disposed=true;
}
