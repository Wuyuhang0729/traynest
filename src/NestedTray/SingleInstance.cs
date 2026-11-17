using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NestedTray;

internal static class SingleInstance
{
    internal static readonly uint OpenMessage=Native.RegisterWindowMessageW("TrayNest.Open.v1");
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint window,int command);
    internal static bool Activate()
    {
        bool found=false;
        Native.EnumWindows((window,_)=>
        {
            Native.GetWindowThreadProcessId(window,out uint owner);
            if(owner==Environment.ProcessId)return true;
            try
            {
                using var process=Process.GetProcessById((int)owner);
                if(!string.Equals(process.MainModule?.FileName,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))return true;
                var title=new System.Text.StringBuilder(256);GetWindowTextW(window,title,title.Capacity);
                if(title.ToString()=="NestedTray entry")
                {Native.AllowSetForegroundWindow(owner);found=Native.SendMessageTimeoutW(window,OpenMessage,0,0,2,1500,out var result)!=0&&result==1;return !found;}
                if(title.ToString().StartsWith("TrayNest · TN",StringComparison.Ordinal))
                {ShowWindowAsync(window,9);Native.SetForegroundWindow(window);found=true;return false;}
            }
            catch(Exception error)when(error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException){}
            return true;
        },0);
        return found;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowTextW(nint window,System.Text.StringBuilder text,int length);
}
