using System.Reflection;
using System.Text.Json;

namespace NestedTray;
internal static class FixedToolsRegression
{
    internal static int Run(string directory,string launchFixture="")
    {
        Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(directory,"profile"));
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var records=Enumerable.Range(0,26).Select(i=>new IconRecord{Key="live:"+i,Rule="fixture|"+i,Tooltip="Tool "+i,Executable=Path.Combine(directory,$"tool{i}.exe"),Selected=true,Image=SystemIcons.Information.ToBitmap()}).ToArray();
        try
        {
            File.WriteAllText(Settings.FilePath,"");
        }
        catch(DirectoryNotFoundException){Directory.CreateDirectory(Settings.DirectoryPath);}
        try
        {
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[\"fixture|0\",\"fixture|1\",\"fixture|2\"]}");
            var settings=Settings.Load();using var library=new ToolLibrary(settings);
            checks["LegacyRulesMigrateWithoutLoss"]=library.EnsureEntries(records.Take(3))&&settings.Tools.Count==3;
            settings.Save();checks["NamesAndImagesPersist"]=Settings.Load().Tools.All(tool=>tool.IconPng.Length>0&&tool.Name.StartsWith("Tool "));
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5};
            void Render(IEnumerable<IconRecord> live){panel.Render(library.Snapshot(live));if(!panel.Visible){panel.Location=new Point(-20000,-20000);panel.Show();}Application.DoEvents();}
            panel.Reordered=(record,index)=>{library.Reorder(record.Rule,index);Render(records.Take(3));};Render(records.Take(3));
            var grid=panel.Controls.OfType<FlowLayoutPanel>().Single();
            Dictionary<string,Rectangle> Positions()=>grid.Controls.OfType<Button>().ToDictionary(button=>((IconRecord)button.Tag!).Rule,button=>button.Bounds);
            string[] Order()=>grid.Controls.OfType<Button>().Select(button=>((IconRecord)button.Tag!).Rule).ToArray();
            var target=grid.Controls.OfType<Button>().First();
            typeof(TrayPanel).GetMethod("Reorder",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(panel,[records[2].Rule,target.PointToScreen(new Point(20,20))]);
            var expected=new[]{records[2].Rule,records[0].Rule,records[1].Rule};checks["DragOrderSaved"]=Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(expected)&&Order().SequenceEqual(expected);
            var previewTile=grid.Controls.OfType<Button>().First();
            var previewField=previewTile.GetType().GetField("Reordering",BindingFlags.Instance|BindingFlags.NonPublic)!;
            previewField.SetValue(previewTile,true);
            using(var preview=new Bitmap(previewTile.Width,previewTile.Height))
            {
                previewTile.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));
                var highlight=panel.LightSurface?Color.FromArgb(209,230,249):Color.FromArgb(43,66,86);
                checks["ReorderTargetHighlightVisible"]=preview.GetPixel(8,8).ToArgb()==highlight.ToArgb();
                preview.Save(Path.Combine(directory,"reorder-target-tile.png"));
            }
            using(var preview=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(directory,"reorder-feedback-no-duplicate.png"));}
            checks["ReorderFeedbackHasNoFloatingDuplicate"]=panel.Controls.Count==3&&grid.Controls.Count==3;
            previewField.SetValue(previewTile,false);
            var positions=Positions();Render(new[]{records[1],records[2]});
            checks["MissingToolRetainsSlotAndNeighbors"]=Positions().All(pair=>pair.Value==positions[pair.Key])&&library.Snapshot(new[]{records[1],records[2]}).Single(record=>record.Rule==records[0].Rule).IsOffline;
            using(var bitmap=new Bitmap(panel.Width,panel.Height))
            {
                panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,"offline-panel.png"));
                var tile=grid.Controls.OfType<Button>().Single(button=>((IconRecord)button.Tag!).IsOffline);using var dim=new Bitmap(40,40);tile.DrawToBitmap(dim,new Rectangle(0,0,40,40));
                var brightTile=grid.Controls.OfType<Button>().Single(button=>((IconRecord)button.Tag!).Rule==records[1].Rule);using var bright=new Bitmap(40,40);brightTile.DrawToBitmap(bright,new Rectangle(0,0,40,40));
                var back=tile.BackColor;double Energy(Bitmap b){double sum=0;for(int y=0;y<40;y++)for(int x=0;x<40;x++){var c=b.GetPixel(x,y);sum+=Math.Abs(c.R-back.R)+Math.Abs(c.G-back.G)+Math.Abs(c.B-back.B);}return sum;}
                double ratio=Energy(dim)/Energy(bright);checks["OfflineOpacityApproximately40Percent"]=ratio>.35&&ratio<.45;
            }
            Render(new[]{records[2],records[1],records[0]});checks["ReappearingToolReturnsToOriginalSlot"]=Positions().All(pair=>pair.Value==positions[pair.Key])&&Order().SequenceEqual(expected);
            var replacement=new IconRecord{Key="restarted-native-id",Rule=records[0].Rule,Selected=true,Tooltip="Restarted",Image=SystemIcons.Warning.ToBitmap()};
            Render(new[]{records[1],replacement,records[2]});
            var refreshed=grid.Controls.OfType<Button>().Single(button=>((IconRecord)button.Tag!).Rule==replacement.Rule);checks["NativeIdentityReplacementKeepsTileAndCurrentRecord"]=ReferenceEquals(refreshed.Tag,replacement)&&Positions().All(pair=>pair.Value==positions[pair.Key]);
            IconRecord? clicked=null;panel.IconInput=(record,_,_)=>clicked=record;typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(refreshed,[EventArgs.Empty]);
            checks["ClickUsesReplacementOwnerNotStaleCapture"]=ReferenceEquals(clicked,replacement);replacement.Dispose();
            var loaded=Settings.Load();using(var reboot=new ToolLibrary(loaded)){
                var empty=reboot.Snapshot([]);checks["RestartRestoresAllOfflineSlots"]=empty.All(record=>record.IsOffline)&&empty.Select(record=>record.Rule).SequenceEqual(expected);
                var late=reboot.Snapshot(new[]{records[1],records[0],records[2]});checks["ArrivalOrderDoesNotChangeSavedOrder"]=late.Select(record=>record.Rule).SequenceEqual(expected);
            }
            var failedOrder=settings.Tools.Select(tool=>tool.Rule).ToArray();Directory.CreateDirectory(Settings.FilePath+".tmp");
            bool reorderFailed=Throws(()=>library.Reorder(records[0].Rule,0));
            bool removeFailed=Throws(()=>library.Forget(records[0].Rule));Directory.Delete(Settings.FilePath+".tmp");
            checks["FailedReorderRollsBackMemoryAndDisk"]=reorderFailed&&settings.Tools.Select(tool=>tool.Rule).SequenceEqual(failedOrder)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(failedOrder);
            checks["FailedRemoveRetainsSlot"]=removeFailed&&settings.Collected.Contains(records[0].Rule)&&settings.Tools.Any(tool=>tool.Rule==records[0].Rule);
            Render(new[]{records[1],records[2]});bool offlineClicked=false;clicked=null;panel.OfflineInput=(record,_)=>offlineClicked=record.IsOffline;
            var offlineTile=grid.Controls.OfType<Button>().Single(button=>((IconRecord)button.Tag!).IsOffline);typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(offlineTile,[EventArgs.Empty]);
            checks["OfflineClickDoesNotSendStaleNativeCallback"]=offlineClicked&&clicked==null;
            settings.Columns=6;settings.Save();checks["ColumnPreferencePersistsAcrossRestart"]=Settings.Load().Columns==6;settings.Columns=5;settings.Save();
            foreach(var record in records.Skip(3))settings.Collected.Add(record.Rule);library.EnsureEntries(records);settings.Save();Render(records.Take(25));var before=Positions();Render(records);
            checks["Adding26thToolDoesNotReflowColumns"]=before.All(pair=>Positions()[pair.Key]==pair.Value);
            string good=Path.Combine(directory,"launch.exe");File.WriteAllText(good,"fixture only; launcher injected");
            var saved=settings.Tools[0];saved.LaunchTarget=good;
            int starts=0;long now=0;var launcher=new ToolLauncher{Clock=()=>now,IsRunning=_=>false,Start=_=>starts++};
            checks["ClickOfflineToolStartsOnce"]=launcher.Launch(saved).Started&&starts==1;
            checks["RepeatedClicksDuringLaunchDoNotDuplicate"]=!launcher.Launch(saved).Started&&starts==1;
            launcher.Registered(saved.Rule);checks["RegistrationClearsPendingState"]=!launcher.IsPending(saved.Rule);
            launcher.IsRunning=_=>true;checks["RunningProcessWithoutIconNotRelaunched"]=!launcher.Launch(saved).Started&&starts==1;
            launcher.Registered(saved.Rule);saved.LaunchTarget=Path.Combine(directory,"missing.exe");checks["MissingLaunchTargetShowsActionableHint"]=launcher.Launch(saved).Message.Contains("右键");
            saved.LaunchTarget=good;launcher.IsRunning=_=>false;launcher.Start=_=>throw new System.ComponentModel.Win32Exception(2);
            checks["LaunchFailureIsReportedAndRetryable"]=!launcher.Launch(saved).Started&&!launcher.IsPending(saved.Rule);
            launcher.Start=_=>starts++;launcher.Launch(saved);now=13000;checks["PendingStateExpiresForRetry"]=!launcher.IsPending(saved.Rule);
            string persisted=File.ReadAllText(Settings.FilePath);File.WriteAllText(Settings.FilePath,"not json");checks["InvalidConfigurationIsNotSilentlyDiscarded"]=Throws(()=>Settings.Load());File.WriteAllText(Settings.FilePath,persisted);
            library.Forget(records[0].Rule);checks["ExplicitRemoveReleasesSavedEntry"]=!Settings.Load().Collected.Contains(records[0].Rule)&&!Settings.Load().Tools.Any(tool=>tool.Rule==records[0].Rule);
            if(!string.IsNullOrEmpty(launchFixture)){
                string marker=Path.Combine(directory,"actual-launch.txt");Environment.SetEnvironmentVariable("TRAYNEST_LAUNCH_FIXTURE_MARKER",marker);
                var actual=new ToolLauncher();var actualTool=new SavedTool{Rule="actual-fixture",Executable=launchFixture,LaunchTarget=launchFixture};
                bool started=actual.Launch(actualTool).Started;var wait=System.Diagnostics.Stopwatch.StartNew();while(!File.Exists(marker)&&wait.ElapsedMilliseconds<8000){Application.DoEvents();Thread.Sleep(20);}
                checks["ActualExecutableLaunches"]=started&&File.Exists(marker);
                actual.Launch(actualTool);Thread.Sleep(250);checks["ActualDoubleLaunchSuppressed"]=File.Exists(marker)&&File.ReadAllLines(marker).Length==1;
                Environment.SetEnvironmentVariable("TRAYNEST_LAUNCH_FIXTURE_MARKER",null);
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        var report=new{Passed=checks.Values.All(value=>value)&&errors.Count==0,Checks=checks,Errors=errors,Scope="Isolated production settings/library/panel plus own launch helper. No live tray takeover, startup writes or Windows reboot."};
        File.WriteAllText(Path.Combine(directory,"fixed-tools-test.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));return report.Passed?0:1;
    }
    private static bool Throws(Action action){try{action();return false;}catch(TrayNestException){return true;}}
}
