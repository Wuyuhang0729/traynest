using System.Runtime.InteropServices;
using System.Text;

namespace NestedTray;
internal static class ProcessExecutable
{
    [DllImport("kernel32.dll",SetLastError=true)]private static extern nint OpenProcess(uint access,bool inherit,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool QueryFullProcessImageNameW(nint process,uint flags,StringBuilder path,ref uint size);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(nint handle);
    // Reading an executable's path needs limited query rights, not VM_READ.
    // MainModule requires broader access and fails for elevated/UIAccess apps.
    internal static string Read(int process)
    {
        if(process<=0)return "";
        nint handle=OpenProcess(0x1000,false,(uint)process);if(handle==0)return "";
        try{var path=new StringBuilder(32768);uint size=(uint)path.Capacity;return QueryFullProcessImageNameW(handle,0,path,ref size)?path.ToString():"";}
        finally{CloseHandle(handle);}
    }
    internal static int Probe(int process,string output)
    {string path=Read(process);File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{Process=process,Resolved=!string.IsNullOrWhiteSpace(path),Executable=path}));return string.IsNullOrWhiteSpace(path)?1:0;}
}
