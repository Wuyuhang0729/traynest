using System.Text.Json;
using System.Reflection;

namespace NestedTray;
internal static class ExperienceRegression
{
    internal static int Run(string directory)
    {
        directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try
        {
            foreach(string scenario in new[]{"cancel","success","mixed","first","middle","final-save","journal","journal-after-native","siblings","siblings-failure"})
            {
                Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(directory,scenario));Directory.CreateDirectory(Settings.DirectoryPath);
                var settings=new Settings();var journal=new RecoveryJournal();
                using var bridge=new TrayBridge(settings,journal);
                var native=new Dictionary<string,uint>();
                int count=scenario.StartsWith("siblings")?4:3;
                for(int i=0;i<count;i++)
                {
                    string rule=scenario.StartsWith("siblings")&&i==3?"own|0":"own|"+i;
                    var icon=new IconRecord{Key="own:"+i,Rule=rule,Id=(uint)i,Selected=true};bridge.Icons.Add(icon.Key,icon);native[icon.Key]=1;
                    if(!settings.Collected.Contains(rule)){settings.Collected.Add(rule);settings.Tools.Add(new SavedTool{Rule=rule});}
                }
                if(scenario is "mixed" or "final-save" or "journal"){settings.Collected.Add("offline");settings.Tools.Add(new SavedTool{Rule="offline"});}
                settings.Save();journal.Write(bridge.Icons.Values.Select(value=>value.Recovery()));
                string before=File.ReadAllText(Settings.FilePath),recoveryBefore=File.ReadAllText(journal.PathName);int saves=0,writes=0;
                settings.BeforeSave=()=>{saves++;if(scenario=="final-save"&&saves==4)throw new IOException("Own injected save failure");};
                journal.BeforeWrite=()=>{writes++;if(scenario=="journal"||scenario=="journal-after-native"&&writes==2)throw new IOException("Own injected journal failure");};
                bridge.StateOverride=(icon,state)=>{if(state==0&&(scenario=="first"&&icon.Key=="own:0"||scenario=="middle"&&icon.Key=="own:1"||scenario=="siblings-failure"&&icon.Key=="own:3"))throw new InvalidOperationException("Own injected native failure");native[icon.Key]=state;};
                var result=bridge.RestoreAll(scenario!="cancel");
                if(scenario=="cancel")checks["CancelChangesNeitherMemoryNorDisk"]=result==new BulkRestoreResult(0,0,0,false)&&before==File.ReadAllText(Settings.FilePath)&&recoveryBefore==File.ReadAllText(journal.PathName)&&saves==0&&writes==0;
                else
                {
                    int expected=scenario is "first" or "middle" or "siblings-failure"?2:scenario is "journal" or "journal-after-native"?0:count;
                    checks[scenario+"Counts"]=result.Restored==expected&&result.Failed==(scenario=="journal"?4:scenario is "first" or "middle" or "final-save"?1:scenario=="journal-after-native"?3:scenario=="siblings-failure"?2:0)&&result.ClearedOffline==(scenario=="mixed"?1:0);
                    var loaded=Settings.Load();
                    checks[scenario+"Persistence"]=loaded.Collected.SequenceEqual(settings.Collected)&&loaded.Tools.Select(tool=>tool.Rule).SequenceEqual(settings.Tools.Select(tool=>tool.Rule));
                    checks[scenario+"SelectedRetainsRules"]=bridge.Icons.Values.Where(icon=>icon.Selected).All(icon=>settings.Collected.Contains(icon.Rule));
                    var items=JsonSerializer.Deserialize<RecoveryItem[]>(File.ReadAllText(journal.PathName))!;
                    checks[scenario+"JournalCoversHidden"]=native.Where(pair=>pair.Value==1).All(pair=>items.Any(item=>item.Id==bridge.Icons[pair.Key].Id))&&items.Length==bridge.Icons.Values.Count(icon=>icon.Selected);
                    if(scenario is "first" or "middle" or "siblings-failure"){bridge.StateOverride=(icon,state)=>native[icon.Key]=state;checks[scenario+"Retry"]=bridge.RestoreAll(true).Failed==0&&Settings.Load().Collected.Count==0;}
                }
                settings.BeforeSave=null;journal.BeforeWrite=null;bridge.StateOverride=(icon,state)=>native[icon.Key]=state;
            }
            using var dialog=BulkRestoreDialog.Create();checks["ConfirmationDefaultsToCancel"]=dialog.AcceptButton==dialog.CancelButton&&dialog.ActiveControl==dialog.CancelButton;
            VerifyPanelAndLaunch(directory,checks);
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=checks.Values.All(value=>value)&&errors.Count==0;
        File.WriteAllText(Path.Combine(directory,"experience-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Isolated native-state adapter and real settings/recovery files; no Explorer takeover, input or user profile access."},DiagnosticStore.JsonOptions));
        return passed?0:1;
    }
    private static void VerifyPanelAndLaunch(string directory,Dictionary<string,bool> checks)
    {
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(directory,"panel-profile"));
        string target=Path.Combine(directory,"own-launch.exe");File.WriteAllText(target,"Not executed; injected launcher");
        var tool=new SavedTool{Rule="own-launch",LaunchTarget=target,Executable=target};
        long now=0;int starts=0,probes=0;
        var launcher=new ToolLauncher{Clock=()=>now,Start=_=>starts++,IsRunning=_=>{probes++;return false;}};
        using var offline=new IconRecord{Key="offline",Rule=tool.Rule,Selected=true,IsOffline=true,Tooltip="Own fixture",Image=SystemIcons.Information.ToBitmap()};
        using var live=new IconRecord{Key="live",Rule="live",Selected=true,Callback=1,Tooltip="Own live fixture",Image=SystemIcons.Warning.ToBitmap()};
        using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5,ToolStatus=launcher.Status};
        panel.Location=new Point(-20000,-20000);panel.Render([offline,live]);panel.Show();
        var grid=panel.Controls.OfType<FlowLayoutPanel>().Single();
        Button tile=grid.Controls.OfType<Button>().First();
        checks["LaunchEntersWaiting"]=launcher.Launch(tool).Started&&launcher.Status(tool.Rule).State==ToolLaunchState.Waiting;
        panel.RefreshToolStates();
        checks["WaitingIsAccessibleAndHasNonColorMark"]=tile.AccessibleDescription!.Contains("等待")&&(string)tile.GetType().GetField("StatusMark",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(tile)! == "…";
        for(int i=0;i<5;i++){launcher.Launch(tool);panel.RefreshToolStates();}
        checks["RepeatDoesNotStartOrProbeProcesses"]=starts==1&&probes==1;
        panel.Hide();panel.Show();panel.RefreshToolStates();checks["ReopenRetainsWaiting"]=tile.AccessibleDescription!.Contains("等待");
        now=11999;checks["SuppressionLastsTwelveSeconds"]=launcher.IsPending(tool.Rule);
        now=12000;panel.RefreshToolStates();checks["TimeoutKeepsSlotAndDoesNotRetry"]=launcher.Status(tool.Rule).State==ToolLaunchState.TimedOut&&grid.Controls.Count==2&&starts==1&&tile.AccessibleDescription!.Contains("仍未发现");
        using(var bitmap=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(bitmap,panel.ClientRectangle);bitmap.Save(Path.Combine(directory,"launch-timeout.png"));}
        panel.Hide();panel.Show();panel.RefreshToolStates();checks["ReopenRetainsTimeout"]=tile.AccessibleDescription!.Contains("仍未发现");
        launcher.Registered(tool.Rule);checks["RegistrationClearsLaunchState"]=launcher.Status(tool.Rule).State==ToolLaunchState.Idle;
        launcher.IsRunning=_=>true;launcher.Launch(tool);launcher.Launch(tool);checks["RunningWithoutIconIsDistinctAndSuppressed"]=launcher.Status(tool.Rule).State==ToolLaunchState.RunningWithoutIcon&&starts==1;
        launcher.Registered(tool.Rule);tool.LaunchTarget=Path.Combine(directory,"missing.exe");launcher.Launch(tool);checks["InvalidTargetDistinct"]=launcher.Status(tool.Rule).State==ToolLaunchState.InvalidTarget;
        tool.LaunchTarget=target;launcher.IsRunning=_=>false;launcher.Start=_=>throw new IOException("Own failure");launcher.Launch(tool);checks["LaunchFailureDistinct"]=launcher.Status(tool.Rule).State==ToolLaunchState.Failed;
        launcher.Start=_=>starts++;string shortcut=Path.Combine(directory,"own.lnk");File.WriteAllText(shortcut,"Injected shortcut");tool.LaunchTarget=shortcut;checks["ShortcutRemainsAccepted"]=launcher.Launch(tool).Started;
        int removals=0,reorders=0,clicks=0;panel.RemoveIcon=_=>removals++;panel.Reordered=(_,_)=>reorders++;panel.IconInput=(_,_,_)=>clicks++;
        void Invoke(string method,params object[] args)=>typeof(TrayPanel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(panel,args);
        Point center=tile.PointToScreen(new Point(20,20)),outside=new(panel.Right+50,panel.Top+20);
        Invoke("BeginTilePress",tile,center);checks["PressAloneHasNoExitHint"]=!panel.HasOutsideReorderHint;
        Invoke("MoveTilePress",tile,outside);checks["InternalDragOutsideShowsHint"]=panel.HasOutsideReorderHint;
        Invoke("MoveTilePress",tile,center);checks["ReturnInsideClearsHint"]=!panel.HasOutsideReorderHint;
        Invoke("MoveTilePress",tile,outside);panel.HandleReorderEscape(true);panel.HandleReorderEscape(false);
        checks["OutsideEscapeDoesNotRemoveOrSave"]=!panel.IsReordering&&!panel.HasOutsideReorderHint&&removals==0&&reorders==0;
        Invoke("BeginTilePress",tile,center);Invoke("MoveTilePress",tile,outside);tile.Capture=false;
        checks["OutsideCaptureLossCancels"]=!panel.IsReordering&&!panel.HasOutsideReorderHint&&removals==0&&reorders==0;
        Invoke("BeginTilePress",tile,center);Invoke("MoveTilePress",tile,outside);Invoke("FinishTilePress",tile,outside,false);
        checks["OutsideReleaseRemovesOnceWithoutForwarding"]=removals==1&&clicks==0&&!panel.HasOutsideReorderHint;
        panel.Render([]);
        var empty=panel.Controls.OfType<Label>().Single();
        foreach(int dpi in new[]{96,144,192})
        {
            panel.CellPixels=40*dpi/96;panel.Render([]);using var font=new Font(empty.Font.FontFamily,9*dpi/96f,GraphicsUnit.Point);empty.Font=font;
            var measured=TextRenderer.MeasureText(empty.Text,empty.Font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding);
            checks["EmptyTextFitsSimulatedDpi"+dpi]=measured.Width<=empty.Width&&measured.Height<=empty.Height;
            using var bitmap=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(bitmap,panel.ClientRectangle);bitmap.Save(Path.Combine(directory,$"empty-layout-{dpi}.png"));
        }
        using var invalid=new IconRecord{Key="invalid",Image=SystemIcons.Information.ToBitmap()};panel.SetIncomingIcon(invalid);checks["UnrecognizedSourceHasNoReceiveFeedback"]=!panel.NativeDragFeedback;
        using var incoming=new IconRecord{Key="incoming",Callback=1,Image=SystemIcons.Information.ToBitmap()};panel.SetIncomingIcon(incoming);checks["RecognizedSourceHasReceiveFeedback"]=panel.NativeDragFeedback;
        panel.SetIncomingIcon(null);
    }
}
