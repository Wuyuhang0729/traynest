using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class TaskbarLayerRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        nint foreground=Native.GetForegroundWindow();
        try
        {
            using var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(80,80,160,40),Text="TrayNest own taskbar anchor"};
            using var panel=new TrayPanel{TaskbarWindow=anchor.Handle};
            panel.DismissSettings.Enabled=false;panel.SetAppearance(PanelAppearance.Dark);
            panel.Bounds=new Rectangle(300,200,208,168);
            anchor.Show();panel.Show();Pump(80);
            using var cover=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=panel.Bounds,BackColor=Color.Magenta,Text="TrayNest own covering application"};
            foreach(bool topmost in new[]{false,true,false})
            {
                anchor.TopMost=topmost;cover.Show();cover.Activate();Pump(80);
                var before=Native.GetForegroundWindow();panel.EnsureAboveApplications();Pump(80);
                string label=(checks.Count/3)+"-"+(topmost?"topmost":"ordinary");
                checks[label+"-PanelRetainsTopmost"]=(Native.GetWindowLongPtrW(panel.Handle,-20).ToInt64()&8)!=0;
                var point=new Native.Point{X=panel.Left+2,Y=panel.Top+2};
                checks[label+"-PanelIsHitAboveOrdinaryApplication"]=Native.GetAncestor(Native.WindowFromPoint(point),2)==panel.Handle;
                checks[label+"-ForegroundPreserved"]=Native.GetForegroundWindow()==before;
                using var image=new Bitmap(panel.Width,panel.Height);using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(panel.Location,Point.Empty,panel.Size);
                image.Save(Path.Combine(directory,label+".png"));cover.Hide();
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{if(foreground!=0)Native.SetForegroundWindow(foreground);}
        bool passed=checks.Count==9&&checks.Values.All(value=>value)&&errors.Count==0;
        File.WriteAllText(Path.Combine(directory,"taskbar-layer-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Production panel and own taskbar/cover windows, real HWND styles and screen hit testing. Does not establish any third-party tool as the cause."},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    internal static int RunTrayOwner(string directory,string expected)
    {
        Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(directory));
        string state="Native",error="";
        try{using var bridge=new TrayBridge(new Settings(),new RecoveryJournal());bridge.Start();}
        catch(TrayNestException exception){state=exception.Code;}
        catch(Exception exception){state="Unexpected";error=exception.ToString();}
        bool passed=state==expected;
        File.WriteAllText(Path.Combine(directory,"tray-owner-test.json"),JsonSerializer.Serialize(new{Passed=passed,Expected=expected,Actual=state,Error=error},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
}
