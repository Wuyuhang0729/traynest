using System.Runtime.InteropServices;
using System.Text;

namespace NestedTray;
internal static class Native
{
    internal delegate nint Procedure(nint hwnd,uint message,nuint wp,nint lp);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct WindowClass
    {
        internal uint Style;internal Procedure Callback;internal int ClassExtra,WindowExtra;
        internal nint Instance,Icon,Cursor,Background;internal string? Menu;internal string ClassName;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct CopyData {internal nuint Tag;internal uint Size;internal nint Bytes;}
    [StructLayout(LayoutKind.Sequential)] internal struct Point {internal int X,Y;}
    [StructLayout(LayoutKind.Sequential)] internal struct MouseData {internal Point Point;internal uint Mouse,Flags,Time;internal nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] internal struct CursorInfo {internal uint Size,Flags;internal nint Cursor;internal Point Position;}
    internal delegate nint MouseProcedure(int code,nuint wp,nint lp);
    internal delegate bool WindowVisitor(nint window,nint parameter);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(WindowVisitor visitor,nint parameter);
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput {internal int X,Y;internal uint Data,Flags,Time;internal nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] internal struct KeyInput {internal ushort Key,Scan;internal uint Flags,Time;internal nuint Extra;}
    [StructLayout(LayoutKind.Explicit,Size=40)] internal struct Input {[FieldOffset(0)]internal uint Type;[FieldOffset(8)]internal MouseInput Mouse;[FieldOffset(8)]internal KeyInput Keyboard;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint FindWindowW(string name,string? title);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetWindowLongPtrW(nint hwnd,int index);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool GetWindowRect(nint hwnd,out NativeTray.Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern ushort RegisterClassW(ref WindowClass cls);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern nint CreateWindowExW(uint extended,string cls,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint DefWindowProcW(nint hwnd,uint message,nuint wp,nint lp);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern nint SendMessageTimeoutW(nint hwnd,uint message,nuint wp,nint lp,uint flags,uint timeout,out nuint result);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool UnregisterClassW(string cls,nint module);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessageW(string name);
    [DllImport("user32.dll")] internal static extern nint CopyIcon(nint icon);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint process);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] internal static extern int GetSystemMetricsForDpi(int index,uint dpi);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassNameW(nint window,StringBuilder text,int count);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll",SetLastError=true)] internal static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll",SetLastError=true)] internal static extern nint SetWindowsHookExW(int kind,MouseProcedure callback,nint module,uint thread);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook,int code,nuint wp,nint lp);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] internal static extern bool PostThreadMessageW(uint thread,uint message,nuint wp,nint lp);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window,uint attribute,ref int value,int size);
    internal static Rectangle Rect(NativeTray.Rect value)=>Rectangle.FromLTRB(value.Left,value.Top,value.Right,value.Bottom);
    internal static Rectangle GetIconRect(nint hwnd,uint id,Guid guid)
    {
        var identity=new NativeTray.Identity {Size=(uint)Marshal.SizeOf<NativeTray.Identity>(),Window=hwnd,Id=id,Guid=guid};
        return NativeTray.GetRect(ref identity,out var rect)==0 ? Rect(rect) : Rectangle.Empty;
    }
}
