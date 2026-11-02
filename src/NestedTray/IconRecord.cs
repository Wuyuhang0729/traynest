using System.Diagnostics;

namespace NestedTray;
internal sealed class IconRecord:IDisposable
{
    internal nint Window;
    internal uint Id,Callback,Version,OriginalState;
    internal Guid Guid;
    internal string Key="",Rule="",Tooltip="",Executable="";
    internal int ProcessId;
    internal bool Selected;
    internal bool IsOffline;
    internal bool RegistrationKnown;
    internal bool CanCollect=>Callback!=0||RegistrationKnown;
    internal Bitmap? Image;
    internal Rectangle NativeBounds;
    internal long RehideAfter;
    internal static string Identity(nint hwnd,uint id,Guid guid)=>guid!=Guid.Empty?guid.ToString("D"):hwnd.ToInt64()+":"+id;
    internal void UpdateImage(nint handle)
    {
        if(handle==0)return;
        nint copy=Native.CopyIcon(handle);if(copy==0)return;
        try {using var icon=Icon.FromHandle(copy);var old=Image;Image=icon.ToBitmap();old?.Dispose();}
        finally {Native.DestroyIcon(copy);}
    }
    internal RecoveryItem Recovery()=>new(Window.ToInt64(),Id,Guid,ProcessId,OriginalState);
    public void Dispose()=>Image?.Dispose();
}
