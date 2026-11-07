using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class MaterialSettingsRegression
{
    internal static int Run(string root)
    {
        Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(root));
        new Settings{Appearance=PanelAppearance.Acrylic}.Save();var checks=new Dictionary<string,bool>();var errors=new List<string>();
        using var app=new TrayApplication(true);string phase="settings";
        using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,30000,Timeout.Infinite);
        try
        {
            app.OwnMenu.Items.OfType<ToolStripMenuItem>().Single(item=>item.Text=="设置").PerformClick();Pump(200);
            var window=Application.OpenForms.OfType<SettingsWindow>().Single();window.SelectPage(1);
            var mirror=window.Controls.Find("MatchPanelMaterials",true).OfType<CheckBox>().Single();mirror.Checked=true;
            Wait(()=>mirror.Enabled,10000);
            checks["SyncTogglePersistsAfterNativeAcknowledgement"]=mirror.Checked&&Settings.Load().MatchPanelMaterials;
            checks["CollectionUsesAcrylicWhenSynchronized"]=app.Panel.Appearance==PanelAppearance.Acrylic&&(!PanelTheme.TransparencyEnabled||SystemInformation.HighContrast||app.Panel.AcrylicActive);
            var appearance=window.Controls.Find("Appearance",true).OfType<ComboBox>().Single();
            appearance.SelectedIndex=3;Pump(900);checks["DarkSelectionUpdatesCollection"]=!app.Panel.LightSurface&&Settings.Load().Appearance==PanelAppearance.Dark;
            appearance.SelectedIndex=2;Pump(900);checks["LightSelectionUpdatesCollection"]=app.Panel.LightSurface&&Settings.Load().Appearance==PanelAppearance.Light;
            appearance.SelectedIndex=0;Pump(900);checks["FollowSystemUsesSharedMaterial"]=app.Panel.Appearance==PanelAppearance.Acrylic&&Settings.Load().Appearance==PanelAppearance.FollowSystem;
            appearance.SelectedIndex=1;Pump(900);
            phase="hide-settings";window.Hide();Pump(600);
            phase="open-native";NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Wait(()=>NativeOverflow.Visible,1500);
            Native.SetForegroundWindow(NativeOverflow.Window);Pump(450);phase="open-collection";app.Panel.Open();
            Wait(()=>app.Panel.Visible&&!app.Panel.IsAnimating,1800);app.Panel.RequestRealignment();Pump(300);app.Panel.FollowNativeBounds();Pump(220);
            checks["BothPanelsVisibleForMaterialPreview"]=NativeOverflow.Visible&&app.Panel.Visible;
            Native.GetWindowRect(NativeOverflow.Window,out var nativeRect);var bounds=Rectangle.Union(Native.Rect(nativeRect),app.Panel.Bounds);bounds.Inflate(12,12);
            var surface=NativeOverflow.SurfaceBounds(NativeOverflow.Window,(int)Math.Round(4*app.Panel.DeviceDpi/96.0));
            checks["SynchronizedPanelsBottomAlign"]=app.Panel.Bottom==surface.Bottom;
            File.WriteAllText(Path.Combine(root,"paired-geometry.json"),JsonSerializer.Serialize(new{Native=Native.Rect(nativeRect),Surface=surface,Panel=app.Panel.Bounds,Entry=app.Panel.EntryRectangle,app.Panel.IsOpening,app.Panel.DragPreview},DiagnosticStore.JsonOptions));
            using(var bitmap=new Bitmap(bounds.Width,bounds.Height)){using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);bitmap.Save(Path.Combine(root,"synchronized-panels.png"));}
            window.Show();window.Activate();mirror.Checked=false;Wait(()=>mirror.Enabled,5000);
            checks["DisableRestoresAndPersists"]=!mirror.Checked&&!Settings.Load().MatchPanelMaterials;
            window.Close();
        }
        catch(Exception error){errors.Add(phase+": "+error);File.WriteAllText(Path.Combine(root,"failure-state.json"),JsonSerializer.Serialize(new{Phase=phase,NativeVisible=NativeOverflow.Visible,PanelVisible=app.Panel.Visible,app.Panel.IsAnimating,app.Panel.IsPreparing,app.Panel.IsDismissing,Geometry=app.Panel.PresentationGeometry},DiagnosticStore.JsonOptions));}
        bool passed=errors.Count==0&&checks.Count==8&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(root,"material-settings-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Wait(Func<bool> ready,int timeout){var clock=Stopwatch.StartNew();while(!ready()&&clock.ElapsedMilliseconds<timeout)Pump(30);if(!ready())throw new TimeoutException("Material settings transaction did not finish");}
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
}
