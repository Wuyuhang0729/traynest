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
            checks["AboveAlignsOverAnchor"]=above==new Point(1672,704);
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
            settings.BeforeSave=null;leftItem.PerformClick();checks["MenuLeftClickPersistsAndApplies"]=leftItem.Checked&&!aboveItem.Checked&&panel.Placement==PopupPlacement.Left&&Settings.Load().Placement==PopupPlacement.Left;
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count==0&&checks.Values.All(value=>value);
        File.WriteAllText(Path.Combine(directory,"placement-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Isolated geometry, settings save/rollback and production panel; no user config or input"},DiagnosticStore.JsonOptions));return passed?0:1;
    }
}
