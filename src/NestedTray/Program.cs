using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class Program
{
    [STAThread]private static int Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--self-test-presentation")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return PresentationRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-material-settings")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return MaterialSettingsRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-native-material")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return NativeMaterialRegression.Run(args[1]);}
        if(args.Length==3&&args[0]=="--self-test-process-path")return ProcessExecutable.Probe(int.Parse(args[1]),args[2]);
        if(args.Length==2&&args[0]=="--self-test-auto-expand")
        {Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(args[1]));Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return AutoExpandRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-settings")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return SettingsRegression.Run(args[1]);}
        if(args.Length is 2 or 3&&args[0]=="--self-test-appearance")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return AppearanceRegression.Run(args[1],args.Length==3&&args[2]=="--capture");}
        if(args.Length==3&&args[0]=="--self-test-entry-hover")
        {Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(args[1]));Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return EntryHoverRegression.Run(args[2]);}
        if(args.Length==2&&args[0]=="--self-test-placement")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return PopupPlacementRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-experience")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return ExperienceRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-reorder-motion")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return ReorderMotionRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-panel-reorder")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return PanelReorderRegression.Run(args[1]);}
        if(args.Length==3&&args[0]=="--configure-startup")return StartupRegistration.Configure(args[1],args[2]);
        if(args.Length==2&&args[0]=="--self-test-startup")return StartupRegression.Run(args[1]);
        if(args.Length==2&&args[0]=="--self-test-tray-connection")return TrayConnectionRegression.Run(args[1]);
        if(args.Length is 2 or 3&&args[0]=="--self-test-fixed-tools")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return FixedToolsRegression.Run(args[1],args.Length==3?args[2]:"");}
        if(args.Length==2&&args[0]=="--self-test-diagnostics")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return DiagnosticsRegression.Run(args[1]);}
        if(args.Length==4&&args[0]=="--self-test-crash-collector")
        {
            Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(args[1]));
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            return ReliabilityRegression.CrashCollector(int.Parse(args[2]),args[3]);
        }
        if(args.Length==2&&args[0]=="--self-test-animation")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return DismissAnimationRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-layout")
        {Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return PanelLayoutRegression.Run(args[1]);}
        if(args.Length==2&&args[0]=="--self-test-menu")
        {Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(args[1]));Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return OwnMenuRegression.Run();}
        if(args.Length==3&&args[0]=="--recover-after-exit"){RecoveryJournal.Watch(int.Parse(args[1]),args[2]);return 0;}
        if(args.Length==2&&args[0]=="--restart-after-exit")
        {
            try {using var parent=Process.GetProcessById(int.Parse(args[1]));parent.WaitForExit();}catch(ArgumentException){}
            var wait=Stopwatch.StartNew();
            while(Native.FindWindowW("Shell_TrayWnd",null)==0&&wait.ElapsedMilliseconds<10000)Thread.Sleep(200);
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});return 0;
        }
        if(args.Length==3&&args[0] is "--self-test" or "--self-test-hidden" or "--self-test-legacy" or "--self-test-ole" or "--self-test-ole-live" or "--self-test-reliability")
        {
            Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(args[1]));
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            return args[0]=="--self-test-reliability"?ReliabilityRegression.Run(args[2]):args[0]=="--self-test-hidden"?HiddenStateRegression.Run(args[2]):args[0]=="--self-test-ole-live"?ExternalOleRegression.Run(args[2]):ProductSelfTest.Run(args[2],args[0]=="--self-test-ole",args[0]=="--self-test-legacy");
        }
        using var single=new Mutex(true,"Local\\NestedTray-"+Environment.UserName,out bool created);
        if(!created)return 0;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        TrayApplication? running=null;bool failing=false;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,eventArgs)=>
        {
            if(failing)return;failing=true;
            var problem=AppDiagnostics.Error("TN9001",eventArgs.Exception);
            try{running?.Shutdown();}catch(Exception cleanup){AppDiagnostics.Error("TN6001",cleanup);}
            DiagnosticUi.Show(problem);Application.Exit();
        };
        AppDomain.CurrentDomain.UnhandledException+=(_,eventArgs)=>
        {try{AppDiagnostics.Error("TN9002",eventArgs.ExceptionObject as Exception);}catch{/* The watchdog remains independent of logging. */}};
        try
        {
            AppDiagnostics.Info("TN0001",new(){["LaunchMode"]=args.Contains("--startup")?"Logon":"Manual"});CompatibilityProbe.EnsureStartup(args.Contains("--startup"));
            using var app=new TrayApplication();running=app;Application.Run(app);return failing?1:0;
        }
        catch(Exception error)
        {DiagnosticUi.Error(error is TrayNestException known?known.Code:"TN1003",error);return 1;}
        finally{AppDiagnostics.State=null;}
    }
}

internal sealed class TrayApplication:ApplicationContext
{
    private readonly Settings settings=Settings.Load();
    private readonly RecoveryJournal recovery=new();
    private readonly TrayPanel panel=new();
    private readonly ToolLibrary library;
    private readonly ToolLauncher launcher=new();
    private readonly ContextMenuStrip offlineMenu=new();
    private EntryIcon? entry;
    private TrayBridge? bridge;
    private DragObserver? drag;
    private readonly System.Windows.Forms.Timer timer=new(){Interval=30};
    private readonly System.Windows.Forms.Timer discovery=new(){Interval=1500};
    private readonly ContextMenuStrip menu=new();
    private readonly Form menuOwner=new(){ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Size=new Size(1,1),Opacity=0};
    private IconRecord? candidate;
    private IconRecord? releasedCandidate;
    private readonly System.Windows.Forms.Timer finishDrag=new(){Interval=150};
    private Point releasedPoint;
    private int initialDragEnters,initialDrops;
    private int initialCoreEnters,initialCoreDrops;
    private bool cancelledDrag;
    private bool sourceWasOverflow,pairedWithOverflow,restoreOverflowRequested;
    private long panelOpenedUntil,restoreOverflowUntil;
    private long releasedUntil;
    private bool lastNativeVisibility;
    private bool lastNativeFocused;
    private Rectangle chevronBounds;
    private bool ignoreNativeCloseForPanelInteraction,escapeWasDown;
    private IconRecord? contextIcon;
    private readonly Dictionary<nint,IconRecord> testDragSources=new();
    private readonly Queue<object> dragHistory=new();
    private Point down;
    private bool stopping;
    private readonly AutoExpand autoExpand=new();
    private readonly NativePanelGeometry nativeGeometry=new();
    private bool refreshPanelPending;
    private readonly NativeMaterialController nativeMaterial=new();
    private bool updatingMaterial;
    private bool changingMaterialSettings;
    private long materialRetryAfter;
    private SettingsWindow? settingsWindow;
    private int ticks;
    private readonly long launchedAt=Environment.TickCount64;
    private int startupCheckpoints;
    private long suppressMenuUntil;
    private Rectangle menuEntryAnchor;
    private long menuOpenedAt;
    internal ContextMenuStrip OwnMenu=>menu;
    internal TrayBridge Bridge=>bridge!;
    internal TrayPanel Panel=>panel;
    internal EntryIcon Entry=>entry!;
    internal bool HasDragCandidate=>candidate!=null;
    internal TrayApplication(bool testMode=false)
    {
        library=new ToolLibrary(settings);
        _=panel.Handle;
        try
        {
            recovery.Start();entry=new EntryIcon();bridge=new TrayBridge(settings,recovery);
            entry.DiagnosticsRequested=SaveDiagnostics;
            entry.SettingsRequested=()=>Guard(ShowSettings);
            if(testMode)bridge.AllowedOwners=new();
            panel.UseSavedOrder=true;panel.FixedColumns=settings.Columns;
            panel.NativeGeometry=nativeGeometry;
            panel.ToolStatus=launcher.Status;
            bridge.Changed+=()=>Guard(RefreshPanel);
            panel.Reordered=(record,index)=>Guard(()=>{library.Reorder(record.Rule,index);RefreshPanel();});
            panel.OfflineInput=(record,code)=>Guard(()=>{BeginPanelInteraction();if(code==0x7B)ShowOfflineMenu(record);else LaunchTool(record);});
            offlineMenu.Closed+=(_,_)=>menuOwner.Hide();
            RefreshPanel();
            bridge.Faulted+=(code,error)=>panel.BeginInvoke((Action)(()=>
            {
                if(stopping)return;
                if(!bridge.ExplorerAlive){Restart();return;}
                var problem=AppDiagnostics.Error(code,error);Stop();if(DiagnosticUi.OpenWindows==0)DiagnosticUi.Show(problem);
            }));
            bridge.Bounds=record=>panel.CellBounds(record);
            entry.Open=()=>{autoExpand.SuppressCurrentCycle(NativeOverflow.Visible);UpdateEntryBounds();if(panel.WantsVisible)panel.Dismiss();else panel.Open();};
            entry.Menu=()=>ShowMenu();
            // The standard refresh broadcast also reaches this window, so restart detection
            // is based on the actual Explorer handle rather than the message alone.
            entry.ExplorerRestarted=()=>{if(bridge!=null&&!bridge.ExplorerAlive)panel.BeginInvoke((Action)Restart);};
            panel.PanelInteraction=BeginPanelInteraction;
            panel.IconInput=(record,code,rect)=>
            {
                BeginPanelInteraction();
                if(code==0x7B)
                {
                    contextIcon=record;Native.AllowSetForegroundWindow((uint)record.ProcessId);Native.SetForegroundWindow(record.Window);
                }
                Guard(()=>bridge?.ForwardInput(record,code,rect));
            };
            panel.CanAcceptNativeDrag=()=>!cancelledDrag&&(candidate??releasedCandidate)is {CanCollect:true,Selected:false};
            panel.NativeDragActive=()=>candidate!=null||releasedCandidate!=null;
            panel.NativeDropReceived=point=>
            {
                releasedCandidate??=candidate;
                releasedPoint=point;
                finishDrag.Stop();finishDrag.Start();
            };
            finishDrag.Tick+=(_,_)=>CompleteNativeDrop();
            panel.RemoveIcon=record=>Guard(()=>{if(record.IsOffline){library.Forget(record.Rule);RefreshPanel();}else bridge.Restore(record);});panel.OpenMenu=ShowMenu;
            panel.DismissStarted=()=>{autoExpand.SuppressCurrentCycle(NativeOverflow.Visible);pairedWithOverflow=false;restoreOverflowUntil=0;contextIcon=null;};
            panel.VisibleChanged+=(_,_)=>{pairedWithOverflow=panel.Visible&&NativeOverflow.Visible;ignoreNativeCloseForPanelInteraction=false;panelOpenedUntil=Environment.TickCount64+700;if(!panel.Visible){restoreOverflowUntil=0;contextIcon=null;}};
            BuildMenu();bridge.Start();panel.TaskbarWindow=bridge.ExplorerWindow;chevronBounds=NativeOverflow.ChevronBounds(bridge.ExplorerWindow);
            AppDiagnostics.State=DiagnosticState;
            if(!panel.CoreDrop.Registered)AppDiagnostics.Warn("TN1102");
            drag=new DragObserver(panel);
            drag.Down=PointerDown;drag.Up=PointerUp;drag.OtherDown=OtherPointerDown;
            timer.Tick+=(_,_)=>
            {
                if(bridge!=null&&!bridge.ExplorerAlive){Restart();return;}
                nativeGeometry.Tick(!panel.IsAnimating&&candidate==null&&releasedCandidate==null&&!panel.IsReordering&&(Native.GetAsyncKeyState(1)&0x8000)==0);
                if(panel.IsAnimating)
                {
                    if((Native.GetAsyncKeyState((int)Keys.Escape)&0x8000)!=0)panel.Dismiss();
                    SyncOverflowVisibility();return;
                }
                if(refreshPanelPending){refreshPanelPending=false;RefreshPanel();}
                bridge?.Maintain();if(bridge?.Reconnecting==true)return;
                if(!testMode&&startupCheckpoints<2&&Environment.TickCount64-launchedAt>=(startupCheckpoints==0?5000:30000))
                {
                    startupCheckpoints++;
                    AppDiagnostics.Info("TN0012",new(){["Checkpoint"]=startupCheckpoints.ToString(),["SavedTools"]=settings.Tools.Count.ToString(),["CollectedIcons"]=bridge!.Icons.Values.Count(icon=>icon.Selected).ToString(),["KnownIcons"]=bridge.Icons.Count.ToString()});
                }
                if(++ticks%7==0){UpdateEntryBounds();CacheNativeIconBounds();}if(ticks%33==0){bridge?.PruneClosed();if(candidate==null&&releasedCandidate==null&&(Native.GetAsyncKeyState(1)&0x8000)==0)chevronBounds=NativeOverflow.ChevronBounds(bridge!.ExplorerWindow);}
                if(ticks%33==0&&panel.Visible)panel.RefreshToolStates();
                if((candidate!=null||releasedCandidate!=null)&&(Native.GetAsyncKeyState((int)Keys.Escape)&0x8000)!=0){cancelledDrag=true;candidate=null;releasedCandidate=null;finishDrag.Stop();panel.DragPreview=false;panel.SetIncomingIcon(null);}
                bool escape=(Native.GetAsyncKeyState((int)Keys.Escape)&0x8000)!=0;
                bool reorderEscape=panel.HandleReorderEscape(escape);
                if(!reorderEscape&&escape&&!escapeWasDown&&menu.Visible)menu.Close(ToolStripDropDownCloseReason.Keyboard);
                else if(!reorderEscape&&escape&&!escapeWasDown&&(panel.Visible||panel.IsPreparing)&&!cancelledDrag&&!IsContextSurface(Native.GetForegroundWindow()))panel.Dismiss();
                escapeWasDown=escape;
                if(candidate!=null&&Native.GetCursorPos(out var point))
                {
                    var cursor=new Point(point.X,point.Y);
                    if(Math.Abs(cursor.X-down.X)+Math.Abs(cursor.Y-down.Y)>=SystemInformation.DragSize.Width&&panel.EntryRectangle.Contains(cursor)&&!panel.Visible)panel.Open(true);
                    bool overTarget=panel.Visible&&panel.Bounds.Contains(cursor)&&Math.Abs(cursor.X-down.X)+Math.Abs(cursor.Y-down.Y)>=SystemInformation.DragSize.Width;
                    panel.SetIncomingIcon(overTarget&&candidate.CanCollect?candidate:null);
                }
                UpdateAutomaticExpansion();
                if(ticks%16==0)UpdateNativeMaterial();
                SyncOverflowVisibility();
                panel.FollowNativeBounds();
                if(ticks%8==0&&panel.Visible&&!menu.Visible&&DiagnosticUi.OpenWindows==0&&!HasContextPopup())panel.EnsureAboveApplications();
            };
            timer.Start();discovery.Tick+=(_,_)=>{discovery.Stop();bridge?.RefreshExisting();};if(!testMode)discovery.Start();
        }
        catch {Stop();throw;}
    }
    internal void PointerDown(Point point,nint sourceRoot,bool nativeVisibleOnPress,long observedAt=0)
    {
        if(HandleOwnMenuPress(point,false,observedAt))return;
        finishDrag.Stop();releasedCandidate=null;cancelledDrag=false;panel.SetIncomingIcon(null);restoreOverflowUntil=0;
        initialDragEnters=panel.ExternalDragEnters;initialDrops=panel.ExternalDrops;
        initialCoreEnters=panel.CoreDrop.Enters;initialCoreDrops=panel.CoreDrop.Drops;
        candidate=null;down=point;
        if((panel.Visible||panel.IsPreparing)&&nativeVisibleOnPress&&sourceRoot==bridge!.ExplorerWindow&&chevronBounds.Contains(point))
        {TraceDrag("native-chevron-close-start",new{BeforeNativeHidden=true});panel.Dismiss();return;}
        // Controls handle their own MouseDown synchronously. The hook's queued copy
        // may arrive after MouseUp has opened a new application menu.
        if(sourceRoot==panel.Handle||panel.IsDropSurface(point))return;
        UpdateEntryBounds();
        bool fromSystemTray=IsSystemTrayWindow(sourceRoot);
        if(fromSystemTray&&panel.Visible){ignoreNativeCloseForPanelInteraction=false;panelOpenedUntil=0;pairedWithOverflow=NativeOverflow.Visible;}
        sourceWasOverflow=sourceRoot==NativeOverflow.Window&&NativeOverflow.Visible;
        if(sourceWasOverflow&&panel.Visible)pairedWithOverflow=true;
        if(bridge!.AllowedOwners!=null&&testDragSources.TryGetValue(sourceRoot,out var fixture))candidate=fixture;
        else if(fromSystemTray)
        {
            var available=bridge!.Icons.Values.Where(record=>!record.Selected).ToArray();
            candidate=available.FirstOrDefault(record=>Native.GetIconRect(record.Window,record.Id,record.Guid).Contains(point));
            candidate??=available.FirstOrDefault(record=>record.NativeBounds.Contains(point));
            if(candidate is {CanCollect:false})NativeTray.PostMessage(candidate.Window,Native.RegisterWindowMessageW("TaskbarCreated"),0,0);
        }
        if(panel.Visible&&candidate==null&&!panel.EntryRectangle.Contains(point)&&!fromSystemTray&&!IsContextSurface(sourceRoot)&&!IsOwnPopup(sourceRoot))
        {TraceDrag("outside-click-close",new{Point=point.ToString(),Class=WindowClass(sourceRoot),PanelBounds=panel.Bounds.ToString()});panel.Dismiss();}
        if(fromSystemTray||panel.Visible)TraceDrag("down",new {Point=point.ToString(),SourceClass=WindowClass(sourceRoot),FromSystemTray=fromSystemTray,KnownIcons=bridge.Icons.Count,Candidate=candidate==null?null:new {Process=candidate.ProcessId,candidate.CanCollect,candidate.RegistrationKnown,candidate.Callback}});
    }
    internal void PointerUp(Point point)
    {
        UpdateEntryBounds();
        var item=candidate;candidate=null;bool moved=Math.Abs(point.X-down.X)+Math.Abs(point.Y-down.Y)>=SystemInformation.DragSize.Width;
        bool dropping=item!=null&&moved&&((panel.Visible&&panel.Bounds.Contains(point))||panel.EntryRectangle.Contains(point));
        if(dropping&&!cancelledDrag){releasedCandidate=item;releasedPoint=point;releasedUntil=Environment.TickCount64+2500;finishDrag.Stop();finishDrag.Start();}
        else if(releasedCandidate==null){panel.DragPreview=false;panel.SetIncomingIcon(null);}
        if(item!=null||panel.Visible)TraceDrag("up",new {Point=point.ToString(),HasCandidate=item!=null,Moved=moved,Dropping=dropping,PanelVisible=panel.Visible,OleEnters=panel.ExternalDragEnters-initialDragEnters,OleDrops=panel.ExternalDrops-initialDrops});
    }
    internal void RegisterTestDragSource(nint source,IconRecord record)
    {
        Native.GetWindowThreadProcessId(source,out uint owner);
        if(bridge?.AllowedOwners?.Contains((int)owner)!=true||owner!=record.ProcessId)throw new InvalidOperationException("Only the integration fixture may supply a test OLE source.");
        testDragSources[source]=record;
    }
    private void CompleteNativeDrop()
    {
        finishDrag.Stop();
        bool awaitingProtocol=(panel.CoreDrop.Enters>initialCoreEnters&&panel.CoreDrop.Drops==initialCoreDrops)||(panel.ExternalDragEnters>initialDragEnters&&panel.ExternalDrops==initialDrops);
        if(releasedCandidate!=null&&awaitingProtocol&&Environment.TickCount64<releasedUntil){finishDrag.Start();return;}
        var record=releasedCandidate;releasedCandidate=null;
        bool protocolDrop=panel.ExternalDrops>initialDrops||panel.CoreDrop.Drops>initialCoreDrops;
        bool ontoPanel=panel.Visible&&panel.Bounds.Contains(releasedPoint)&&(protocolDrop||panel.IsDropSurface(releasedPoint));
        bool oleAccepted=(panel.ExternalDragEnters==initialDragEnters||panel.ExternalDrops>initialDrops)&&(panel.CoreDrop.Enters==initialCoreEnters||panel.CoreDrop.Drops>initialCoreDrops);
        if(!cancelledDrag&&record is {CanCollect:true,Selected:false}&&bridge!.Icons.TryGetValue(record.Key,out var live)&&ReferenceEquals(live,record)&&(ontoPanel&&oleAccepted||panel.EntryRectangle.Contains(releasedPoint)))
            Guard(()=>bridge.Collect(record));
        else if(!cancelledDrag&&record is {CanCollect:false}&&(ontoPanel||panel.EntryRectangle.Contains(releasedPoint)))
        {if(AppDiagnostics.Warn("TN2101")!=null)entry?.Notify("TN2101");}
        panel.DragPreview=false;panel.SetIncomingIcon(null);
        panel.RequestRealignment();
        if(record?.Selected==true&&panel.Visible&&sourceWasOverflow)
        {pairedWithOverflow=true;restoreOverflowUntil=Environment.TickCount64+800;restoreOverflowRequested=false;}
        TraceDrag("complete",new {HasCandidate=record!=null,CanCollect=record?.CanCollect,Selected=record?.Selected,OntoPanel=ontoPanel,OleAccepted=oleAccepted,Cancelled=cancelledDrag});
    }
    private void SyncOverflowVisibility()
    {
        if(panel.IsPreparing&&!NativeOverflow.Visible){panel.Dismiss();return;}
        if(!panel.Visible||panel.IsDismissing)return;
        long now=Environment.TickCount64;bool visible=NativeOverflow.Visible;
        nint foreground=Native.GetAncestor(Native.GetForegroundWindow(),2);
        bool nativeFocused=foreground!=0&&foreground==NativeOverflow.Window;
        bool focusClosing=lastNativeFocused&&!nativeFocused&&!IsOwnPopup(foreground)&&!IsContextSurface(foreground)&&!menu.Visible;
        lastNativeFocused=nativeFocused;
        if(visible!=lastNativeVisibility){lastNativeVisibility=visible;TraceDrag("native-visibility",new {Visible=visible,Paired=pairedWithOverflow,OpeningGrace=now<panelOpenedUntil,HasCandidate=candidate!=null});}
        if(visible&&now>=panelOpenedUntil)pairedWithOverflow=true;
        if(restoreOverflowUntil>now)
        {
            if(!visible&&!restoreOverflowRequested)
            {restoreOverflowRequested=true;TraceDrag("restore-native-overflow",new {Requested=NativeOverflow.TryOpen(bridge!.ExplorerWindow)});}
            return;
        }
        if(pairedWithOverflow&&(!visible||focusClosing)&&!ignoreNativeCloseForPanelInteraction&&candidate==null&&releasedCandidate==null&&(Native.GetAsyncKeyState(1)&0x8000)==0)
        {TraceDrag("native-overflow-close-start",new {FocusClosing=focusClosing});panel.Dismiss();}
    }
    private void UpdateAutomaticExpansion()
    {
        bool nativeVisible=NativeOverflow.Visible;
        bool blocked=candidate!=null||releasedCandidate!=null||panel.IsReordering||panel.HasReorderPreview||panel.IsDismissing||
            (Native.GetAsyncKeyState(1)&0x8000)!=0||(Native.GetAsyncKeyState(2)&0x8000)!=0||menu.Visible||offlineMenu.Visible||
            settingsWindow is {Visible:true,WindowState:not FormWindowState.Minimized}||DiagnosticUi.OpenWindows>0||HasContextPopup();
        bool overEntry=Native.GetCursorPos(out var pointer)&&panel.EntryRectangle.Contains(new Point(pointer.X,pointer.Y));
        var reason=autoExpand.Evaluate(Environment.TickCount64,settings.FollowNativeOverflow,settings.HoverToOpen,nativeVisible,panel.Visible||panel.IsPreparing,overEntry,blocked);
        if(reason==AutoExpandReason.None)return;
        UpdateEntryBounds();
        bool following=reason==AutoExpandReason.NativeOverflow;
        panel.Open(waitForNative:following&&settings.ExpansionOrder==NativeExpansionOrder.AfterNative,simultaneous:following&&settings.ExpansionOrder==NativeExpansionOrder.Simultaneous);
        if(nativeVisible)pairedWithOverflow=true;
        TraceDrag("auto-expand",new{Reason=reason.ToString()});
    }
    private void ShowSettings()
    {
        menu.Close();offlineMenu.Close();autoExpand.SuppressCurrentCycle(NativeOverflow.Visible);panel.Dismiss();
        if(settingsWindow==null||settingsWindow.IsDisposed)
        {
            settingsWindow=new SettingsWindow(settings,RefreshPanel,()=>Settings.StartupEnabled,value=>Settings.StartupEnabled=value);
            settingsWindow.OpenDiagnostics=()=>DiagnosticUi.Show(AppDiagnostics.Store.LastProblem);
            settingsWindow.OpenPanelRequested=()=>{settingsWindow.WindowState=FormWindowState.Minimized;UpdateEntryBounds();panel.Open();};
            settingsWindow.RefreshIconsRequested=()=>Guard(()=>bridge?.RefreshExisting());
            settingsWindow.RestoreAllRequested=()=>Guard(RestoreAllIcons);
            settingsWindow.ExitRequested=Stop;
            settingsWindow.ChangeMaterialSync=ChangeMaterialSyncAsync;
        }
        else settingsWindow.RefreshValues();
        if(settingsWindow.WindowState==FormWindowState.Minimized)settingsWindow.WindowState=FormWindowState.Normal;
        settingsWindow.Show();settingsWindow.Activate();
    }
    private async Task ChangeMaterialSyncAsync(bool enabled)
    {
        bool previous=settings.MatchPanelMaterials;
        changingMaterialSettings=true;
        try
        {
            while(updatingMaterial)await Task.Delay(30);
            if(enabled)
            {
                if(!NativeOverflow.Visible)NativeOverflow.TryOpen(bridge!.ExplorerWindow);
                nint root=NativeOverflow.Window;if(root==0)throw new NativeMaterialException("请先打开系统隐藏图标区，再开启材质同步。");
                Native.GetWindowThreadProcessId(root,out uint owner);
                await nativeMaterial.ConnectAsync(root,(int)owner);
            }
            var appearance=AppearanceForMaterial(enabled);
            int sequence=nativeMaterial.Apply(appearance,enabled,PanelTheme.Light(appearance),PanelTheme.TransparencyEnabled,SystemInformation.HighContrast);
            if(nativeMaterial.Connected)await nativeMaterial.WaitForAppliedAsync(sequence);
            settings.ChangeBehavior(settings.FollowNativeOverflow,settings.HoverToOpen,enabled);
            RefreshPanel();
        }
        catch{var appearance=AppearanceForMaterial(previous);nativeMaterial.Apply(appearance,previous,PanelTheme.Light(appearance),PanelTheme.TransparencyEnabled,SystemInformation.HighContrast);throw;}
        finally{changingMaterialSettings=false;}
    }
    private PanelAppearance AppearanceForMaterial(bool match)=>match&&settings.Appearance==PanelAppearance.FollowSystem?PanelAppearance.Acrylic:settings.Appearance;
    private async void UpdateNativeMaterial()
    {
        if(stopping||updatingMaterial||changingMaterialSettings)return;
        if(!settings.MatchPanelMaterials){nativeMaterial.Restore();return;}
        if(Environment.TickCount64<materialRetryAfter||(!NativeOverflow.Visible&&!nativeMaterial.Connected)||candidate!=null||releasedCandidate!=null||panel.IsReordering||(Native.GetAsyncKeyState(1)&0x8000)!=0)return;
        updatingMaterial=true;
        try
        {
            nint root=NativeOverflow.Window;Native.GetWindowThreadProcessId(root,out uint owner);
            await nativeMaterial.ConnectAsync(root,(int)owner);
            var appearance=AppearanceForMaterial(true);
            int sequence=nativeMaterial.Apply(appearance,true,PanelTheme.Light(appearance),PanelTheme.TransparencyEnabled,SystemInformation.HighContrast);
            await nativeMaterial.WaitForAppliedAsync(sequence);
        }
        catch(OperationCanceledException){}
        catch(ObjectDisposedException){}
        catch(Exception error){materialRetryAfter=Environment.TickCount64+15000;settingsWindow?.ShowOperationStatus(error is NativeMaterialException?error.Message:"主面板材质暂时无法同步。",true);AppDiagnostics.Warn("TN4201",error:error);}
        finally{updatingMaterial=false;}
    }
    private void TraceDrag(string phase,object detail)
    {
        dragHistory.Enqueue(new {Time=DateTimeOffset.Now,Phase=phase,Detail=detail});
        while(dragHistory.Count>32)dragHistory.Dequeue();
    }
    private static string WindowClass(nint window)
    {var name=new System.Text.StringBuilder(256);Native.GetClassNameW(window,name,256);return name.ToString();}
    private Dictionary<string,object?> DiagnosticState()=>new()
    {
        ["KnownIcons"]=bridge?.Icons.Count??0,["CollectedIcons"]=bridge?.Icons.Values.Count(value=>value.Selected)??0,
        ["SavedTools"]=settings.Tools.Count,["SavedRules"]=settings.Collected.Count,["StartupEnabled"]=Settings.StartupEnabled,
        ["MissingInteractionMetadata"]=bridge?.Icons.Values.Count(value=>!value.CanCollect)??0,
        ["HiddenStateReapplications"]=bridge?.HiddenStateReapplications??0,["PanelVisible"]=panel.Visible,["PanelDpi"]=panel.DeviceDpi,
        ["NativeOverflowVisible"]=NativeOverflow.Visible,["ExplorerAlive"]=bridge?.ExplorerAlive??false,
        ["CoreDropRegistered"]=panel.CoreDrop.Registered,["CoreDragRequests"]=panel.CoreDrop.Requests,["CoreDragEnters"]=panel.CoreDrop.Enters,["CoreDrops"]=panel.CoreDrop.Drops,
        ["OleEnters"]=panel.ExternalDragEnters,["OleDrops"]=panel.ExternalDrops,
        ["PacketLayouts"]=bridge==null?"":string.Join(";",bridge.PacketLayouts.Select(pair=>$"{pair.Key} x{pair.Value}")),
        ["UnrecognizedPacketCount"]=bridge?.UnrecognizedPacketCount??0
    };
    private void SaveDiagnostics()
    {
        try
        {
            var report=new {Time=DateTimeOffset.Now,Process=Environment.ProcessId,PacketLayouts=bridge!.PacketLayouts,bridge.HiddenStateReapplications,CoreDrop=new{panel.CoreDrop.Status,panel.CoreDrop.Requests,panel.CoreDrop.Enters,panel.CoreDrop.Overs,panel.CoreDrop.Drops,panel.CoreDrop.LastEffect},Icons=bridge.Icons.Values.ToArray().Select(record=>new {record.ProcessId,Executable=Path.GetFileName(record.Executable),Window=record.Window.ToInt64(),record.Id,record.Guid,WindowValid=Native.IsWindow(record.Window),record.Callback,record.Version,record.RegistrationKnown,record.CanCollect,record.Selected,NativeBounds=Native.GetIconRect(record.Window,record.Id,record.Guid).ToString(),OriginalHostBounds=bridge.ReadOriginalBounds(record).ToString(),BoundsWithoutGuid=Native.GetIconRect(record.Window,record.Id,Guid.Empty).ToString(),CachedBounds=record.NativeBounds.ToString()}),Events=dragHistory.ToArray()};
            File.WriteAllText(Path.Combine(Settings.DirectoryPath,"diagnostics.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(IOException){}catch(UnauthorizedAccessException){}
    }
    private void CacheNativeIconBounds()
    {
        if(candidate!=null||releasedCandidate!=null||bridge==null)return;
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
        if(!panel.Visible&&(overflow==0||!Native.IsWindowVisible(overflow)))return;
        foreach(var record in bridge.Icons.Values.Where(value=>!value.Selected))
        {var rect=Native.GetIconRect(record.Window,record.Id,record.Guid);if(!rect.IsEmpty)record.NativeBounds=rect;}
    }
    private bool IsSystemTrayWindow(nint window)
    {
        if(window==0)return false;
        nint root=Native.GetAncestor(window,2);
        if(root==0)return false;
        return root==bridge?.ExplorerWindow||root==Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null)||root==Native.FindWindowW("NotifyIconOverflowWindow",null);
    }
    private void BeginPanelInteraction()
    {
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out uint foregroundOwner);
        if(contextIcon!=null&&foregroundOwner==contextIcon.ProcessId)Native.SetForegroundWindow(panel.Handle);
        contextIcon=null;ignoreNativeCloseForPanelInteraction=true;pairedWithOverflow=false;restoreOverflowUntil=0;
    }
    private bool IsContextSurface(nint root)
    {
        if(contextIcon==null||root==0||!Native.IsWindowVisible(root))return false;
        Native.GetWindowThreadProcessId(root,out uint owner);
        return owner==contextIcon.ProcessId&&(Native.GetWindowLongPtrW(root,-16).ToInt64()&0x80000000L)!=0;
    }
    private bool HasContextPopup()
    {
        if(contextIcon==null)return false;
        bool found=false;Native.WindowVisitor visitor=(window,_)=>{if(IsContextSurface(window)){found=true;return false;}return true;};
        Native.EnumWindows(visitor,0);GC.KeepAlive(visitor);return found;
    }
    private static bool IsOwnPopup(nint root)
    {Native.GetWindowThreadProcessId(root,out uint owner);return owner==Environment.ProcessId;}
    internal void OtherPointerDown(Point point,nint sourceRoot,long observedAt=0)
    {
        if(HandleOwnMenuPress(point,true,observedAt))return;
        if(!panel.Visible)return;
        if(sourceRoot==panel.Handle||panel.IsDropSurface(point))return;
        if(IsSystemTrayWindow(sourceRoot)){ignoreNativeCloseForPanelInteraction=false;panelOpenedUntil=0;pairedWithOverflow=NativeOverflow.Visible;return;}
        if(!IsContextSurface(sourceRoot)&&!IsOwnPopup(sourceRoot))panel.Dismiss();
    }
    private void UpdateEntryBounds(){if(entry!=null){var rect=entry.Bounds;if(!rect.IsEmpty)panel.EntryRectangle=rect;}}
    private void RefreshPanel()
    {
        if(panel.IsPreparing&&(!settings.FollowNativeOverflow||settings.ExpansionOrder!=NativeExpansionOrder.AfterNative))panel.Dismiss();
        if(panel.IsAnimating){refreshPanelPending=true;return;}
        var records=bridge?.Icons.Values.ToArray()??[];
        if(library.EnsureEntries(records))settings.Save();
        var ordered=library.Snapshot(records);foreach(var record in ordered.Where(record=>!record.IsOffline))launcher.Registered(record.Rule);
        panel.SetAppearance(AppearanceForMaterial(settings.MatchPanelMaterials));panel.SetPlacement(settings.Placement);panel.FixedColumns=settings.Columns;panel.Render(ordered);
    }
    private void LaunchTool(IconRecord record)
    {
        var saved=settings.Tools.FirstOrDefault(tool=>tool.Rule==record.Rule);if(saved==null)return;
        var result=launcher.Launch(saved);panel.RefreshToolStates();panel.ShowToolStatus(record,result.Message);
    }
    private void ShowOfflineMenu(IconRecord record)
    {
        menu.Close();offlineMenu.Items.Clear();
        offlineMenu.Items.Add("尝试启动",null,(_,_)=>Guard(()=>LaunchTool(record)));
        offlineMenu.Items.Add("设置启动文件…",null,(_,_)=>Guard(()=>
        {
            var saved=settings.Tools.FirstOrDefault(tool=>tool.Rule==record.Rule);if(saved==null)return;
            using var dialog=new OpenFileDialog{Title="选择此工具的程序或快捷方式",Filter="程序或快捷方式 (*.exe;*.lnk)|*.exe;*.lnk",CheckFileExists=true};
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            string previous=saved.LaunchTarget;saved.LaunchTarget=dialog.FileName;try{settings.Save();}catch{saved.LaunchTarget=previous;throw;}
            panel.ShowToolStatus(record,"启动文件已保存；单击图标可尝试启动。");
        }));
        offlineMenu.Items.Add("移出收纳",null,(_,_)=>Guard(()=>{library.Forget(record.Rule);RefreshPanel();}));
        menuOwner.Location=Cursor.Position;menuOwner.Show();Native.SetForegroundWindow(menuOwner.Handle);menuOpenedAt=Environment.TickCount64;offlineMenu.Show(menuOwner,Point.Empty);
    }
    private void Guard(Action action){try{action();}catch(Exception error){DiagnosticUi.Error(error is TrayNestException known?known.Code:"TN3002",error);}}
    private void BuildMenu()
    {
        menu.AutoClose=true;
        menu.Closed+=(_,args)=>{AppDiagnostics.Info("TN0101",new(){["Operation"]="MenuClosed",["Reason"]=args.CloseReason.ToString()});menuOwner.Hide();if(entry!=null)NativeTray.PostMessage(entry.Handle,0,0,0);};
        menu.Items.Add("设置",null,(_,_)=>Guard(ShowSettings));
    }
    private void RestoreAllIcons()
    {
        using var confirm=BulkRestoreDialog.Create();
        if(confirm.ShowDialog(settingsWindow)!=DialogResult.OK||bridge==null)return;
        var result=bridge.RestoreAll(true);RefreshPanel();MessageBox.Show(settingsWindow,result.Message,"TrayNest",MessageBoxButtons.OK,result.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
    }
    internal static ToolStripMenuItem CreatePlacementMenu(Settings settings,Action<PopupPlacement> apply,Action<Action> guard)
    {
        var placement=new ToolStripMenuItem("弹出位置");
        foreach(var side in new[]{PopupPlacement.Left,PopupPlacement.Above})
        {
            var item=new ToolStripMenuItem(side==PopupPlacement.Left?"左侧":"上方"){Checked=settings.Placement==side};
            item.Click+=(_,_)=>guard(()=>{settings.ChangePlacement(side);foreach(ToolStripMenuItem sibling in placement.DropDownItems)sibling.Checked=sibling==item;apply(side);});
            placement.DropDownItems.Add(item);
        }
        return placement;
    }
    internal static ToolStripMenuItem CreateAppearanceMenu(Settings settings,Action<PanelAppearance> apply,Action<Action> guard)
    {
        var menu=new ToolStripMenuItem("面板外观");
        foreach(var option in new[]{(PanelAppearance.Acrylic,"亚克力"),(PanelAppearance.Light,"浅色"),(PanelAppearance.Dark,"深色"),(PanelAppearance.FollowSystem,"跟随系统托盘")})
        {
            var item=new ToolStripMenuItem(option.Item2){Checked=settings.Appearance==option.Item1};
            item.Click+=(_,_)=>guard(()=>{settings.ChangeAppearance(option.Item1);apply(option.Item1);foreach(ToolStripMenuItem sibling in menu.DropDownItems)sibling.Checked=sibling==item;});
            menu.DropDownItems.Add(item);
        }
        return menu;
    }
    private bool HandleOwnMenuPress(Point point,bool right,long observedAt=0)
    {
        if(offlineMenu.Visible){if(observedAt>0&&observedAt<=menuOpenedAt||ContainsMenuPoint(offlineMenu,point))return true;offlineMenu.Close(ToolStripDropDownCloseReason.AppClicked);}
        if(!menu.Visible)return false;
        if(observedAt>0&&observedAt<=menuOpenedAt)return true;
        if(ContainsMenuPoint(menu,point))return true;
        if(right&&menuEntryAnchor.Contains(point))suppressMenuUntil=Environment.TickCount64+500;
        AppDiagnostics.Info("TN0101",new(){["Operation"]="OutsideMenuPress",["Button"]=right?"Right":"Left"});
        menu.Close(ToolStripDropDownCloseReason.AppClicked);return false;
    }
    private static bool ContainsMenuPoint(ToolStripDropDown root,Point point)
    {
        if(!root.Visible)return false;
        if(root.Bounds.Contains(point))return true;
        return root.Items.OfType<ToolStripDropDownItem>().Any(item=>item.HasDropDownItems&&ContainsMenuPoint(item.DropDown,point));
    }
    private void ShowMenu()
    {
        offlineMenu.Close();
        if(Environment.TickCount64<suppressMenuUntil){suppressMenuUntil=0;return;}
        if(menu.Visible){menu.Close(ToolStripDropDownCloseReason.CloseCalled);return;}
        BeginPanelInteraction();menuEntryAnchor=entry?.Bounds??Rectangle.Empty;
        menuOwner.Location=Cursor.Position;menuOwner.Show();Native.SetForegroundWindow(menuOwner.Handle);
        menuOpenedAt=Environment.TickCount64;menu.Show(menuOwner,Point.Empty);
    }
    private void Restart()
    {
        if(stopping)return;
        AppDiagnostics.Warn("TN2003");
        if(!RestartThrottle.TryConsume(Settings.DirectoryPath,DateTimeOffset.UtcNow))
        {Stop();DiagnosticUi.Error("TN2005");return;}
        // Let the watchdog restore surviving icons and let the next launch discover them.
        Stop();
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--restart-after-exit",Environment.ProcessId.ToString()}});
    }
    private void Stop()
    {
        if(stopping)return;stopping=true;
        var last=AppDiagnostics.Snapshot();last["Stopped"]=true;AppDiagnostics.State=()=>last;
        timer.Stop();discovery.Stop();finishDrag.Stop();nativeGeometry.Dispose();nativeMaterial.Dispose();settingsWindow?.Close();
        try{drag?.Dispose();}catch(Exception error){AppDiagnostics.Error("TN9001",error);}
        bool bridgeStopped=true;
        try{bridge?.Dispose();}catch(Exception error){bridgeStopped=false;AppDiagnostics.Error("TN6001",error);}
        try{entry?.Dispose();}catch(Exception error){AppDiagnostics.Error("TN9001",error);}
        if(bridgeStopped&&(bridge==null||bridge.RestoredAll))
            try{recovery.Complete();}catch(Exception error){AppDiagnostics.Error("TN6001",error);}
        AppDiagnostics.Info("TN0002");panel.Hide();ExitThread();
    }
    internal void Shutdown()=>Stop();
    protected override void Dispose(bool disposing)
    {if(disposing){Stop();timer.Dispose();discovery.Dispose();finishDrag.Dispose();menu.Dispose();offlineMenu.Dispose();menuOwner.Dispose();panel.Dispose();library.Dispose();}base.Dispose(disposing);}
}
