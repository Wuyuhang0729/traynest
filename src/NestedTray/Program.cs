using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class Program
{
    [STAThread]private static int Main(string[] args)
    {
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
        if(args.Length==3&&args[0] is "--self-test" or "--self-test-hidden" or "--self-test-legacy" or "--self-test-ole" or "--self-test-ole-live")
        {
            Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(args[1]));
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            return args[0]=="--self-test-hidden"?HiddenStateRegression.Run(args[2]):args[0]=="--self-test-ole-live"?ExternalOleRegression.Run(args[2]):ProductSelfTest.Run(args[2],args[0]=="--self-test-ole",args[0]=="--self-test-legacy");
        }
        using var single=new Mutex(true,"Local\\NestedTray-"+Environment.UserName,out bool created);
        if(!created)return 0;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {using var app=new TrayApplication();Application.Run(app);return 0;}
        catch(Exception error){MessageBox.Show(error.Message,"嵌套面板",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}

internal sealed class TrayApplication:ApplicationContext
{
    private readonly Settings settings=Settings.Load();
    private readonly RecoveryJournal recovery=new();
    private readonly TrayPanel panel=new();
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
    private readonly ToolStripMenuItem startup=new("开机启动");
    private int ticks;
    private long suppressMenuUntil;
    private Rectangle menuEntryAnchor;
    internal ContextMenuStrip OwnMenu=>menu;
    internal TrayBridge Bridge=>bridge!;
    internal TrayPanel Panel=>panel;
    internal EntryIcon Entry=>entry!;
    internal bool HasDragCandidate=>candidate!=null;
    internal TrayApplication(bool testMode=false)
    {
        _=panel.Handle;
        try
        {
            recovery.Start();entry=new EntryIcon();bridge=new TrayBridge(settings,recovery);
            entry.DiagnosticsRequested=SaveDiagnostics;
            if(testMode)bridge.AllowedOwners=new();
            bridge.Changed+=()=>panel.Render(bridge.Icons.Values);
            bridge.Faulted+=text=>panel.BeginInvoke((Action)(()=>{if(!bridge.ExplorerAlive){Restart();return;}Stop();MessageBox.Show(text,"嵌套面板",MessageBoxButtons.OK,MessageBoxIcon.Information);}));
            bridge.Bounds=record=>panel.CellBounds(record);
            entry.Open=()=>{UpdateEntryBounds();if(panel.Visible&&!panel.IsDismissing)panel.Dismiss();else panel.Open();};
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
                bridge?.ForwardInput(record,code,rect);
            };
            panel.CanAcceptNativeDrag=()=>!cancelledDrag&&(candidate??releasedCandidate)is {CanCollect:true,Selected:false};
            panel.NativeDropReceived=point=>
            {
                releasedCandidate??=candidate;
                releasedPoint=point;
                finishDrag.Stop();finishDrag.Start();
            };
            finishDrag.Tick+=(_,_)=>CompleteNativeDrop();
            panel.RemoveIcon=record=>Guard(()=>bridge.Restore(record));panel.OpenMenu=ShowMenu;
            panel.DismissStarted=()=>{pairedWithOverflow=false;restoreOverflowUntil=0;contextIcon=null;};
            panel.VisibleChanged+=(_,_)=>{pairedWithOverflow=false;ignoreNativeCloseForPanelInteraction=false;panelOpenedUntil=Environment.TickCount64+700;if(!panel.Visible){restoreOverflowUntil=0;contextIcon=null;}};
            BuildMenu();bridge.Start();chevronBounds=NativeOverflow.ChevronBounds(bridge.ExplorerWindow);
            drag=new DragObserver(panel);
            drag.Down=PointerDown;drag.Up=PointerUp;drag.OtherDown=OtherPointerDown;
            timer.Tick+=(_,_)=>
            {
                if(bridge!=null&&!bridge.ExplorerAlive){Restart();return;}
                bridge?.Maintain();if(++ticks%7==0){UpdateEntryBounds();CacheNativeIconBounds();}if(ticks%33==0){bridge?.PruneClosed();chevronBounds=NativeOverflow.ChevronBounds(bridge!.ExplorerWindow);}
                if((candidate!=null||releasedCandidate!=null)&&(Native.GetAsyncKeyState((int)Keys.Escape)&0x8000)!=0){cancelledDrag=true;candidate=null;releasedCandidate=null;finishDrag.Stop();panel.DragPreview=false;panel.SetIncomingIcon(null);}
                bool escape=(Native.GetAsyncKeyState((int)Keys.Escape)&0x8000)!=0;
                if(escape&&!escapeWasDown&&menu.Visible)menu.Close(ToolStripDropDownCloseReason.Keyboard);
                else if(escape&&!escapeWasDown&&panel.Visible&&!cancelledDrag&&!IsContextSurface(Native.GetForegroundWindow()))panel.Dismiss();
                escapeWasDown=escape;
                if(candidate!=null&&Native.GetCursorPos(out var point))
                {
                    var cursor=new Point(point.X,point.Y);
                    if(Math.Abs(cursor.X-down.X)+Math.Abs(cursor.Y-down.Y)>=SystemInformation.DragSize.Width&&panel.EntryRectangle.Contains(cursor)&&!panel.Visible)panel.Open(true);
                    bool overTarget=panel.Visible&&panel.Bounds.Contains(cursor)&&Math.Abs(cursor.X-down.X)+Math.Abs(cursor.Y-down.Y)>=SystemInformation.DragSize.Width;
                    panel.SetIncomingIcon(overTarget&&candidate.CanCollect?candidate:null);
                }
                SyncOverflowVisibility();
                if(ticks%8==0&&panel.Visible&&!menu.Visible&&!HasContextPopup())panel.EnsureAboveApplications();
            };
            timer.Start();discovery.Tick+=(_,_)=>{discovery.Stop();bridge?.RefreshExisting();};if(!testMode)discovery.Start();
        }
        catch {Stop();throw;}
    }
    internal void PointerDown(Point point,nint sourceRoot,bool nativeVisibleOnPress)
    {
        if(HandleOwnMenuPress(point,false))return;
        finishDrag.Stop();releasedCandidate=null;cancelledDrag=false;panel.SetIncomingIcon(null);restoreOverflowUntil=0;
        initialDragEnters=panel.ExternalDragEnters;initialDrops=panel.ExternalDrops;
        initialCoreEnters=panel.CoreDrop.Enters;initialCoreDrops=panel.CoreDrop.Drops;
        candidate=null;down=point;
        if(panel.Visible&&nativeVisibleOnPress&&sourceRoot==bridge!.ExplorerWindow&&chevronBounds.Contains(point))
        {TraceDrag("native-chevron-close-start",new{BeforeNativeHidden=true});panel.Dismiss();return;}
        if(panel.IsDropSurface(point)){BeginPanelInteraction();return;}
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
        panel.DragPreview=false;panel.SetIncomingIcon(null);
        if(record?.Selected==true&&panel.Visible&&sourceWasOverflow)
        {pairedWithOverflow=true;restoreOverflowUntil=Environment.TickCount64+800;restoreOverflowRequested=false;}
        TraceDrag("complete",new {HasCandidate=record!=null,CanCollect=record?.CanCollect,Selected=record?.Selected,OntoPanel=ontoPanel,OleAccepted=oleAccepted,Cancelled=cancelledDrag});
    }
    private void SyncOverflowVisibility()
    {
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
    private void TraceDrag(string phase,object detail)
    {
        dragHistory.Enqueue(new {Time=DateTimeOffset.Now,Phase=phase,Detail=detail});
        while(dragHistory.Count>32)dragHistory.Dequeue();
    }
    private static string WindowClass(nint window)
    {var name=new System.Text.StringBuilder(256);Native.GetClassNameW(window,name,256);return name.ToString();}
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
    private void OtherPointerDown(Point point,nint sourceRoot)
    {
        if(HandleOwnMenuPress(point,true))return;
        if(!panel.Visible)return;
        if(panel.IsDropSurface(point)){BeginPanelInteraction();return;}
        if(IsSystemTrayWindow(sourceRoot)){ignoreNativeCloseForPanelInteraction=false;panelOpenedUntil=0;pairedWithOverflow=NativeOverflow.Visible;return;}
        if(!IsContextSurface(sourceRoot)&&!IsOwnPopup(sourceRoot))panel.Dismiss();
    }
    private void UpdateEntryBounds(){if(entry!=null){var rect=entry.Bounds;if(!rect.IsEmpty)panel.EntryRectangle=rect;}}
    private void Guard(Action action){try{action();}catch(Exception error){MessageBox.Show(error.Message,"嵌套面板",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    private void BuildMenu()
    {
        menu.AutoClose=true;
        menu.Closed+=(_,_)=>{menuOwner.Hide();if(entry!=null)NativeTray.PostMessage(entry.Handle,0,0,0);};
        menu.Items.Add("打开面板",null,(_,_)=>{menu.Close(ToolStripDropDownCloseReason.ItemClicked);UpdateEntryBounds();panel.Open();});
        menu.Items.Add("刷新图标",null,(_,_)=>{menu.Close(ToolStripDropDownCloseReason.ItemClicked);bridge?.RefreshExisting();});
        startup.Checked=Settings.StartupEnabled;
        startup.Click+=(_,_)=>{menu.Close(ToolStripDropDownCloseReason.ItemClicked);Guard(()=>{Settings.StartupEnabled=!Settings.StartupEnabled;startup.Checked=Settings.StartupEnabled;});};menu.Items.Add(startup);
        menu.Items.Add("移回所有图标",null,(_,_)=>{menu.Close(ToolStripDropDownCloseReason.ItemClicked);Guard(()=>{if(bridge!=null)foreach(var record in bridge.Icons.Values.Where(value=>value.Selected).ToArray())bridge.Restore(record);settings.Collected.Clear();settings.Save();});});
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,(_,_)=>{menu.Close(ToolStripDropDownCloseReason.ItemClicked);Stop();});
    }
    private bool HandleOwnMenuPress(Point point,bool right)
    {
        if(!menu.Visible)return false;
        if(menu.Bounds.Contains(point))return true;
        if(right&&menuEntryAnchor.Contains(point))suppressMenuUntil=Environment.TickCount64+500;
        menu.Close(ToolStripDropDownCloseReason.AppClicked);return false;
    }
    private void ShowMenu()
    {
        if(Environment.TickCount64<suppressMenuUntil){suppressMenuUntil=0;return;}
        if(menu.Visible){menu.Close(ToolStripDropDownCloseReason.CloseCalled);return;}
        BeginPanelInteraction();startup.Checked=Settings.StartupEnabled;menuEntryAnchor=entry?.Bounds??Rectangle.Empty;
        menuOwner.Location=Cursor.Position;menuOwner.Show();Native.SetForegroundWindow(menuOwner.Handle);
        menu.Show(menuOwner,Point.Empty);
    }
    private void Restart()
    {
        if(stopping)return;
        // Let the watchdog restore surviving icons and let the next launch discover them.
        Stop();
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--restart-after-exit",Environment.ProcessId.ToString()}});
    }
    private void Stop()
    {
        if(stopping)return;stopping=true;
        timer.Stop();discovery.Stop();finishDrag.Stop();drag?.Dispose();bridge?.Dispose();entry?.Dispose();
        if(bridge==null||bridge.RestoredAll)recovery.Complete();
        panel.Hide();ExitThread();
    }
    protected override void Dispose(bool disposing)
    {if(disposing){Stop();timer.Dispose();discovery.Dispose();finishDrag.Dispose();menu.Dispose();menuOwner.Dispose();panel.Dispose();}base.Dispose(disposing);}
}
