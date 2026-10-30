using System.Runtime.InteropServices;

namespace NestedTray;
internal sealed class DragObserver:IDisposable
{
    private readonly Native.MouseProcedure callback;
    private nint hook;
    private readonly Thread thread;
    private uint threadId;
    internal Action<Point,nint,bool>? Down;
    internal Action<Point,nint>? OtherDown;
    internal Action<Point>? Up;
    internal DragObserver(Control dispatcher)
    {
        callback=(code,wp,lp)=>
        {
            try
            {
                if(code>=0&&wp is 0x201 or 0x202 or 0x204)
                {
                    var data=Marshal.PtrToStructure<Native.MouseData>(lp);
                    if(!dispatcher.IsDisposed) {
                        var point=new Point(data.Point.X,data.Point.Y);bool down=wp is 0x201 or 0x204,right=wp==0x204;
                        // Capture before Explorer handles the press or creates its drag window.
                        nint sourceRoot=down?Native.GetAncestor(Native.WindowFromPoint(data.Point),2):0;
                        bool nativeVisible=down&&NativeOverflow.Visible;
                        dispatcher.BeginInvoke((Action)(()=>{if(right)OtherDown?.Invoke(point,sourceRoot);else if(down)Down?.Invoke(point,sourceRoot,nativeVisible);else Up?.Invoke(point);}));
                    }
                }
            }
            catch(InvalidOperationException){}
            return Native.CallNextHookEx(0,code,wp,lp);
        };
        using var ready=new ManualResetEventSlim();Exception? failure=null;
        thread=new Thread(()=>
        {
            threadId=Native.GetCurrentThreadId();
            hook=Native.SetWindowsHookExW(14,callback,Native.GetModuleHandleW(null),0);
            if(hook==0)failure=new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            ready.Set();if(hook!=0)Application.Run();
        }){IsBackground=true,Name="NestedTray mouse observation"};thread.SetApartmentState(ApartmentState.STA);thread.Start();ready.Wait();
        if(failure!=null)throw failure;
    }
    public void Dispose(){if(hook!=0){Native.UnhookWindowsHookEx(hook);hook=0;}if(threadId!=0)Native.PostThreadMessageW(threadId,0x12,0,0);thread.Join(1000);GC.KeepAlive(callback);}
}
