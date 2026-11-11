using System.Runtime.InteropServices;
namespace NestedTray;
// Geometry-only taskbar facade for clients which discover our notification receiver.
// Never transfer Explorer's real child windows or replace their message procedures.
internal sealed class TaskbarGeometryFacade:IDisposable
{
 private readonly nint receiver,explorer;
 private readonly nint module=Native.GetModuleHandleW(null);
 private readonly Native.Procedure procedure=Native.DefWindowProcW;
 private readonly List<(string Name,nint Window,nint Source)> mirrors=new();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindowExW(nint parent,nint after,string name,string? title);
 [DllImport("user32.dll",SetLastError=true)]private static extern nint SetParent(nint window,nint parent);
 internal TaskbarGeometryFacade(nint receiver,nint explorer)
 {
  this.receiver=receiver;this.explorer=explorer;
  foreach(string name in new[]{"Windows.UI.Composition.DesktopWindowContentBridge","TrayNotifyWnd","Start"})
  {
   nint source=FindWindowExW(explorer,0,name,null);if(source==0)continue;
   var cls=new Native.WindowClass{Instance=module,ClassName=name,Callback=procedure};
   if(Native.RegisterClassW(ref cls)==0)continue;
   nint child=Native.CreateWindowExW(0,name,"",0x40000000,0,0,1,1,receiver,0,module,0);
   if(child==0){Native.UnregisterClassW(name,module);continue;}
   mirrors.Add((name,child,source));
  }
  Refresh();
 }
 internal void Refresh()
 {
  if(!Native.IsWindow(explorer)||!Native.GetWindowRect(explorer,out var taskbar))return;
  Native.SetWindowPos(receiver,0,taskbar.Left,taskbar.Top,taskbar.Right-taskbar.Left,taskbar.Bottom-taskbar.Top,0x214);
  foreach(var item in mirrors)if(Native.GetWindowRect(item.Source,out var rect))
   Native.SetWindowPos(item.Window,0,rect.Left-taskbar.Left,rect.Top-taskbar.Top,rect.Right-rect.Left,rect.Bottom-rect.Top,0x214);
  RestoreAttachments();
 }
 private void RestoreAttachments()
 {
  // A third-party taskbar widget may have attached to the receiver. Move it to
  // Explorer before receiver destruction can destroy a foreign child HWND.
  for(nint child=Native.GetWindow(receiver,5);child!=0;)
  {
   nint next=Native.GetWindow(child,2);Native.GetWindowThreadProcessId(child,out uint owner);
   if(owner!=(uint)Environment.ProcessId&&Native.IsWindow(explorer))SetParent(child,explorer);
   child=next;
  }
 }
 public void Dispose()
 {
  RestoreAttachments();foreach(var item in mirrors){Native.DestroyWindow(item.Window);Native.UnregisterClassW(item.Name,module);}mirrors.Clear();GC.KeepAlive(procedure);
 }
}
