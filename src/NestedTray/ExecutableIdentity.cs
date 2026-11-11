using System.Runtime.InteropServices;
namespace NestedTray;
internal static class ExecutableIdentity
{
 [DllImport("shell32.dll")]private static extern int SHGetKnownFolderPath(ref Guid id,uint flags,nint token,out nint path);
 internal static string Normalize(string path)
 {
  if(path.Length<40||path[0]!='{'||path[38]!='\\'||!Guid.TryParse(path[..38],out var id))return path;
  if(SHGetKnownFolderPath(ref id,0,0,out nint pointer)<0)return path;
  try
  {
   string root=Marshal.PtrToStringUni(pointer)??"";if(root.Length==0)return path;
   string tail=path[39..];if(Path.IsPathRooted(tail)||tail.Split('\\','/').Contains(".."))return path;
   return Path.GetFullPath(Path.Combine(root,tail));
  }
  catch(Exception error)when(error is ArgumentException or NotSupportedException or PathTooLongException){return path;}
  finally{Marshal.FreeCoTaskMem(pointer);}
 }
 internal static string Rule(string rule)
 {
  int split=rule.LastIndexOf('|');return split<0?rule:Normalize(rule[..split]).ToLowerInvariant()+rule[split..];
 }
 internal static void NormalizeSettings(Settings settings)
 {
  settings.Collected=settings.Collected.Select(Rule).Distinct(StringComparer.Ordinal).ToList();
  foreach(var tool in settings.Tools)
  {
   string previous=tool.Executable;tool.Executable=Normalize(previous);tool.Rule=Rule(tool.Rule);
   if(string.Equals(tool.LaunchTarget,previous,StringComparison.OrdinalIgnoreCase))tool.LaunchTarget=tool.Executable;
  }
 }
}
