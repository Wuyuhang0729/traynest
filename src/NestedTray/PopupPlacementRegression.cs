using System.Text.Json;
namespace NestedTray;
internal static class PopupPlacementRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(Path.GetFullPath(directory),"profile"));
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try
        {
            var work=new Rectangle(0,0,1920,1040);var anchor=new Rectangle(1640,800,240,200);var size=new Size(208,88);
            var left=PopupPosition.Calculate(anchor,size,work,PopupPlacement.Left);var above=PopupPosition.Calculate(anchor,size,work,PopupPlacement.Above);
            checks["LeftAlignsBesideAnchor"]=left==new Point(1424,912);
            checks["AboveAlignsOverAnchor"]=above==new Point(1640,704);
            checks["LeftBottomMatchesVisibleSurface"]=left.Y+size.Height==anchor.Bottom;
            checks["AboveLeftMatchesVisibleSurface"]=above.X==anchor.Left;
            var surface=NativeOverflow.SurfaceFromCells(new[]{new Rectangle(1644,804,40,40),new Rectangle(1804,804,40,40),new Rectangle(1644,884,40,40)},4);
            checks["XamlHostShadowExcludedFromAnchor"]=surface==new Rectangle(1640,800,208,128);
            var surfaceLeft=PopupPosition.Calculate(surface,size,work,PopupPlacement.Left);var surfaceAbove=PopupPosition.Calculate(surface,size,work,PopupPlacement.Above);
            checks["LeftUsesVisibleBottomInsteadOfHostBottom"]=surfaceLeft.Y+size.Height==928;
            checks["AboveUsesVisibleLeftInsteadOfHostLeft"]=surfaceAbove.X==1640;
            checks["NoCellsUsesFallbackBounds"]=NativeOverflow.SurfaceFromCells([],4).IsEmpty;
            foreach(var bounds in new[]{work,new Rectangle(-1920,-200,1920,1080),new Rectangle(0,0,640,480)})
                foreach(var edge in new[]{new Rectangle(bounds.Left,bounds.Top,20,20),new Rectangle(bounds.Right-20,bounds.Bottom-20,20,20)})
                    foreach(var side in Enum.GetValues<PopupPlacement>())
                        checks[$"Fits{bounds.Location}{edge.Location}{side}"]=bounds.Contains(new Rectangle(PopupPosition.Calculate(edge,size,bounds,side),size));
            checks["LeftFallsBackAboveAtEdge"]=PopupPosition.Calculate(new Rectangle(10,500,20,20),size,work,PopupPlacement.Left).Y==404;
            var settings=new Settings();settings.Save();string initial=File.ReadAllText(Settings.FilePath);
            settings.ChangePlacement(PopupPlacement.Above);checks["AbovePersists"]=Settings.Load().Placement==PopupPlacement.Above;
            settings.BeforeSave=()=>throw new IOException("Own injected placement failure");bool failed=false;
            try{settings.ChangePlacement(PopupPlacement.Left);}catch(TrayNestException){failed=true;}
            checks["SaveFailureKeepsMemoryAndDisk"]=failed&&settings.Placement==PopupPlacement.Above&&Settings.Load().Placement==PopupPlacement.Above;
            settings.BeforeSave=null;settings.ChangePlacement(PopupPlacement.Left);checks["LeftPersists"]=Settings.Load().Placement==PopupPlacement.Left;
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Placement\":99}");checks["InvalidPreferenceFallsBack"]=Settings.Load().Placement==PopupPlacement.Left;
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[]}");checks["OldConfigurationLoads"]=Settings.Load().Placement==PopupPlacement.Left;
            using var panel=new TrayPanel();panel.Location=new Point(-20000,-20000);panel.Show();nint foreground=Native.GetForegroundWindow();panel.SetPlacement(PopupPlacement.Above);checks["VisiblePanelUpdatesWithoutActivation"]=panel.Placement==PopupPlacement.Above&&!panel.IsOpening&&!panel.IsDismissing&&Native.GetForegroundWindow()==foreground;panel.Hide();panel.SetPlacement(PopupPlacement.Left);checks["HiddenPanelKeepsSelection"]=panel.Placement==PopupPlacement.Left&&!panel.Visible;
            bool menuFailure=false;
            using var menu=TrayApplication.CreatePlacementMenu(settings,panel.SetPlacement,action=>{try{action();}catch(TrayNestException){menuFailure=true;}});
            var leftItem=(ToolStripMenuItem)menu.DropDownItems[0];var aboveItem=(ToolStripMenuItem)menu.DropDownItems[1];
            checks["ProductionMenuHasBothChoices"]=menu.Text=="弹出位置"&&leftItem.Text=="左侧"&&aboveItem.Text=="上方";
            aboveItem.PerformClick();checks["MenuAboveClickPersistsAndApplies"]=aboveItem.Checked&&!leftItem.Checked&&panel.Placement==PopupPlacement.Above&&Settings.Load().Placement==PopupPlacement.Above;
            settings.BeforeSave=()=>throw new IOException("Own menu write failure");leftItem.PerformClick();
            checks["FailedMenuClickKeepsCheckAndPanel"]=menuFailure&&aboveItem.Checked&&!leftItem.Checked&&panel.Placement==PopupPlacement.Above&&Settings.Load().Placement==PopupPlacement.Above;
            // ToolStrip suppresses a rapid second click on the same item as a
            // double-click. A retry after a save failure is a separate click.
            settings.BeforeSave=null;Thread.Sleep(SystemInformation.DoubleClickTime+50);leftItem.PerformClick();checks["MenuLeftClickPersistsAndApplies"]=leftItem.Checked&&!aboveItem.Checked&&panel.Placement==PopupPlacement.Left&&Settings.Load().Placement==PopupPlacement.Left;
            CheckLayer(checks);
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count==0&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(directory,"placement-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Isolated geometry, settings save/rollback and production panel; no user config or input"},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private sealed class InactiveWindow:Form
    {
        protected override bool ShowWithoutActivation=>true;
    }
    private static void CheckLayer(Dictionary<string,bool> checks)
    {
        using var application=new InactiveWindow{ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,Location=new Point(-20000,-20000),StartPosition=FormStartPosition.Manual};
        using var taskbar=new InactiveWindow{ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,Location=new Point(-20000,-20000),StartPosition=FormStartPosition.Manual,TopMost=true};
        using var panel=new TrayPanel{TaskbarWindow=taskbar.Handle,Location=new Point(-20000,-20000)};
        nint foreground=Native.GetForegroundWindow();application.Show();taskbar.Show();panel.Show();panel.EnsureAboveApplications();
        bool Above(nint first,nint second)
        {
            for(nint window=Native.GetWindow(second,3);window!=0;window=Native.GetWindow(window,3))if(window==first)return true;
            return false;
        }
        checks["PanelBelowTaskbarWhileVisible"]=Above(taskbar.Handle,panel.Handle);
        checks["PanelStillAboveNormalApplications"]=Above(panel.Handle,application.Handle);
        panel.DismissSettings=new DismissOptions{DurationMs=180,DistanceDip=36,Fade=false};panel.Dismiss();
        var clock=System.Diagnostics.Stopwatch.StartNew();bool stayedBelow=true;
        while(panel.Visible&&clock.ElapsedMilliseconds<600){Application.DoEvents();if(panel.Visible)stayedBelow&=Above(taskbar.Handle,panel.Handle);Thread.Sleep(5);}
        checks["PanelStaysBelowTaskbarThroughoutDismiss"]=stayedBelow&&!panel.Visible;
        checks["LayeringDoesNotStealForeground"]=Native.GetForegroundWindow()==foreground;
    }
}
