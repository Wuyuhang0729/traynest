using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class NativeMaterialRegression
{
    internal static int Run(string root)
    {
        Directory.CreateDirectory(root);var checks=new Dictionary<string,bool>();var errors=new List<string>();
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(Path.GetFullPath(root),"profile"));
        using var controller=new NativeMaterialController();
        using var dispatcher=new Form{ShowInTaskbar=false,Opacity=0,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),Size=new Size(1,1)};
        using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,35000,Timeout.Infinite);
        bool wasVisible=NativeOverflow.Visible;int result=1;nint explorer=0;
        dispatcher.Shown+=async(_,_)=>
        {
            try
            {
                if((Native.GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Mouse is in use; material test was not started");
                Native.EnumWindows((window,_)=>{var cls=new System.Text.StringBuilder(80);Native.GetClassNameW(window,cls,80);if(cls.ToString()=="Shell_TrayWnd"){Native.GetWindowThreadProcessId(window,out uint pid);if(Path.GetFileName(ProcessExecutable.Read((int)pid)).Equals("explorer.exe",StringComparison.OrdinalIgnoreCase))explorer=window;}return true;},0);
                if(explorer==0||!NativeOverflow.TryOpen(explorer))throw new InvalidOperationException("Native overflow unavailable");
                await Task.Delay(350);nint target=NativeOverflow.Window;Native.GetWindowThreadProcessId(target,out uint owner);
                double before=Capture("before");
                await controller.ConnectAsync(target,(int)owner);checks["NativeBackgroundTargetConnected"]=controller.Connected;
                var medians=new Dictionary<PanelAppearance,double>();
                foreach(var mode in new[]{PanelAppearance.Light,PanelAppearance.Dark,PanelAppearance.Acrylic})
                {
                    if((Native.GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Mouse interaction started during material test");
                    int sequence=controller.Apply(mode,true,mode!=PanelAppearance.Dark,true,false);await controller.WaitForAppliedAsync(sequence);await Task.Delay(300);
                    medians[mode]=Capture(mode.ToString());checks[mode+"Acknowledged"]=controller.LastError==0;
                }
                int restore=controller.Apply(PanelAppearance.FollowSystem,false,true,false,false);await controller.WaitForAppliedAsync(restore);await Task.Delay(300);
                double after=Capture("restored");checks["LightAndDarkVisiblyDiffer"]=medians[PanelAppearance.Light]-medians[PanelAppearance.Dark]>70;
                checks["OriginalBackgroundRestored"]=Math.Abs(before-after)<12;
                File.WriteAllText(Path.Combine(root,"background-luminance.json"),JsonSerializer.Serialize(new{Before=before,After=after,Modes=medians,Backend=controller.BackendState},DiagnosticStore.JsonOptions));
                double Capture(string name)
                {
                    if(!Native.GetWindowRect(target,out var rect))throw new InvalidOperationException("Native overflow moved or closed");
                    var bounds=Native.Rect(rect);using var bitmap=new Bitmap(bounds.Width,bounds.Height);using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);
                    bitmap.Save(Path.Combine(root,"native-"+name+".png"));var samples=new List<double>();
                    for(int y=22;y<bitmap.Height-22;y+=4)for(int x=22;x<bitmap.Width-22;x+=4){var color=bitmap.GetPixel(x,y);samples.Add(.2126*color.R+.7152*color.G+.0722*color.B);}
                    samples.Sort();return samples.Count==0?0:samples[samples.Count/2];
                }
            }
            catch(Exception error){errors.Add(error.ToString());}
            finally
            {
                controller.Restore();await Task.Delay(200);
                if(!wasVisible&&NativeOverflow.Visible&&explorer!=0)try
                {
                    var nodes=AutomationElement.FromHandle(explorer).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
                    foreach(AutomationElement node in nodes)if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons")((InvokePattern)node.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                }catch(Exception error){errors.Add(error.ToString());}
                bool passed=errors.Count==0&&checks.Count==6&&checks.Values.All(value=>value);result=passed?0:1;
                File.WriteAllText(Path.Combine(root,"native-material-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));dispatcher.Close();
            }
        };
        Application.Run(dispatcher);return result;
    }
}
