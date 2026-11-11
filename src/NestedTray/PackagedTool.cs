using System.Runtime.InteropServices;
namespace NestedTray;
internal static class PackagedTool
{
 [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 private interface IActivationManager
 {
  [PreserveSig]int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string app,[MarshalAs(UnmanagedType.LPWStr)]string args,uint options,out uint process);
  [PreserveSig]int ActivateForFile(nint app,nint items,nint verb,out uint process);
  [PreserveSig]int ActivateForProtocol(nint app,nint items,out uint process);
 }
 internal static void Start(string id)
 {
  if(string.IsNullOrWhiteSpace(id)||!id.Contains('!'))throw new InvalidOperationException("应用启动标识无效。");
  var manager=(IActivationManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"),true)!)!;
  try{Marshal.ThrowExceptionForHR(manager.ActivateApplication(id,"",2,out _));}finally{Marshal.FinalReleaseComObject(manager);}
 }
}
