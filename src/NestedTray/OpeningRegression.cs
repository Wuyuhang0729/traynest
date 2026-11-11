using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
namespace NestedTray;
internal static class OpeningRegression
{
    internal static int Foreground(string root)
    {
        using var form=new Form{Text="TrayNest TN013 foreground fixture",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(80,80,360,180)};
        using var timer=new System.Windows.Forms.Timer{Interval=120000};timer.Tick+=(_,_)=>form.Close();timer.Start();Application.Run(form);return 0;
    }
    internal static int Run(string root,bool desktop)
    {
        root=Path.GetFullPath(root);Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(root,"profile"));Directory.CreateDirectory(Settings.DirectoryPath);
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try{if(desktop)Desktop(root,checks);else Isolated(root,checks);}catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count==0&&checks.Count>0&&checks.Values.All(x=>x);
        File.WriteAllText(Path.Combine(root,"opening-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Isolated(string root,Dictionary<string,bool> c)
    {
        File.WriteAllText(Settings.FilePath,"{\"Collected\":[\"one\",\"two\"],\"Tools\":[],\"FollowNativeOverflow\":true,\"Placement\":1,\"Columns\":7}");
        var s=Settings.Load();c["LegacyBehaviorPreserved"]=s.OpeningMode==OpeningMode.Linked&&s.FollowNativeOverflow&&s.Shortcut==Settings.DefaultShortcut&&!s.BlankCellOpens;
        File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Shortcut\":0}");c["PreviouslyUnconfiguredShortcutGetsAltD"]=Settings.Load().Shortcut==(Keys.Alt|Keys.D);
        File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Shortcut\":0,\"ShortcutConfigured\":true}");c["ExplicitClearSurvivesRestart"]=Settings.Load().Shortcut==Keys.None;
        File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Shortcut\":458875}");c["ExistingCustomShortcutPreserved"]=Settings.Load().Shortcut==(Keys.Control|Keys.Alt|Keys.Shift|Keys.F12);
        s.Save();s.ChangeOpeningMode(OpeningMode.Independent);s.ChangeOpeningMode(OpeningMode.Linked);s.ChangeOpeningMode(OpeningMode.Independent);
        var read=Settings.Load();c["ModePersistsAndLayoutSurvives"]=read.OpeningMode==OpeningMode.Independent&&read.Columns==7&&read.Placement==PopupPlacement.Above&&read.Collected.SequenceEqual(["one","two"]);
        int requests=0;var placement=new Settings{OpeningMode=OpeningMode.Independent};EntryPlacementPreference.Initialize(placement,()=>null,value=>requests++);c["InitialPlacementSaved"]=requests==1&&placement.EntryPlacementInitialized;s.Save();
        s.BeforeSave=()=>throw new IOException("controlled mode save failure");try{s.ChangeOpeningMode(OpeningMode.Linked);}catch(TrayNestException){}
        c["ModeFailureRollsBack"]=s.OpeningMode==OpeningMode.Independent&&Settings.Load().OpeningMode==OpeningMode.Independent;s.BeforeSave=null;
        using var hotkey=new GlobalShortcut();var old=Keys.Control|Keys.Alt|Keys.Shift|Keys.F12;var next=Keys.Control|Keys.Alt|Keys.Shift|Keys.F11;
        hotkey.Change(s,old,false);c["ShortcutPersists"]=Settings.Load().Shortcut==old;
        using var blocker=new GlobalShortcut();var bs=new Settings();blocker.Change(bs,next,false);s.Save();
        bool conflict=false;try{hotkey.Change(s,next,true);}catch(ShortcutException){conflict=true;}
        c["OccupiedShortcutKeepsOldSettings"]=conflict&&s.Shortcut==old&&!s.ShortcutBothPanels&&Settings.Load().Shortcut==old;
        blocker.Change(bs,Keys.None,false);s.Save();s.BeforeSave=()=>throw new IOException("controlled shortcut save failure");
        try{hotkey.Change(s,next,true);}catch(TrayNestException){}
        c["ShortcutSaveFailureRollsBack"]=s.Shortcut==old&&!s.ShortcutBothPanels;
        c["FailedReplacementReleasesReservation"]=GlobalShortcut.RegisterHotKey(blocker.Handle,901,7,(uint)Keys.F11);GlobalShortcut.UnregisterHotKey(blocker.Handle,901);
        c["OldRegistrationSurvivesFailure"]=!GlobalShortcut.RegisterHotKey(blocker.Handle,902,7,(uint)Keys.F12);GlobalShortcut.UnregisterHotKey(blocker.Handle,902);
        s.BeforeSave=null;hotkey.Change(s,next,true);c["ReplacementAndTargetPersist"]=Settings.Load().Shortcut==next&&Settings.Load().ShortcutBothPanels;
        c["OldRegistrationReleased"]=GlobalShortcut.RegisterHotKey(blocker.Handle,903,7,(uint)Keys.F12);GlobalShortcut.UnregisterHotKey(blocker.Handle,903);
        hotkey.Change(s,Keys.None,false);c["ClearPersists"]=Settings.Load().Shortcut==Keys.None;
        c["ClearReleasesRegistration"]=GlobalShortcut.RegisterHotKey(blocker.Handle,904,7,(uint)Keys.F11);GlobalShortcut.UnregisterHotKey(blocker.Handle,904);
        c["UnsafeKeysRejected"]=!GlobalShortcut.Valid(Keys.A)&&!GlobalShortcut.Valid(Keys.ControlKey|Keys.Control)&&!GlobalShortcut.Valid(Keys.LWin|Keys.Control);
        foreach(int cell in new[]{40,50,60,80})foreach(int columns in new[]{5,6,7,8})
        {
            var cells=Enumerable.Range(0,columns+1).Select(i=>new Rectangle(100+i%columns*cell,100+i/columns*cell,cell,cell)).ToList();var host=new Rectangle(90,90,columns*cell+20,cell*2+20);
            var blank=new Point(100+cell+cell/2,100+cell+cell/2);
            c[$"BlankCell_{cell}_{columns}"]=!OverflowBlankCell.Find(blank,host,cells).IsEmpty&&OverflowBlankCell.Find(new Point(100+cell/2,100+cell/2),host,cells).IsEmpty&&OverflowBlankCell.Find(new Point(95,95),host,cells).IsEmpty;
            cells.Add(new Rectangle(100+cell,100+cell,cell,cell));c[$"NewIconInvalidatesBlank_{cell}_{columns}"]=OverflowBlankCell.Find(blank,host,cells).IsEmpty;
        }
        c["UnknownAndFullGridHaveNoBlank"]=OverflowBlankCell.Find(new Point(180,180),new Rectangle(0,0,300,300),[new Rectangle(100,100,40,40)]).IsEmpty;
        var work=new Rectangle(0,0,1000,800);c["IndependentCenteredAbove"]=PopupPosition.CenterAbove(new Rectangle(450,800,20,20),new Size(200,100),work)==new Point(360,692);
        c["IndependentClampsAtMonitorEdges"]=PopupPosition.CenterAbove(new Rectangle(980,800,20,20),new Size(200,100),work)==new Point(800,692);
        using var icon=EntryIcon.CreateIndependentIcon();using var bitmap=icon.ToBitmap();bitmap.Save(Path.Combine(root,"independent-caret.png"));
        c["BrandBlueCaretHasTransparentBackground"]=bitmap.GetPixel(0,0).A==0&&Enumerable.Range(0,32).SelectMany(y=>Enumerable.Range(0,32).Select(x=>bitmap.GetPixel(x,y))).Any(p=>p.A>200&&p.B>220&&p.R<50);
        using var down=EntryIcon.CreateIndependentIcon(true);using var downImage=down.ToBitmap();downImage.Save(Path.Combine(root,"expanded-caret.png"));
        using var hint=new BlankCellHint();hint.Show();Pump(200);using(var screen=new Bitmap(hint.Width,hint.Height)){using(var g=Graphics.FromImage(screen))g.CopyFromScreen(hint.Location,Point.Empty,hint.Size);screen.Save(Path.Combine(root,"blank-cell-hint-screen.png"));}c["GraphicalHintHasConfirmButton"]=hint.Controls.Find("HintConfirm",true).Length==1;hint.Close();
    }
    private static void Desktop(string root,Dictionary<string,bool> c)
    {
        new Settings{OpeningMode=OpeningMode.Independent,FollowNativeOverflow=true,Shortcut=Settings.DefaultShortcut}.Save();
        Native.GetCursorPos(out var previous);using var app=new TrayApplication(true);Process? foreground=null;
        try
        {
            NativeOverflow.TryClose(app.Bridge.ExplorerWindow);Pump(450);app.Entry.Open?.Invoke();Pump(600);
            c["IndependentEntryOpensOnlyCollection"]=app.Panel.Visible&&!NativeOverflow.Visible;
            c["OpeningTurnsArrowDown"]=app.Entry.Expanded;
            c["IndependentPanelCenteredAndClamped"]=app.Panel.Location==PopupPosition.CenterAbove(app.Entry.Bounds,app.Panel.Size,Screen.FromRectangle(app.Entry.Bounds).WorkingArea);
            app.Entry.Open?.Invoke();Pump(600);c["EntryTogglesClosed"]=!app.Panel.Visible;
            c["ClosingTurnsArrowUp"]=!app.Entry.Expanded;
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Wait(()=>NativeOverflow.Visible,1800);Pump(400);c["ChevronDoesNotOpenIndependentPanel"]=NativeOverflow.Visible&&!app.Panel.Visible;
            app.Entry.Open?.Invoke();Pump(600);NativeOverflow.TryClose(app.Bridge.ExplorerWindow);Pump(650);c["NativeCloseDoesNotCloseIndependentPanel"]=app.Panel.Visible;
            app.Panel.Hide();
            foreground=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--opening-foreground",root}});
            nint other=0;Wait(()=>{other=Native.FindWindowW(null!,"TrayNest TN013 foreground fixture");return other!=0;},3000);Native.SetForegroundWindow(other);Pump(200);
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out uint owner);c["OtherProcessIsForeground"]=foreground!=null&&owner==foreground.Id;
            Hotkey();Pump(700);c["RealGlobalShortcutFromOtherProcess"]=app.Panel.Visible&&!NativeOverflow.Visible;
            c["ShortcutAlsoTurnsArrowDown"]=app.Entry.Expanded;
            Hotkey();Pump(600);c["RepeatedShortcutCloses"]=!app.Panel.Visible;
            app.Entry.Open?.Invoke();Pump(600);Key(Keys.Escape,true);Pump(100);Key(Keys.Escape,false);Pump(600);c["EscapeClosesIndependent"]=!app.Panel.Visible;
            c["EscapeRestoresArrowUp"]=!app.Entry.Expanded;
            app.Entry.Open?.Invoke();Pump(600);Click(new Point(160,140));Pump(600);c["RealOutsideClickCloses"]=!app.Panel.Visible;
            app.OwnMenu.Items.OfType<ToolStripMenuItem>().Single().PerformClick();Pump(200);var settings=Application.OpenForms.OfType<SettingsWindow>().Single();
            settings.SelectPage(1);WebSettingsTest.Page(settings,1);WebSettingsTest.Set(settings,"OpeningMode",0);Pump(250);c["ModeSwitchClosesAndPersists"]=!app.Panel.Visible&&Settings.Load().OpeningMode==OpeningMode.Linked;
            WebSettingsTest.Set(settings,"ShortcutTarget",false);settings.WindowState=FormWindowState.Minimized;Pump(150);
            NativeOverflow.TryClose(app.Bridge.ExplorerWindow);Pump(400);Hotkey();Pump(1000);c["LinkedShortcutOpensBoth"]=app.Panel.Visible&&NativeOverflow.Visible;
            Hotkey();Pump(700);c["LinkedShortcutClosesBoth"]=!app.Panel.Visible&&!NativeOverflow.Visible;
            settings.WindowState=FormWindowState.Normal;settings.SelectPage(0);WebSettingsTest.Page(settings,1);WebSettingsTest.Set(settings,"ExpansionMode",0);
            var hints=new HashSet<nint>();using var hintTimer=new System.Windows.Forms.Timer{Interval=150};
            hintTimer.Tick+=(_,_)=>{var dialog=Application.OpenForms.OfType<BlankCellHint>().FirstOrDefault();if(dialog!=null){hints.Add(dialog.Handle);dialog.DialogResult=DialogResult.OK;dialog.Close();}};
            hintTimer.Start();WebSettingsTest.Set(settings,"BlankCellOpens",true);
            WebSettingsTest.Set(settings,"BlankCellOpens",false);WebSettingsTest.Set(settings,"BlankCellOpens",true);Pump(250);hintTimer.Stop();c["FirstUseHintShownOnceAndPersists"]=hints.Count==1&&Settings.Load().BlankCellHintSeen;
            File.WriteAllText(Path.Combine(root,"hint-observation.json"),JsonSerializer.Serialize(new{Dialogs=hints.Count,Persisted=Settings.Load().BlankCellHintSeen}));
            settings.WindowState=FormWindowState.Minimized;
            NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(750);app.Panel.Hide();
            var ownIcons=new List<BlankFixture>{new BlankFixture()};try
            {
                Point blank=Point.Empty;Rectangle slot=Rectangle.Empty;
                for(int attempt=0;attempt<6&&slot.IsEmpty;attempt++)
                {
                    Pump(350);Native.GetWindowRect(NativeOverflow.Window,out var raw);var host=Native.Rect(raw);
                    var nodes=AutomationElement.FromHandle(NativeOverflow.Window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
                    var cells=new List<Rectangle>();foreach(AutomationElement node in nodes){var r=node.Current.BoundingRectangle;if(!node.Current.IsOffscreen&&!r.IsEmpty)cells.Add(new Rectangle((int)Math.Round(r.X),(int)Math.Round(r.Y),(int)Math.Round(r.Width),(int)Math.Round(r.Height)));}
                    File.WriteAllText(Path.Combine(root,$"blank-geometry-{attempt}.private.json"),JsonSerializer.Serialize(new{Host=host.ToString(),Cells=cells.Select(r=>r.ToString())}));
                    for(int y=host.Top+15;y<host.Bottom&&slot.IsEmpty;y+=10)for(int x=host.Left+15;x<host.Right&&slot.IsEmpty;x+=10)
                    {var result=OverflowBlankCell.Find(new Point(x,y),host,cells);if(!result.IsEmpty){slot=result;blank=new Point(result.Left+result.Width/2,result.Top+result.Height/2);}}
                    if(slot.IsEmpty)ownIcons.Add(new BlankFixture());
                }
                c["RealOverflowProvidesBlankCell"]=!slot.IsEmpty;
                if(!slot.IsEmpty)
                {
                    Click(blank);Pump(70);Click(blank);Pump(700);c["RealBlankDoubleClickOpensCollection"]=app.Panel.Visible;Click(blank);Pump(70);Click(blank);Pump(650);c["RealBlankDoubleClickClosesCollection"]=!app.Panel.Visible;
                    app.Panel.Hide();Pump(300);Native.SetCursorPos(blank.X,blank.Y);Mouse(2);Pump(100);MoveMouse(20,0);Pump(100);MoveMouse(-20,0);Pump(100);Mouse(4);Pump(600);c["DragAwayAndBackDoesNotTrigger"]=!app.Panel.Visible;
                    app.Panel.Hide();Pump(300);Mouse(8);Pump(100);Mouse(16);Pump(350);Key(Keys.Escape,true);Key(Keys.Escape,false);Pump(250);c["RightClickDoesNotTrigger"]=!app.Panel.Visible;
                    Native.GetWindowRect(NativeOverflow.Window,out var border);Click(new Point(border.Left+2,border.Top+2));Pump(500);c["BorderDoesNotTrigger"]=!app.Panel.Visible;
                }
                app.Panel.Hide();NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Wait(()=>NativeOverflow.Visible,1800);Pump(650);
                var own=ownIcons[0];var iconRect=Native.GetIconRect(own.Handle,777,own.Guid);
                if(!iconRect.IsEmpty){Click(new Point(iconRect.Left+iconRect.Width/2,iconRect.Top+iconRect.Height/2));Pump(500);}
                File.WriteAllText(Path.Combine(root,"own-icon-observation.json"),JsonSerializer.Serialize(new{Clicks=own.Clicks,Bounds=iconRect.ToString(),Native=NativeOverflow.Visible,Panel=app.Panel.Visible}));
                c["OwnIconClickPassesThroughWithoutBlankTrigger"]=own.Clicks>0&&!app.Panel.Visible;
                settings.WindowState=FormWindowState.Normal;settings.SelectPage(0);settings.SelectPage(1);WebSettingsTest.Page(settings,1);WebSettingsTest.Set(settings,"OpeningMode",1);Pump(250);settings.WindowState=FormWindowState.Minimized;
                NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(650);if(!slot.IsEmpty)Click(blank);Pump(500);c["BlankClickDisabledInIndependentMode"]=!app.Panel.Visible;
                using var screenshot=new Bitmap(Screen.PrimaryScreen!.Bounds.Width,Screen.PrimaryScreen.Bounds.Height);using(var graphics=Graphics.FromImage(screenshot))graphics.CopyFromScreen(Point.Empty,Point.Empty,screenshot.Size);screenshot.Save(Path.Combine(root,"desktop.private.png"));
            }
            finally{foreach(var item in ownIcons)item.Dispose();}
            settings.Close();NativeOverflow.TryClose(app.Bridge.ExplorerWindow);
        }
        finally
        {
            if(foreground!=null&&!foreground.HasExited){NativeTray.PostMessage(Native.FindWindowW(null!,"TrayNest TN013 foreground fixture"),0x10,0,0);foreground.WaitForExit(3000);}foreground?.Dispose();Native.SetCursorPos(previous.X,previous.Y);
        }
    }
    internal sealed class BlankFixture:NativeWindow,IDisposable
    {
        private readonly Guid guid=Guid.NewGuid();
        internal Guid Guid=>guid;internal int Clicks;
        internal BlankFixture(){CreateHandle(new CreateParams{Parent=new nint(-3)});var data=NativeTray.New(Handle,777,guid);data.Flags=1|2|4|0x20;data.CallbackMessage=0x8000+9;data.Icon=SystemIcons.Information.Handle;data.Tip="TrayNest TN013 test";if(!NativeTray.Notify(0,ref data))throw new InvalidOperationException("Own fixture registration failed");}
        protected override void WndProc(ref Message m){if(m.Msg==0x8000+9&&((uint)m.LParam.ToInt64()&0xffff) is 0x202 or 0x400)Clicks++;base.WndProc(ref m);}
        public void Dispose(){var data=NativeTray.New(Handle,777,guid);data.Flags=0x20;NativeTray.Notify(2,ref data);DestroyHandle();}
    }
    private static void Hotkey(){foreach(var key in new[]{Keys.Menu,Keys.D})Key(key,true);Pump(90);foreach(var key in new[]{Keys.D,Keys.Menu})Key(key,false);}
    private static void Key(Keys key,bool down)=>Native.SendInput(1,[new Native.Input{Type=1,Keyboard=new Native.KeyInput{Key=(ushort)key,Flags=down?0u:2u}}],Marshal.SizeOf<Native.Input>());
    private static void Mouse(uint flags)=>Native.SendInput(1,[new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=flags}}],Marshal.SizeOf<Native.Input>());
    private static void MoveMouse(int x,int y)=>Native.SendInput(1,[new Native.Input{Type=0,Mouse=new Native.MouseInput{X=x,Y=y,Flags=1}}],Marshal.SizeOf<Native.Input>());
    private static void Click(Point point){Native.SetCursorPos(point.X,point.Y);Pump(80);Mouse(2);Pump(80);Mouse(4);}
    private static void Wait(Func<bool> ready,int ms){var clock=Stopwatch.StartNew();while(!ready()&&clock.ElapsedMilliseconds<ms)Pump(20);}
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
}
