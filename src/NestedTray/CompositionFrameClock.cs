using System.Runtime.InteropServices;

namespace NestedTray;

// Wait for composition frames off the UI thread. At most one frame message is queued.
internal sealed class CompositionFrameClock:IDisposable
{
    internal const uint Message=0x8000+854;
    private readonly AutoResetEvent wake=new(false);
    private readonly nint window;
    private readonly object sync=new();
    private volatile bool active,disposed;
    private int pending;
    [DllImport("dwmapi.dll")]private static extern int DwmFlush();
    [DllImport("user32.dll")]private static extern bool PostMessageW(nint window,uint message,nuint w,nint l);
    internal CompositionFrameClock(nint window)
    {
        this.window=window;
        new Thread(Run){IsBackground=true,Name="TrayNest composition frames"}.Start();
    }
    internal void Start(){lock(sync){if(disposed)return;active=true;wake.Set();}}
    internal void Stop()=>active=false;
    internal void Consume()=>Interlocked.Exchange(ref pending,0);
    private void Run()
    {
        try
        {
            while(true)
            {
                wake.WaitOne();if(disposed)return;
                while(active&&!disposed)
                {
                    long before=System.Diagnostics.Stopwatch.GetTimestamp();
                    if(DwmFlush()<0)Thread.Sleep(8);
                    else if(System.Diagnostics.Stopwatch.GetElapsedTime(before).TotalMilliseconds<2)Thread.Sleep(2);
                    if(!active||disposed)break;
                    if(Interlocked.CompareExchange(ref pending,1,0)==0&&!PostMessageW(window,Message,0,0))
                    {active=false;Interlocked.Exchange(ref pending,0);}
                }
            }
        }
        finally{lock(sync)wake.Dispose();}
    }
    public void Dispose(){lock(sync){if(disposed)return;disposed=true;active=false;wake.Set();}}
}
