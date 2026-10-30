using System.Runtime.InteropServices;

namespace NestedTray;

// Public Windows 11 IDragDropManagerInterop ABI. IIDs and method order were
// checked against the installed Windows WinMetadata; no Explorer interfaces.
internal sealed class CoreDragReceiver:IDisposable
{
    private nint manager;
    private long token;
    private bool subscribed,roInitialized;
    private readonly CoreTargetRequested handler;
    private readonly CoreDropTarget target;
    internal string Status{get;private set;}="Not initialized";
    internal int Requests,Enters,Overs,Drops;
    internal uint LastEffect,LastAllowed;
    internal bool Registered=>subscribed;
    internal CoreDragReceiver(Control owner,Func<bool> canAccept,Action<Point> drop)
    {
        target=new CoreDropTarget(this,owner,canAccept,drop);handler=new CoreTargetRequested(this,target);
        nint name=0,factory=0;
        string stage="Initialize WinRT";
        try
        {
            int init=WinrtDrag.RoInitialize(0);if(init>=0)roInitialized=true;else if(init!=unchecked((int)0x80010106))Marshal.ThrowExceptionForHR(init);
            const string runtime="Windows.ApplicationModel.DataTransfer.DragDrop.Core.CoreDragDropManager";
            Marshal.ThrowExceptionForHR(WinrtDrag.WindowsCreateString(runtime,runtime.Length,out name));
            stage="Get interop factory";
            Guid interop=new("5AD8CBA7-4C01-4DAC-9074-827894292D63"),iid=new("7D56D344-8464-4FAF-AA49-37EA6E2D7BD1");
            Marshal.ThrowExceptionForHR(WinrtDrag.RoGetActivationFactory(name,ref interop,out factory));
            stage="Get manager for HWND";
            Marshal.ThrowExceptionForHR(WinrtDrag.Method<WinrtDrag.GetForWindow>(factory,6)(factory,owner.Handle,ref iid,out manager));
            stage="Create target handler";
            nint callback=Marshal.GetComInterfaceForObject(handler,typeof(INativeTargetRequested));
            try {stage="Subscribe target handler";Marshal.ThrowExceptionForHR(WinrtDrag.Method<WinrtDrag.AddHandler>(manager,6)(manager,callback,out token));}
            finally {Marshal.Release(callback);}
            subscribed=true;Status="Registered";
        }
        catch(Exception error)when(error is COMException or InvalidCastException or ArgumentException or NotSupportedException)
        {Status=$"Unavailable at {stage}: 0x{error.HResult:X8} {error.Message}";Dispose();}
        finally {if(factory!=0)Marshal.Release(factory);if(name!=0)WinrtDrag.WindowsDeleteString(name);}
    }
    public void Dispose()
    {
        if(subscribed&&manager!=0){WinrtDrag.Method<WinrtDrag.RemoveHandler>(manager,7)(manager,token);subscribed=false;}
        if(manager!=0){Marshal.Release(manager);manager=0;}
        if(roInitialized){WinrtDrag.RoUninitialize();roInitialized=false;}
        GC.KeepAlive(handler);GC.KeepAlive(target);
    }
}

internal static class WinrtDrag
{
    [DllImport("combase.dll")]internal static extern int RoInitialize(uint type);
    [DllImport("combase.dll")]internal static extern void RoUninitialize();
    [DllImport("combase.dll",CharSet=CharSet.Unicode)]internal static extern int WindowsCreateString(string text,int length,out nint result);
    [DllImport("combase.dll")]internal static extern int WindowsDeleteString(nint text);
    [DllImport("combase.dll")]internal static extern int RoGetActivationFactory(nint name,ref Guid iid,out nint factory);
    internal static T Method<T>(nint instance,int slot)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int GetForWindow(nint self,nint window,ref Guid iid,out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int AddHandler(nint self,nint handler,out long token);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int RemoveHandler(nint self,long token);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int SetTarget(nint self,nint target);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int GetUInt(nint self,out uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int SetBoolean(nint self,byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int SetString(nint self,nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int Complete(nint self,nint operation,uint status);
    internal static uint Allowed(nint info)
    {
        Guid iid=new("C54691E5-E6FB-4D74-B4B1-8A3C17F25E9E");
        if(Marshal.QueryInterface(info,in iid,out nint extra)<0)return 1;
        try {return Method<GetUInt>(extra,6)(extra,out uint allowed)>=0?allowed:0;}
        finally {Marshal.Release(extra);}
    }
    internal static void Caption(nint dragUi,bool accepted)
    {
        if(dragUi==0)return;
        string text=accepted?"收纳到面板":"此图标尚不可收纳";
        if(WindowsCreateString(text,text.Length,out nint value)<0)return;
        try {Method<SetString>(dragUi,11)(dragUi,value);Method<SetBoolean>(dragUi,13)(dragUi,1);Method<SetBoolean>(dragUi,15)(dragUi,1);}
        finally {WindowsDeleteString(value);}
    }
}

[ComVisible(true),Guid("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeInspectable
{
    [PreserveSig]int GetIids(out uint count,out nint values);
    [PreserveSig]int GetRuntimeClassName(out nint name);
    [PreserveSig]int GetTrustLevel(out int level);
}
[ComVisible(true),Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeAgile { }

[ComVisible(true),Guid("A4C3B1C1-B8AD-58CB-ACC0-8EF37EAE4ED4"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeTargetRequested { [PreserveSig]int Invoke(nint sender,nint args); }

[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class CoreTargetRequested:INativeTargetRequested,INativeAgile
{
    private readonly CoreDragReceiver receiver;private readonly CoreDropTarget target;
    internal CoreTargetRequested(CoreDragReceiver receiver,CoreDropTarget target){this.receiver=receiver;this.target=target;}
    public int Invoke(nint sender,nint args)
    {
        try
        {
            receiver.Requests++;
            nint value=Marshal.GetComInterfaceForObject(target,typeof(INativeCoreDropTarget));
            try {return WinrtDrag.Method<WinrtDrag.SetTarget>(args,6)(args,value);}
            finally {Marshal.Release(value);}
        }
        catch(Exception error){return error.HResult;}
    }
}

[ComVisible(true),Guid("D9126196-4C5B-417D-BB37-76381DEF8DB4"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeCoreDropTarget
{
    [PreserveSig]int GetIids(out uint count,out nint values);
    [PreserveSig]int GetRuntimeClassName(out nint name);
    [PreserveSig]int GetTrustLevel(out int level);
    [PreserveSig]int EnterAsync(nint info,nint ui,out nint operation);
    [PreserveSig]int OverAsync(nint info,nint ui,out nint operation);
    [PreserveSig]int LeaveAsync(nint info,out nint action);
    [PreserveSig]int DropAsync(nint info,out nint operation);
}

[ComVisible(true)]
public abstract class NativeInspectable:INativeInspectable,INativeAgile
{
    public int GetIids(out uint count,out nint values){count=0;values=0;return 0;}
    public int GetRuntimeClassName(out nint name){name=0;return 0;}
    public int GetTrustLevel(out int level){level=0;return 0;}
}

[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class CoreDropTarget:NativeInspectable,INativeCoreDropTarget
{
    private readonly CoreDragReceiver receiver;private readonly Control owner;private readonly Func<bool> ready;private readonly Action<Point> drop;
    internal CoreDropTarget(CoreDragReceiver receiver,Control owner,Func<bool> ready,Action<Point> drop){this.receiver=receiver;this.owner=owner;this.ready=ready;this.drop=drop;}
    private uint Effect(nint info)
    {
        if(owner.IsDisposed)return 0;
        bool accepted=owner.InvokeRequired?(bool)owner.Invoke(ready):ready();
        uint allowed=receiver.LastAllowed=WinrtDrag.Allowed(info);return receiver.LastEffect=!accepted?0u:(allowed&1)!=0?1u:(allowed&2)!=0?2u:0u;
    }
    private int Over(nint info,nint ui,out nint operation)
    {
        operation=0;
        try {uint effect=Effect(info);WinrtDrag.Caption(ui,effect!=0);operation=NativeCompletedOperation.Create(effect);return 0;}
        catch(Exception error){return error.HResult;}
    }
    public int EnterAsync(nint info,nint ui,out nint operation){receiver.Enters++;return Over(info,ui,out operation);}
    public int OverAsync(nint info,nint ui,out nint operation){receiver.Overs++;return Over(info,ui,out operation);}
    public int LeaveAsync(nint info,out nint action){action=NativeCompletedOperation.CreateAction();return 0;}
    public int DropAsync(nint info,out nint operation)
    {
        operation=0;
        try
        {
            uint effect=Effect(info);
            if(effect!=0){receiver.Drops++;if(owner.InvokeRequired)owner.Invoke((Action)(()=>drop(Cursor.Position)));else drop(Cursor.Position);}
            operation=NativeCompletedOperation.Create(effect);return 0;
        }
        catch(Exception error){return error.HResult;}
    }
}

[ComVisible(true),Guid("8B98AEA9-64F0-5672-B30E-DFD9C2E4F6FE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeAsyncOperation
{
    [PreserveSig]int GetIids(out uint count,out nint values);
    [PreserveSig]int GetRuntimeClassName(out nint name);
    [PreserveSig]int GetTrustLevel(out int level);
    [PreserveSig]int PutCompleted(nint handler);
    [PreserveSig]int GetCompleted(out nint handler);
    [PreserveSig]int GetResults(out uint value);
}
[ComVisible(true),Guid("5A648006-843A-4DA9-865B-9D26E5DFAD7B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeAsyncAction
{
    [PreserveSig]int GetIids(out uint count,out nint values);
    [PreserveSig]int GetRuntimeClassName(out nint name);
    [PreserveSig]int GetTrustLevel(out int level);
    [PreserveSig]int PutActionCompleted(nint handler);
    [PreserveSig]int GetActionCompleted(out nint handler);
    [PreserveSig]int GetActionResults();
}
[ComVisible(true),Guid("00000036-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeAsyncInfo
{
    [PreserveSig]int GetIids(out uint count,out nint values);
    [PreserveSig]int GetRuntimeClassName(out nint name);
    [PreserveSig]int GetTrustLevel(out int level);
    [PreserveSig]int GetId(out uint value);
    [PreserveSig]int GetStatus(out uint value);
    [PreserveSig]int GetErrorCode(out int value);
    [PreserveSig]int Cancel();
    [PreserveSig]int Close();
}

[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class NativeCompletedOperation:NativeInspectable,INativeAsyncOperation,INativeAsyncAction,INativeAsyncInfo
{
    private readonly uint value;private nint completed;private static int nextId;private readonly uint id=(uint)Interlocked.Increment(ref nextId);
    private NativeCompletedOperation(uint value){this.value=value;}
    internal static nint Create(uint value)=>Marshal.GetComInterfaceForObject(new NativeCompletedOperation(value),typeof(INativeAsyncOperation));
    internal static nint CreateAction()=>Marshal.GetComInterfaceForObject(new NativeCompletedOperation(0),typeof(INativeAsyncAction));
    public int PutCompleted(nint handler)=>SetCompleted(handler,typeof(INativeAsyncOperation));
    public int PutActionCompleted(nint handler)=>SetCompleted(handler,typeof(INativeAsyncAction));
    private int SetCompleted(nint handler,Type type)
    {
        if(completed!=0)Marshal.Release(completed);completed=handler;if(handler==0)return 0;Marshal.AddRef(handler);
        nint self=Marshal.GetComInterfaceForObject(this,type);
        try {return WinrtDrag.Method<WinrtDrag.Complete>(handler,3)(handler,self,1);}
        finally {Marshal.Release(self);}
    }
    public int GetCompleted(out nint handler){handler=completed;if(handler!=0)Marshal.AddRef(handler);return 0;}
    public int GetActionCompleted(out nint handler)=>GetCompleted(out handler);
    public int GetResults(out uint result){result=value;return 0;}
    public int GetActionResults()=>0;
    public int GetId(out uint result){result=id;return 0;}
    public int GetStatus(out uint result){result=1;return 0;}
    public int GetErrorCode(out int result){result=0;return 0;}
    public int Cancel()=>0;
    public int Close(){nint previous=Interlocked.Exchange(ref completed,0);if(previous!=0)Marshal.Release(previous);return 0;}
    ~NativeCompletedOperation(){Close();}
}
