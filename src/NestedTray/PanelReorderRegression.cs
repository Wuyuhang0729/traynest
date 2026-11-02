using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class PanelReorderRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(directory,"profile"));
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var records=Enumerable.Range(0,10).Select(i=>new IconRecord{Key="reorder:"+i,Rule="reorder|"+i,Selected=true,IsOffline=i==8,Tooltip="Reorder fixture "+i,Image=(i%2==0?SystemIcons.Information:SystemIcons.Warning).ToBitmap()}).ToArray();
        Native.GetCursorPos(out var previous);nint foreground=Native.GetForegroundWindow();
        try
        {
            var settings=new Settings{Collected=records.Select(record=>record.Rule).ToList(),Tools=records.Select(SavedTool.FromRecord).ToList()};settings.Save();
            using var library=new ToolLibrary(settings);
            using var focus=new Form{Text="TrayNest reorder test input",ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(30,30,300,90)};focus.Show();Native.SetForegroundWindow(focus.Handle);
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5};
            int clicks=0,rightClicks=0,removals=0;bool saveFailed=false;
            void RenderSaved()=>panel.Render(library.Snapshot(records));
            panel.Reordered=(record,index)=>{try{library.Reorder(record.Rule,index);}catch(TrayNestException){saveFailed=true;}RenderSaved();};
            panel.RemoveIcon=record=>{removals++;library.Forget(record.Rule);record.Selected=false;RenderSaved();};
            panel.IconInput=(_,code,_)=>{if(code==0x7B)rightClicks++;else clicks++;};panel.OfflineInput=(_,code)=>{if(code==0x7B)rightClicks++;else clicks++;};
            RenderSaved();panel.Location=new Point(Screen.PrimaryScreen!.WorkingArea.Left+80,Screen.PrimaryScreen.WorkingArea.Top+200);panel.Show();Pump(100);
            var grid=panel.Controls.OfType<FlowLayoutPanel>().Single();
            string[] Order()=>grid.Controls.Cast<Control>().Select(tile=>((IconRecord)tile.Tag!).Rule).ToArray();
            Button Tile(string rule)=>grid.Controls.OfType<Button>().Single(tile=>((IconRecord)tile.Tag!).Rule==rule);
            Point Center(Control tile)=>tile.PointToScreen(new Point(tile.Width/2,tile.Height/2));
            void Capture(string name){using var bitmap=new Bitmap(panel.Width,panel.Height);using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(panel.Location,Point.Empty,panel.Size);bitmap.Save(Path.Combine(directory,name+".png"));}
            void Drive(Action action)
            {
                Exception? failure=null;var driver=new Thread(()=>{try{action();}catch(Exception error){failure=error;}finally{Mouse(4);}}){IsBackground=true};driver.Start();
                var clock=Stopwatch.StartNew();while(driver.IsAlive){if(clock.ElapsedMilliseconds>12000)throw new TimeoutException("Own reorder gesture timed out");Pump(10);}if(failure!=null)throw failure;Pump(80);
            }
            void Drag(string rule,Point target,Action preview,bool escape=false,bool loseCapture=false)
            {
                var source=Tile(rule);var from=Center(source);
                Drive(()=>
                {
                    Native.SetCursorPos(from.X,from.Y);Thread.Sleep(60);Mouse(2);Thread.Sleep(60);
                    for(int step=1;step<=10;step++){Native.SetCursorPos(from.X+(target.X-from.X)*step/10,from.Y+(target.Y-from.Y)*step/10);Thread.Sleep(30);}
                    Thread.Sleep(100);panel.Invoke(preview);
                    if(escape){Key(0x1B,false);Thread.Sleep(120);Key(0x1B,true);}
                    if(loseCapture)panel.Invoke((Action)(()=>source.Capture=false));
                    Mouse(4);Thread.Sleep(100);
                });
            }
            var initial=Order();var expected=initial.ToList();expected.Remove("reorder|9");expected.Insert(0,"reorder|9");
            Drag("reorder|9",Center(Tile(initial[0])),()=>
            {
                checks["DragShowsTargetFeedback"]=panel.HasReorderPreview;
                checks["NoFloatingDuplicateIcon"]=panel.Controls.Count==3;
                var cursor=new Native.CursorInfo{Size=(uint)Marshal.SizeOf<Native.CursorInfo>()};
                checks["DraggingKeepsNormalArrowCursor"]=Native.GetCursorInfo(ref cursor)&&cursor.Cursor==Cursors.Default.Handle;
                checks["LivePreviewMovesNeighboringIcons"]=Order().SequenceEqual(expected);
                checks["PreviewDoesNotWriteSettings"]=Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(initial);
                RenderSaved();checks["IncomingIconRefreshPreservesPreview"]=Order().SequenceEqual(expected);Capture("during-reorder");
            });
            checks["ReleaseCommitsOrder"]=Order().SequenceEqual(expected)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(expected);
            checks["SuccessfulDropShowsPlacementFeedback"]=(bool)Tile("reorder|9").GetType().GetField("Placed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(Tile("reorder|9"))!;
            checks["ReorderDoesNotLaunchTools"]=clicks==0&&rightClicks==0&&removals==0;
            Capture("after-reorder");
            var before=Order();string moving=before[0],targetRule=before[7];expected=before.ToList();expected.Remove(moving);expected.Insert(7,moving);
            Drag(moving,Center(Tile(targetRule)),()=>{});checks["CrossRowReorderPersists"]=Order().SequenceEqual(expected)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(expected);
            before=Order();expected=before.ToList();expected.Remove("reorder|8");expected.Insert(2,"reorder|8");
            Drag("reorder|8",Center(Tile(before[2])),()=>{});checks["OfflineToolCanReorder"]=Order().SequenceEqual(expected)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(expected);
            before=Order();Drag(before[^1],Center(Tile(before[0])),()=>{},escape:true);
            checks["EscapeRestoresOrderWithoutClosingPanel"]=panel.Visible&&!panel.IsReordering&&Order().SequenceEqual(before)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(before);
            Drag(before[^1],Center(Tile(before[0])),()=>{},loseCapture:true);checks["CaptureLossCancelsWithoutSavingOrRemoving"]=Order().SequenceEqual(before)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(before)&&removals==0;
            Directory.CreateDirectory(Settings.FilePath+".tmp");
            try{Drag(before[^1],Center(Tile(before[0])),()=>{});}finally{Directory.Delete(Settings.FilePath+".tmp");}
            checks["SaveFailureRestoresOrderAndDoesNotShowSuccess"]=saveFailed&&Order().SequenceEqual(before)&&Settings.Load().Tools.Select(tool=>tool.Rule).SequenceEqual(before);
            Drive(()=>{var point=Center(Tile(before[0]));Native.SetCursorPos(point.X,point.Y);Mouse(2);Thread.Sleep(40);Mouse(4);});checks["NormalLeftClickStillWorks"]=clicks==1;
            Drive(()=>{var point=Center(Tile(before[0]));Native.SetCursorPos(point.X,point.Y);Mouse(8);Thread.Sleep(40);Mouse(16);});checks["NormalRightClickStillWorks"]=rightClicks==1;
            before=Order();Drag(before[^1],new Point(panel.Right+90,panel.Top+20),()=>{});checks["DragOutsideStillRestoresTool"]=removals==1&&Order().Length==9&&!Settings.Load().Tools.Any(tool=>tool.Rule==before[^1]);
            var saved=Settings.Load();using var restartedLibrary=new ToolLibrary(saved);using var restartedPanel=new TrayPanel{UseSavedOrder=true,FixedColumns=5};restartedPanel.Render(restartedLibrary.Snapshot(records.Reverse()));
            checks["RestartKeepsEditedOrder"]=restartedPanel.Controls.OfType<FlowLayoutPanel>().Single().Controls.Cast<Control>().Select(tile=>((IconRecord)tile.Tag!).Rule).SequenceEqual(Order());
            Pump(500);checks["PlacementFeedbackClears"]=grid.Controls.OfType<Button>().All(tile=>!(bool)tile.GetType().GetField("Placed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(tile)!);
            checks["NoDuplicateTools"]=Order().Distinct().Count()==Order().Length&&saved.Tools.Select(tool=>tool.Rule).Distinct().Count()==saved.Tools.Count;
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{Mouse(4);Key(0x1B,true);Native.SetCursorPos(previous.X,previous.Y);Native.SetForegroundWindow(foreground);foreach(var record in records)record.Dispose();}
        bool passed=checks.Values.All(value=>value)&&errors.Count==0;
        File.WriteAllText(Path.Combine(directory,"panel-reorder-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Actual mouse gestures against an isolated production panel and saved library; own fixture icons only, no Explorer takeover or startup writes"},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Mouse(uint flags){var input=new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=flags}};if(Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>())!=1)throw new InvalidOperationException("Mouse input failed");}
    private static void Key(ushort key,bool up){var input=new Native.Input{Type=1,Keyboard=new Native.KeyInput{Key=key,Flags=up?2u:0u}};Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>());}
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
}
