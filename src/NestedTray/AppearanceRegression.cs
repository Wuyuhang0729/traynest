using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class AppearanceRegression
{
    private sealed class PreviewBoard:Form
    {
        protected override bool ShowWithoutActivation=>true;
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(238,242,247));
            using(var blue=new SolidBrush(Color.FromArgb(72,136,229)))e.Graphics.FillRectangle(blue,0,80,260,210);
            using(var orange=new SolidBrush(Color.FromArgb(235,170,93)))e.Graphics.FillRectangle(orange,260,80,260,210);
            TextRenderer.DrawText(e.Graphics,"TrayNest · 面板外观预览",new Font("Microsoft YaHei UI",16),new Point(24,25),Color.FromArgb(25,39,57));
            base.OnPaint(e);
        }
    }
    internal static int Run(string directory,bool capture)
    {
        Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(Path.GetFullPath(directory),"profile"));
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try
        {
            File.WriteAllText(Settings.FilePath.EnsureParent(),"{\"Collected\":[],\"Tools\":[]}");
            checks["OldSettingsDefaultToSystemTray"]=Settings.Load().Appearance==PanelAppearance.FollowSystem;
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Appearance\":99}");
            checks["InvalidAppearanceFallsBack"]=Settings.Load().Appearance==PanelAppearance.FollowSystem;
            var settings=new Settings{Collected=["fixture"]};settings.Save();
            foreach(var mode in Enum.GetValues<PanelAppearance>()){settings.ChangeAppearance(mode);checks["Persists"+mode]=Settings.Load().Appearance==mode&&Settings.Load().Collected.SequenceEqual(new[]{"fixture"});}
            settings.BeforeSave=()=>throw new IOException("Injected own-profile write failure");bool failed=false;
            try{settings.ChangeAppearance(PanelAppearance.Light);}catch(TrayNestException){failed=true;}
            checks["FailedSaveRollsBack"]=failed&&settings.Appearance==PanelAppearance.Dark&&Settings.Load().Appearance==PanelAppearance.Dark;
            settings.BeforeSave=null;
            using var panel=new TrayPanel{Location=new Point(-20000,-20000),FixedColumns=5,UseSavedOrder=true};
            using var record=new IconRecord{Key="fixture",Rule="fixture",Selected=true,Tooltip="Preview icon"};
            record.UpdateImage(SystemIcons.Information.Handle);panel.Render([record]);
            bool menuFailed=false;
            using var menu=TrayApplication.CreateAppearanceMenu(settings,panel.SetAppearance,action=>{try{action();}catch(TrayNestException){menuFailed=true;}});
            checks["FourAppearanceOptions"]=menu.DropDownItems.Count==4;
            foreach(ToolStripMenuItem item in menu.DropDownItems)
            {
                item.PerformClick();
                checks["Menu"+item.Text]=item.Checked&&menu.DropDownItems.Cast<ToolStripMenuItem>().Count(x=>x.Checked)==1&&Settings.Load().Appearance==panel.Appearance;
            }
            settings.BeforeSave=()=>throw new IOException("Injected menu failure");((ToolStripMenuItem)menu.DropDownItems[1]).PerformClick();
            checks["FailedMenuChoiceKeepsAppliedAppearance"]=menuFailed&&panel.Appearance==PanelAppearance.FollowSystem&&settings.Appearance==PanelAppearance.FollowSystem&&((ToolStripMenuItem)menu.DropDownItems[3]).Checked;
            settings.BeforeSave=null;
            nint foreground=Native.GetForegroundWindow();
            using var board=new PreviewBoard{FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Size=new Size(520,360),TopMost=true};
            if(capture){var work=Screen.PrimaryScreen!.WorkingArea;board.Location=new Point(work.Left+80,work.Top+80);board.Show();Pump(100);}
            foreach(var mode in Enum.GetValues<PanelAppearance>())
            {
                panel.SetAppearance(mode);panel.Location=capture?new Point(board.Left+150,board.Top+130):new Point(-20000,-20000);panel.Show();panel.EnsureAboveApplications();Pump(350);
                var grid=panel.Controls.OfType<IconGrid>().Single();var tile=grid.Controls.OfType<Button>().Single();
                checks["PaletteAppliedToChildren"+mode]=grid.BackColor==panel.BackColor&&tile.BackColor==panel.BackColor&&tile.ForeColor==panel.ForeColor&&tile.Image!=null;
                checks["OpaqueIconsAndUnchangedBounds"+mode]=panel.Opacity==1&&panel.Width==208&&tile.Width==40&&panel.CoreDrop.Registered;
                if(mode==PanelAppearance.Light)checks["LightIsLight"]=panel.BackColor.R>240&&!panel.AcrylicActive&&!panel.CustomAcrylicTint;
                if(mode==PanelAppearance.Dark)checks["DarkIsDark"]=panel.BackColor.R<50&&!panel.AcrylicActive&&!panel.CustomAcrylicTint;
                if(mode==PanelAppearance.FollowSystem)checks["FollowsWindowsTrayColorMode"]=panel.LightSurface==PanelTheme.SystemLight&&!panel.AcrylicActive;
                if(capture){using var shot=new Bitmap(board.Width,board.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(board.Location,Point.Empty,shot.Size);shot.Save(Path.Combine(directory,mode+".png"));}
                if(mode==PanelAppearance.Acrylic)File.WriteAllText(Path.Combine(directory,"acrylic-state.json"),JsonSerializer.Serialize(new{panel.AcrylicActive,panel.BackdropResult,panel.CustomAcrylicTint,SystemLight=PanelTheme.SystemLight,Transparency=PanelTheme.TransparencyEnabled,Build=Environment.OSVersion.Version.Build}));
                panel.Hide();
            }
            checks["AppearanceSwitchDoesNotActivate"]=foreground==Native.GetForegroundWindow();
            panel.SetAppearance(PanelAppearance.Light);panel.SetNativeDragFeedback(true);checks["LightDragFeedbackKeepsLightPalette"]=panel.BackColor.R>200;panel.SetNativeDragFeedback(false);
            checks["DragFeedbackRestoresLightSurface"]=panel.BackColor.R>240;
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count==0&&checks.Values.All(x=>x);
        File.WriteAllText(Path.Combine(directory,"appearance-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
    private static string EnsureParent(this string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);return path;}
}
