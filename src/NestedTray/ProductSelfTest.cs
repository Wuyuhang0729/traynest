using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class ProductSelfTest
{
    internal static int Run(string fixtureExe,bool oleSource=false,bool legacyIdentity=false)
    {
        string folder=Settings.DirectoryPath;Directory.CreateDirectory(folder);
        string reportPath=Path.Combine(folder,"product-self-test.json");
        var checks=new Dictionary<string,object?> { ["Scope"]="Production code; own fixture processes only. No discovery broadcast or startup writes.",["Errors"]=new List<string>() };
        var errors=(List<string>)checks["Errors"]!;
        void Save()=>File.WriteAllText(reportPath,JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        using var deadline=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,25000,Timeout.Infinite);
        TrayApplication? app=null;Process? first=null,second=null,neighbor=null;
        Guid guid=Guid.NewGuid();
        bool startupBefore=Settings.StartupEnabled;
        try
        {
            app=new TrayApplication(true);
            checks["CoreDragDropRegistration"]=app.Panel.CoreDrop.Status;
            string firstFolder=Path.Combine(folder,"first");
            first=Start(fixtureExe,firstFolder,guid,legacyIdentity);app.Bridge.AllowedOwners!.Add(first.Id);
            uint expectedVersion=legacyIdentity?0u:4u;
            Wait(()=>app.Bridge.Icons.Values.Any(item=>item.ProcessId==first.Id&&item.Callback!=0&&item.Version==expectedVersion));
            var record=app.Bridge.Icons.Values.Single(item=>item.ProcessId==first.Id);string rule=record.Rule;
            checks["CapturedOriginalInteractionMetadata"]=record.Callback!=0&&record.Version==expectedVersion;
            if(legacyIdentity)checks["UnusedNonzeroGuidIgnored"]=record.Guid==Guid.Empty&&record.Key==IconRecord.Identity(record.Window,record.Id,Guid.Empty);
            checks["LiveImageCaptured"]=record.Image!=null;
            Wait(()=>
            {
                OpenOverflow(app.Bridge.ExplorerWindow);
                return FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")!=null&&FindIcon(app.Bridge.ExplorerWindow,"嵌套面板")!=null;
            });
            var source=FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture");
            var entry=FindIcon(app.Bridge.ExplorerWindow,"嵌套面板");
            if(source==null||entry==null)throw new InvalidOperationException("Fixture or ordinary entry is not visible in native overflow.");
            // Regression: click to open normally, then return to the native overflow.
            // Preview mode would mask Deactivate dismissing the panel before a drag starts.
            ((InvokePattern)entry.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(200);
            checks["PanelOpenedNormallyBeforeDrag"]=app.Panel.Visible&&!app.Panel.DragPreview;
            Pump(350);
            if(!NativeOverflow.Visible)ClickOverflowChevron(app.Bridge.ExplorerWindow);
            Wait(()=>
            {
                return FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")!=null;
            });
            Pump(200);
            checks["PanelStaysOpenWhenReturningToSystemTray"]=app.Panel.Visible;
            Save();
            if(!app.Panel.Visible)throw new InvalidOperationException("Regression: opening native overflow dismissed the normally opened panel.");
            source=FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")??throw new InvalidOperationException("No source icon after returning to native tray.");
            var sourceBounds=source.Current.BoundingRectangle;var entryBounds=app.Entry.Bounds;
            var nativeImage=source.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Image));
            var nativeImageBounds=nativeImage?.Current.BoundingRectangle??System.Windows.Rect.Empty;
            checks["NativeImageBounds"]=nativeImageBounds.ToString();
            checks["PanelLayoutMatchesNativeIconAndCell"]=!nativeImageBounds.IsEmpty&&app.Panel.IconPixels==(int)Math.Round(nativeImageBounds.Width)&&app.Panel.CellPixels==(int)Math.Round(sourceBounds.Width);
            checks["PanelDoesNotCoverSourceIcon"]=!app.Panel.Bounds.IntersectsWith(new Rectangle((int)sourceBounds.X,(int)sourceBounds.Y,(int)sourceBounds.Width,(int)sourceBounds.Height));
            checks["SourceNativeBounds"]=Native.GetIconRect(record.Window,record.Id,record.Guid).ToString();
            checks["SourceAccessibleBounds"]=sourceBounds.ToString();
            checks["EntryAccessibleBounds"]=entryBounds.ToString();
            if((Native.GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Mouse button is in use; refusing to inject the controlled drag.");
            var from=new Point((int)(sourceBounds.X+sourceBounds.Width/2),(int)(sourceBounds.Y+sourceBounds.Height/2));
            var emptyTarget=app.Panel.Controls.OfType<Label>().Single(label=>label.Text.StartsWith("从系统托盘",StringComparison.Ordinal));
            var emptyScreen=AutomationElement.FromHandle(emptyTarget.Handle).Current.BoundingRectangle;
            var to=new Point((int)(emptyScreen.X+emptyScreen.Width/2),(int)(emptyScreen.Y+emptyScreen.Height/2));
            checks["PanelManagedBounds"]=app.Panel.Bounds.ToString();
            Native.GetWindowRect(app.Panel.Handle,out var nativePanelBounds);checks["PanelNativeBounds"]=Native.Rect(nativePanelBounds).ToString();
            checks["PanelAccessibleBounds"]=AutomationElement.FromHandle(app.Panel.Handle).Current.BoundingRectangle.ToString();
            checks["EmptyAccessibleBounds"]=emptyScreen.ToString();checks["NativeDropTargetPoint"]=to.ToString();
            if(oleSource)
            {
                uint origin=unchecked((uint)((((int)sourceBounds.Top&0xFFFF)<<16)|((int)sourceBounds.Left&0xFFFF)));
                NativeTray.PostMessage(record.Window,0x8000+903,origin,0);
                Wait(()=>File.Exists(Path.Combine(firstFolder,"external-ole-window.json")));
                var sourceWindow=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(firstFolder,"external-ole-window.json"))).GetProperty("Window").GetInt64();
                app.RegisterTestDragSource((nint)sourceWindow,record);
                Native.SetWindowPos((nint)sourceWindow,new nint(-1),0,0,0,0,0x13);
                Wait(()=>Native.GetAncestor(Native.WindowFromPoint(new Native.Point{X=from.X,Y=from.Y}),2)==(nint)sourceWindow);
                Native.GetWindowRect((nint)sourceWindow,out var sourceRect);checks["ExternalOleSourceBounds"]=Native.Rect(sourceRect).ToString();
            }
            Native.GetCursorPos(out var previousCursor);
            try
            {
                Native.SetCursorPos(from.X,from.Y);Pump(50);
                MouseButton(2);Pump(180);checks["DragCandidateFound"]=app.HasDragCandidate;
                checks["PanelStaysOpenOnNativeIconPress"]=app.Panel.Visible;
                for(int step=1;step<=8;step++){Native.SetCursorPos(from.X+(to.X-from.X)*step/8,from.Y+(to.Y-from.Y)*step/8);Pump(40);}
                Pump(200);
                checks["NativeOleDragEnterCount"]=app.Panel.ExternalDragEnters;
                checks["NativeCoreDragEnterCount"]=app.Panel.CoreDrop.Enters;
                checks["NativeCoreAcceptedEffect"]=app.Panel.CoreDrop.LastEffect;
                checks["NativeOleFormats"]=app.Panel.LastExternalFormats;
                var allowed=app.Panel.LastExternalAllowed;var effect=app.Panel.LastExternalEffect;
                checks["NativeOleAllowedEffects"]=allowed.ToString();
                checks["NativeOleAcceptedEffect"]=effect.ToString();
                nint hit=Native.WindowFromPoint(new Native.Point{X=to.X,Y=to.Y});nint hitRoot=Native.GetAncestor(hit,2);Native.GetWindowThreadProcessId(hitRoot,out uint hitOwner);
                var hitClass=new System.Text.StringBuilder(256);Native.GetClassNameW(hitRoot,hitClass,256);
                checks["HoverRootIsToolPanel"]=hitRoot==app.Panel.Handle;checks["HoverRootClass"]=hitClass.ToString();checks["HoverRootOwner"]=hitOwner;
                var cursorInfo=new Native.CursorInfo{Size=(uint)Marshal.SizeOf<Native.CursorInfo>()};Native.GetCursorInfo(ref cursorInfo);
                checks["HoverCursorIsNotForbidden"]=cursorInfo.Cursor!=Cursors.No.Handle;
                checks["EmptyPanelShowsAcceptFeedback"]=app.Panel.NativeDragFeedback;
                checks["IncomingIconPreviewVisible"]=app.Panel.HasIncomingIconPreview;
                checks["EmptyPanelAcceptsActualNativeDrag"]=app.Panel.ExternalDragEnters>0?app.Panel.LastExternalEffect!=DragDropEffects.None:app.HasDragCandidate&&app.Panel.NativeDragFeedback;
                using(var hover=new Bitmap(app.Panel.Width,app.Panel.Height))
                {
                    using(var graphics=Graphics.FromImage(hover))graphics.CopyFromScreen(app.Panel.Location,Point.Empty,hover.Size);
                    hover.Save(Path.Combine(folder,"native-drag-hover.png"));
                }
                Save();
                MouseButton(4);Pump(400);
            }
            finally {MouseButton(4);Native.SetCursorPos(previousCursor.X,previousCursor.Y);}
            checks["NativeMouseDragCollectsFixture"]=record.Selected;
            checks["NativeOverflowAvailableForNextDrag"]=NativeOverflow.Visible;
            checks["NativeDragTransport"]=app.Panel.ExternalDragEnters>0?"OLE":"Win11 native capture";
            if(app.Panel.ExternalDragEnters>0)checks["NativeOleDropDelivered"]=app.Panel.ExternalDrops>0;
            if(oleSource)
            {
                Wait(()=>File.Exists(Path.Combine(firstFolder,"external-ole-result.txt")));
                checks["ExternalOleSourceObservedAcceptance"]=File.ReadAllText(Path.Combine(firstFolder,"external-ole-result.txt")) is "Copy" or "Move";
                checks["ExternalOleProtocolActuallyExercised"]=app.Panel.ExternalDragEnters>0&&app.Panel.ExternalDrops>0;
            }
            Save();
            checks["DragSelectionHandlerCollectsFixture"]=record.Selected;
            checks["NativeOriginalIconHidden"]=FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")==null;
            checks["RuleSavedToDisk"]=Settings.Load().Collected.Contains(rule);
            app.Panel.Hide();
            Wait(()=>{OpenOverflow(app.Bridge.ExplorerWindow);entry=FindIcon(app.Bridge.ExplorerWindow,"嵌套面板");return entry!=null;});
            ((InvokePattern)entry.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(200);
            checks["OrdinaryNativeEntryOpensPanel"]=app.Panel.Visible&&!app.Panel.ShowInTaskbar;
            var rect=Native.GetIconRect(record.Window,record.Id,record.Guid);
            checks["GetRectMatchesRealPanelTile"]=rect==app.Panel.CellBounds(record);
            if(!NativeOverflow.Visible)ClickOverflowChevron(app.Bridge.ExplorerWindow);Pump(300);
            checks["BothPanelsVisibleBeforeManualClose"]=app.Panel.Visible&&NativeOverflow.Visible;
            ClickOverflowChevron(app.Bridge.ExplorerWindow,()=>checks["CollectedPanelClosesAtChevronPress"]=app.Panel.IsDismissing||!app.Panel.Visible);
            Wait(()=>!NativeOverflow.Visible&&!app.Panel.Visible);
            checks["ClosingNativeOverflowClosesCollectedPanel"]=true;
            OpenOverflow(app.Bridge.ExplorerWindow);
            entry=FindIcon(app.Bridge.ExplorerWindow,"嵌套面板")??throw new InvalidOperationException("No entry after closing paired panels.");
            ((InvokePattern)entry.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(200);
            string tip=record.Tooltip;Pump(600);checks["DynamicTooltipContinuesWhileCollected"]=record.Tooltip!=tip;
            if(app.Panel.Visible)
            {
                using var bitmap=new Bitmap(app.Panel.Width,app.Panel.Height);
                app.Panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"product-panel.png"));
            }
            var tile=app.Panel.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single();
            checks["RenderedImageMatchesNativeImageSize"]=tile.Image!=null&&tile.Image.Width==(int)Math.Round(nativeImageBounds.Width)&&tile.Image.Height==(int)Math.Round(nativeImageBounds.Height);
            checks["RenderedCellMatchesNativeCellSize"]=tile.Width==(int)Math.Round(sourceBounds.Width)&&tile.Height==(int)Math.Round(sourceBounds.Height);
            int beforeAction=File.Exists(Path.Combine(firstFolder,"events.jsonl"))?ReadEvents(firstFolder).Length:0;
            var actionCell=app.Panel.CellBounds(record);Native.GetCursorPos(out var beforeActionCursor);
            Native.SetCursorPos(actionCell.Left+actionCell.Width/2,actionCell.Top+actionCell.Height/2);MouseButton(2);Pump(40);MouseButton(4);Pump(150);Native.SetCursorPos(beforeActionCursor.X,beforeActionCursor.Y);
            checks["PanelDefaultActionReachesOriginalOwner"]=ReadEvents(firstFolder).Skip(beforeAction).Any(value=>value.GetProperty("Event").GetUInt32()==(legacyIdentity?0x202u:0x400u));
            if(legacyIdentity)checks["ChangingUnusedGuidDoesNotSplitRecords"]=app.Bridge.Icons.Values.Count(value=>value.ProcessId==first.Id)==1;
            var doubleRect=app.Panel.CellBounds(record);Native.GetCursorPos(out var doubleCursor);
            checks["PanelVisibleBeforePhysicalDoubleClick"]=app.Panel.Visible;
            var doubleInput=Task.Run(()=>
            {
                Native.SetCursorPos(doubleRect.Left+doubleRect.Width/2,doubleRect.Top+doubleRect.Height/2);
                Thread.Sleep(50);MouseButton(2);Thread.Sleep(30);MouseButton(4);Thread.Sleep(80);MouseButton(2);Thread.Sleep(30);MouseButton(4);
            });
            try {Wait(()=>doubleInput.IsCompleted);doubleInput.GetAwaiter().GetResult();Pump(150);}
            finally {MouseButton(4);Native.SetCursorPos(doubleCursor.X,doubleCursor.Y);}
            checks["NativeDoubleClickReachesOriginalOwner"]=ReadEvents(firstFolder).Any(value=>value.GetProperty("Event").GetUInt32()==0x203);
            string neighborFolder=Path.Combine(folder,"neighbor");
            neighbor=Start(fixtureExe,neighborFolder,Guid.NewGuid(),false);app.Bridge.AllowedOwners.Add(neighbor.Id);
            Wait(()=>app.Bridge.Icons.Values.Any(value=>value.ProcessId==neighbor.Id&&value.Callback!=0&&value.Version==4));
            var neighborRecord=app.Bridge.Icons.Values.Single(value=>value.ProcessId==neighbor.Id);app.Bridge.Collect(neighborRecord);Pump(80);
            var firstCell=app.Panel.CellBounds(record);Native.GetCursorPos(out var beforeMenuCursor);
            Native.SetCursorPos(firstCell.Left+firstCell.Width/2,firstCell.Top+firstCell.Height/2);MouseButton(8);Pump(40);MouseButton(16);Pump(200);
            checks["PanelRemainsOpenWhileOriginalMenuIsOpen"]=app.Panel.Visible;
            var menuData=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(firstFolder,"menu.json")));
            nint firstMenu=(nint)menuData.GetProperty("Handle").GetInt64();
            Native.GetWindowRect(firstMenu,out var firstMenuBounds);
            var neighborCell=app.Panel.CellBounds(neighborRecord);
            var choices=new[]{new Point(neighborCell.Left+3,neighborCell.Top+3),new Point(neighborCell.Right-3,neighborCell.Bottom-3),new Point(neighborCell.Left+3,neighborCell.Bottom-3)};
            var nextPoint=choices.First(point=>!Native.Rect(firstMenuBounds).Contains(point));
            Native.SetCursorPos(nextPoint.X,nextPoint.Y);MouseButton(8);Pump(40);MouseButton(16);Pump(250);
            var neighborMenuData=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(neighborFolder,"menu.json")));
            nint neighborMenu=(nint)neighborMenuData.GetProperty("Handle").GetInt64();
            checks["ClickingNextIconDismissesPreviousMenu"]=!Native.IsWindowVisible(firstMenu);
            checks["NextOriginalMenuPanelVisible"]=app.Panel.Visible;
            checks["NextOriginalMenuVisible"]=Native.IsWindowVisible(neighborMenu);
            checks["NextOriginalMenuOpensWithoutClosingPanel"]=app.Panel.Visible&&Native.IsWindowVisible(neighborMenu);
            // Reproduce delivery of the observer copy after the control's MouseUp
            // has already opened its menu; it must not close that new menu.
            app.OtherPointerDown(nextPoint,app.Panel.Handle);Pump(60);
            checks["DelayedObserverPreservesNewContextMenu"]=app.Panel.Visible&&Native.IsWindowVisible(neighborMenu);
            var nextMenu=AutomationElement.FromHandle(neighborMenu);
            var nextCommand=nextMenu.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"NestedTray fixture command"));
            ((InvokePattern)nextCommand.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(100);
            checks["NextOriginalMenuCommandWorks"]=File.Exists(Path.Combine(neighborFolder,"commands.log"));
            app.Bridge.Restore(neighborRecord);NativeTray.PostMessage(neighborRecord.Window,0x10,0,0);Wait(()=>neighbor.HasExited);Wait(()=>!app.Bridge.Icons.Values.Any(value=>value.ProcessId==neighbor.Id));
            firstCell=app.Panel.CellBounds(record);
            Native.SetCursorPos(firstCell.Left+firstCell.Width/2,firstCell.Top+firstCell.Height/2);MouseButton(8);Pump(40);MouseButton(16);Pump(180);
            menuData=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(firstFolder,"menu.json")));
            var menu=AutomationElement.FromHandle((nint)menuData.GetProperty("Handle").GetInt64());
            var command=menu.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"NestedTray fixture command"));
            ((InvokePattern)command.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(100);
            checks["OriginalMenuCommandWorksFromProductPanel"]=File.Exists(Path.Combine(firstFolder,"commands.log"));
            checks["PanelRemainsOpenAfterMenuCommand"]=app.Panel.Visible;
            Native.SetCursorPos(firstCell.Left+firstCell.Width/2,firstCell.Top+firstCell.Height/2);MouseButton(8);Pump(40);MouseButton(16);Pump(180);
            var blank=new Point(app.Panel.Right-12,app.Panel.Bottom-12);Native.SetCursorPos(blank.X,blank.Y);MouseButton(2);Pump(40);MouseButton(4);Pump(180);
            checks["ClickingPanelBlankDismissesMenuAndKeepsPanel"]=app.Panel.Visible&&!Native.IsWindowVisible(firstMenu);
            Native.SetCursorPos(beforeMenuCursor.X,beforeMenuCursor.Y);
            NativeTray.PostMessage(record.Window,0x8000+902,0,0);Pump(150);
            var outsideData=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(firstFolder,"outside.json")));
            var outsideButton=AutomationElement.FromHandle((nint)outsideData.GetProperty("Button").GetInt64());
            if(!app.Panel.Visible)
            {
                OpenOverflow(app.Bridge.ExplorerWindow);
                var entryBeforeOutside=FindIcon(app.Bridge.ExplorerWindow,"嵌套面板")??throw new InvalidOperationException("No entry before outside-click regression.");
                ((InvokePattern)entryBeforeOutside.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(200);
            }
            checks["PanelVisibleBeforeOutsideClick"]=app.Panel.Visible;
            var outsideRect=outsideButton.Current.BoundingRectangle;Native.GetCursorPos(out var beforeOutside);
            try {Native.SetCursorPos((int)(outsideRect.X+outsideRect.Width/2),(int)(outsideRect.Y+outsideRect.Height/2));MouseButton(2);Pump(50);MouseButton(4);Pump(200);}
            finally {MouseButton(4);Native.SetCursorPos(beforeOutside.X,beforeOutside.Y);}
            checks["OwnOutsideWindowReceivedClick"]=File.Exists(Path.Combine(firstFolder,"outside-click.log"));
            checks["ClickingOutsideTrayAndPanelDismissesPanel"]=!app.Panel.Visible;
            NativeTray.PostMessage((nint)outsideData.GetProperty("Window").GetInt64(),0x10,0,0);Pump(100);
            if(!app.Panel.Visible)
            {
                OpenOverflow(app.Bridge.ExplorerWindow);
                var showEntry=FindIcon(app.Bridge.ExplorerWindow,"嵌套面板")??throw new InvalidOperationException("No entry before drag-out test.");
                ((InvokePattern)showEntry.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(200);
            }
            checks["PanelVisibleBeforeDragOut"]=app.Panel.Visible;
            var tileRect=app.Panel.CellBounds(record);
            var outside=new Point(Math.Max(Screen.PrimaryScreen!.WorkingArea.Left+30,app.Panel.Left-80),app.Panel.Top+app.Panel.Height/2);
            var dragFrom=new Point(tileRect.Left+tileRect.Width/2,tileRect.Top+tileRect.Height/2);
            checks["OutDragStart"]=dragFrom.ToString();checks["OutDragEnd"]=outside.ToString();
            Native.GetCursorPos(out var cursorBeforeOut);
            var outDrag=Task.Run(()=>
            {
                try
                {
                    Native.SetCursorPos(dragFrom.X,dragFrom.Y);Thread.Sleep(80);MouseButton(2);Thread.Sleep(100);
                    for(int step=1;step<=10;step++){Native.SetCursorPos(dragFrom.X+(outside.X-dragFrom.X)*step/10,dragFrom.Y+(outside.Y-dragFrom.Y)*step/10);Thread.Sleep(40);}
                    MouseButton(4);
                }
                finally {MouseButton(4);}
            });
            try {Wait(()=>outDrag.IsCompleted);outDrag.GetAwaiter().GetResult();Pump(150);}
            finally {MouseButton(4);Native.SetCursorPos(cursorBeforeOut.X,cursorBeforeOut.Y);}
            checks["NativeDragOutRestoresFixture"]=!record.Selected&&!Settings.Load().Collected.Contains(rule);
            checks["OutDragDiagnostic"]=app.Panel.LastDragState;
            app.Bridge.Collect(record);Pump(100);
            NativeTray.PostMessage(record.Window,0x10,0,0);Wait(()=>first.HasExited);
            Wait(()=>!app.Bridge.Icons.Values.Any(value=>value.ProcessId==first.Id));
            checks["ClosedApplicationRemovedFromPanel"]=true;
            string secondFolder=Path.Combine(folder,"second");
            second=Start(fixtureExe,secondFolder,guid,legacyIdentity);app.Bridge.AllowedOwners.Add(second.Id);
            Wait(()=>app.Bridge.Icons.Values.Any(value=>value.ProcessId==second.Id&&value.Selected&&value.Version==expectedVersion));
            record=app.Bridge.Icons.Values.Single(value=>value.ProcessId==second.Id);
            checks["RestartedApplicationAutomaticallyCollected"]=record.Selected&&record.Rule==rule;
            OpenOverflow(app.Bridge.ExplorerWindow);
            checks["RestartedOriginalIconHidden"]=FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")==null;
            app.Bridge.Restore(record);Pump(150);
            checks["RestoreReturnsOriginalIcon"]=FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")!=null;
            checks["RestoreRemovesSavedRule"]=!Settings.Load().Collected.Contains(rule);
            // Simulate an orphan recovery journal for our fixture only. The old fixture's
            // process has exited; no user application or registry startup entry is changed.
            var data=NativeTray.New(record.Window,record.Id,record.Guid);data.Flags=8|(record.Guid==Guid.Empty?0u:0x20u);data.StateMask=1;data.State=1;
            NativeTray.Notify(1,ref data);Pump(100);
            string orphan=Path.Combine(folder,"orphan-recovery.json");
            File.WriteAllText(orphan,JsonSerializer.Serialize(new[]{new RecoveryItem(record.Window.ToInt64(),record.Id,record.Guid,record.ProcessId,0)}));
            RecoveryJournal.Watch(first.Id,orphan);Pump(100);
            checks["RecoveryHelperRestoresOwnOrphanJournal"]=!File.Exists(orphan)&&FindIcon(app.Bridge.ExplorerWindow,"NestedTray product fixture")!=null;
            checks["PhysicalDragNotAutomated"]=false;
            Save();
        }
        catch(Exception error){errors.Add(error.ToString());if(app!=null){checks["NativeOverflowVisibleAtFailure"]=NativeOverflow.Visible;checks["PanelVisibleAtFailure"]=app.Panel.Visible;app.Entry.DiagnosticsRequested?.Invoke();}}
        finally
        {
            app?.Dispose();
            foreach(var process in new[]{first,second,neighbor})
            {
                if(process==null)continue;
                if(!process.HasExited){process.Kill();process.WaitForExit(1000);}process.Dispose();
            }
            checks["StartupSettingUnchanged"]=Settings.StartupEnabled==startupBefore;
            nint taskbar=Native.FindWindowW("Shell_TrayWnd",null);Native.GetWindowThreadProcessId(taskbar,out uint owner);
            checks["OriginalExplorerReceiverRestored"]=taskbar!=0&&Process.GetProcessById((int)owner).ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase);
            checks["RecoveryJournalRemovedOnNormalExit"]=Directory.GetFiles(folder,"recovery-*.json").Length==0;
            // Dismiss the flyout opened by the test, without clicking any existing icon.
            using var dismiss=new Form{ShowInTaskbar=false,Opacity=0,Width=1,Height=1,StartPosition=FormStartPosition.Manual,Location=new Point(-10000,-10000)};
            dismiss.Show();dismiss.Activate();Pump(100);dismiss.Close();
            Save();
        }
        return errors.Count==0&&!checks.Where(pair=>pair.Key!="PhysicalDragNotAutomated").Any(pair=>pair.Value is false)?0:1;
    }
    private static Process Start(string exe,string folder,Guid guid,bool legacyIdentity)
    {
        var start=new ProcessStartInfo(Path.GetFullPath(exe)){UseShellExecute=false,CreateNoWindow=true,ArgumentList={folder,"NestedTray product fixture",guid.ToString("D")}};
        if(legacyIdentity)start.ArgumentList.Add("legacy-unused-guid");
        return Process.Start(start)!;
    }
    private static void MouseButton(uint flags)
    {var input=new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=flags}};if(Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>())!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
    private static void Wait(Func<bool> condition)
    {var watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>4000)throw new TimeoutException("Product integration step timed out.");Pump(20);}}
    private static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
    private static JsonElement[] ReadEvents(string folder)=>File.ReadAllLines(Path.Combine(folder,"events.jsonl")).Select(line=>JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
    private static void OpenOverflow(nint explorer)
    {
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);if(overflow!=0&&Native.IsWindowVisible(overflow))return;
        var nodes=AutomationElement.FromHandle(explorer).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
        foreach(AutomationElement node in nodes)if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons"){((InvokePattern)node.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(150);return;}
    }
    private static void ClickOverflowChevron(nint explorer,Action? afterPress=null)
    {
        var nodes=AutomationElement.FromHandle(explorer).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
        foreach(AutomationElement node in nodes)
        {
            if(node.Current.Name is not ("显示隐藏的图标" or "Show hidden icons"))continue;
            if((Native.GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Mouse is in use before tray focus regression.");
            var rect=node.Current.BoundingRectangle;Native.GetCursorPos(out var previous);
            try {Native.SetCursorPos((int)(rect.X+rect.Width/2),(int)(rect.Y+rect.Height/2));MouseButton(2);Pump(60);afterPress?.Invoke();MouseButton(4);Pump(200);}
            finally {MouseButton(4);Native.SetCursorPos(previous.X,previous.Y);}
            return;
        }
        throw new InvalidOperationException("No native overflow chevron for focus regression.");
    }
    private static AutomationElement? FindIcon(nint explorer,string name)
    {
        foreach(nint hwnd in new[]{explorer,Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null)})
        {
            if(hwnd==0||!Native.IsWindowVisible(hwnd))continue;
            var nodes=AutomationElement.FromHandle(hwnd).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
            foreach(AutomationElement node in nodes)if(!node.Current.IsOffscreen&&node.Current.Name.StartsWith(name,StringComparison.Ordinal))return node;
        }
        return null;
    }
}
