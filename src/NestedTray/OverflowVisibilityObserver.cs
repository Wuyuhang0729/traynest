using System.Runtime.InteropServices;

namespace NestedTray;

// Capture a hide/show pair even when both transitions fall between timer ticks.
internal sealed class OverflowVisibilityObserver : IDisposable
{
    private readonly EventCallback callback;
    private readonly nint hook;
    private const uint Show=0x8002,Hide=0x8003;

    internal OverflowVisibilityObserver(nint explorer,Action<bool> visibilityChanged)
    {
        Native.GetWindowThreadProcessId(explorer,out uint process);
        callback=(_,kind,window,objectId,childId,_,_)=>{
            if(window!=0&&objectId==0&&childId==0&&window==NativeOverflow.Window)
                visibilityChanged(kind==Show);
        };
        if(process!=0)hook=SetWinEventHook(Show,Hide,0,callback,process,0,0);
    }

    public void Dispose()
    {
        if(hook!=0)UnhookWinEvent(hook);
        GC.KeepAlive(callback);
    }
    private delegate void EventCallback(nint hook,uint kind,nint window,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll")]private static extern nint SetWinEventHook(uint first,uint last,nint module,EventCallback callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")]private static extern bool UnhookWinEvent(nint hook);
}
