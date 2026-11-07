using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class AutoExpandRegression
{
    internal static int Run(string root)
    {
        Directory.CreateDirectory(root);var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var settings=new Settings{FollowNativeOverflow=true};settings.Save();
        using var app=new TrayApplication(true);Native.GetCursorPos(out var previous);
        using var timeout=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,30000,Timeout.Infinite);
        try
        {
            CloseNative();app.Panel.Hide();Pump(250);
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(1000);
            checks["NativeOpenAutomaticallyOpensPanel"]=app.Panel.Visible;
            var surface=NativeOverflow.SurfaceBounds(NativeOverflow.Window,(int)Math.Round(4*app.Panel.DeviceDpi/96.0));
            checks["FollowOpenSettlesAtNativeBottom"]=app.Panel.Bottom==surface.Bottom;
            app.Entry.Open?.Invoke();Pump(900);
            checks["ManualCloseStaysClosed"]=!app.Panel.Visible;
            CloseNative();Pump(400);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(800);
            checks["ReopeningNativeOpensPanelAgain"]=app.Panel.Visible;
            CloseNative();Pump(650);checks["ClosingNativeClosesPanel"]=!app.Panel.Visible;
            var settingsItem=app.OwnMenu.Items.OfType<ToolStripMenuItem>().Single(item=>item.Text=="设置");
            checks["MenuContainsOnlySettings"]=app.OwnMenu.Items.Count==1&&app.OwnMenu.Items[0].Text=="设置";
            settingsItem.PerformClick();Pump(250);settingsItem.PerformClick();Pump(100);
            var windows=Application.OpenForms.OfType<SettingsWindow>().ToArray();
            checks["SettingsUsesSingleIndependentWindow"]=windows.Length==1&&windows[0].ShowInTaskbar&&!windows[0].TopMost;
            var window=windows.Single();
            window.Controls.Find("ExpansionMode",true).OfType<ComboBox>().Single().SelectedIndex=2;
            using(var bitmap=new Bitmap(window.Width,window.Height)){using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(window.Location,Point.Empty,window.Size);bitmap.Save(Path.Combine(root,"settings-live-general.png"));}
            window.SelectPage(1);Pump(120);
            using(var bitmap=new Bitmap(window.Width,window.Height)){using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(window.Location,Point.Empty,window.Size);bitmap.Save(Path.Combine(root,"settings-live-panel.png"));}
            window.Close();Native.SetCursorPos(40,40);Pump(250);
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(350);
            checks["FollowDisabledDoesNotOpen"]=!app.Panel.Visible;
            var entry=app.Entry.Bounds;Native.SetCursorPos(entry.Left+entry.Width/2,entry.Top+entry.Height/2);Pump(200);
            checks["ShortHoverDoesNotOpen"]=!app.Panel.Visible;
            Pump(650);checks["SustainedHoverOpensPanel"]=app.Panel.Visible;
            app.Entry.Open?.Invoke();Pump(900);checks["ManualCloseSuppressesContinuedHover"]=!app.Panel.Visible;
            Native.SetCursorPos(40,40);Pump(200);entry=app.Entry.Bounds;Native.SetCursorPos(entry.Left+entry.Width/2,entry.Top+entry.Height/2);Pump(750);
            checks["LeavingAndReturningCanOpenAgain"]=app.Panel.Visible;
            CloseNative();Pump(500);
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{Native.SetCursorPos(previous.X,previous.Y);}
        bool passed=errors.Count==0&&checks.Count==12&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(root,"auto-expand-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;
        void CloseNative()
        {
            if(!NativeOverflow.Visible)return;
            var nodes=AutomationElement.FromHandle(app.Bridge.ExplorerWindow).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
            foreach(AutomationElement node in nodes)if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons")((InvokePattern)node.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
    }
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
}
