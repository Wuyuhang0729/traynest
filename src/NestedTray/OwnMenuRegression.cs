using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class OwnMenuRegression
{
    internal static int Run()
    {
        Directory.CreateDirectory(Settings.DirectoryPath);var result=new Dictionary<string,object?>();var errors=new List<string>();result["Errors"]=errors;
        using var app=new TrayApplication(true);Native.GetCursorPos(out var previous);bool startup=Settings.StartupEnabled;
        result["TemporaryAnimationMenuRemoved"]=!app.OwnMenu.Items.Cast<ToolStripItem>().Any(item=>item.Text?.Contains("动画调试")==true);
        using var outside=new Form{TopMost=true,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(40,40),ClientSize=new Size(180,80)};
        bool clicked=false;var button=new Button{Dock=DockStyle.Fill,Text="Own menu outside fixture"};button.Click+=(_,_)=>clicked=true;outside.Controls.Add(button);outside.Show();
        try
        {
            long earlierPress=Environment.TickCount64;Open();result["EntryCallbackOpensMenuWithoutPanel"]=app.OwnMenu.Visible&&!app.Panel.Visible;
            app.OtherPointerDown(new Point(50,50),outside.Handle,earlierPress);Pump(30);
            result["EarlierObservedPressDoesNotCloseNewMenu"]=app.OwnMenu.Visible;
            var key=new Native.Input{Type=1,Keyboard=new Native.KeyInput{Key=27}};Send(key);Pump(40);key.Keyboard.Flags=2;Send(key);Pump(150);
            result["EscapeClosesMenuWithoutPanel"]=!app.OwnMenu.Visible;
            Open();var point=button.PointToScreen(new Point(30,30));Native.SetCursorPos(point.X,point.Y);Mouse(2);Pump(40);Mouse(4);Pump(150);
            result["OutsideClickClosesMenuWithoutPanel"]=!app.OwnMenu.Visible;result["OutsideWindowClickCount"]=clicked?1:0;
            Open();NativeTray.PostMessage(app.Entry.Handle,0x8000+741,0,(nint)((401u<<16)|0x7B));Pump(120);result["RepeatedEntryCallbackTogglesClosed"]=!app.OwnMenu.Visible;
            result["SettingsContentsRemovedFromMenu"]=!app.OwnMenu.Items.OfType<ToolStripMenuItem>().Any(item=>item.HasDropDownItems||item.Text is "开机启动" or "每行图标数" or "弹出位置" or "面板外观" or "诊断与反馈…");
            Open();var settingsItem=app.OwnMenu.Items.Cast<ToolStripItem>().Single(item=>item.Text=="设置");
            var settingsPoint=app.OwnMenu.PointToScreen(new Point(settingsItem.Bounds.Left+settingsItem.Width/2,settingsItem.Bounds.Top+settingsItem.Height/2));
            Native.SetCursorPos(settingsPoint.X,settingsPoint.Y);Mouse(2);Pump(60);
            result["SettingsMenuRemainsUntilMouseUp"]=app.OwnMenu.Visible;
            Mouse(4);Pump(350);
            var window=Application.OpenForms.OfType<SettingsWindow>().SingleOrDefault();
            result["PhysicalSettingsClickOpensIndependentWindow"]=window is {Visible:true,ShowInTaskbar:true}&&!app.OwnMenu.Visible;
            result["MenuContainsOnlySettings"]=app.OwnMenu.Items.Count==1&&app.OwnMenu.Items[0].Text=="设置";
            result["ExitAvailableInSettings"]=window!=null&&WebSettingsTest.Exists(window,"ExitApplication");
            window?.Close();
            // Physical right click on our registered tray icon, then physical first left click.
            foreach(bool expanded in new[]{false,true})foreach(string state in new[]{"new","existing","minimized","reopened"})
            {
                if(state=="new"||state=="reopened")Application.OpenForms.OfType<SettingsWindow>().SingleOrDefault()?.Close();
                if(state=="minimized")Application.OpenForms.OfType<SettingsWindow>().Single().WindowState=FormWindowState.Minimized;
                NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(500);
                if(expanded){app.Panel.Open();Pump(650);}else app.Panel.Hide();
                var previousWindow=Application.OpenForms.OfType<SettingsWindow>().SingleOrDefault();
                var rect=app.Entry.Bounds;if(rect.IsEmpty)throw new InvalidOperationException("Tray entry has no physical bounds");
                Native.SetCursorPos(rect.Left+rect.Width/2,rect.Top+rect.Height/2);Mouse(8);Pump(60);Mouse(16);Pump(250);
                if(!app.OwnMenu.Visible)throw new InvalidOperationException("Physical tray right click did not open menu");
                if(expanded)result[$"MenuPreservesExpandedPanel_{state}"]=app.Panel.Visible;
                var item=app.OwnMenu.Items[0];var at=app.OwnMenu.PointToScreen(new Point(item.Bounds.Left+item.Width/2,item.Bounds.Top+item.Height/2));
                Native.SetCursorPos(at.X,at.Y);Mouse(2);Pump(60);Mouse(4);Pump(450);
                var current=Application.OpenForms.OfType<SettingsWindow>().SingleOrDefault();
                if(current!=null)WebSettingsTest.Ready(current);
                result[$"PhysicalFirstClick_{expanded}_{state}"]=current is {Visible:true,WindowState:FormWindowState.Normal}&&Native.GetForegroundWindow()==current.Handle&&(previousWindow==null||ReferenceEquals(previousWindow,current));
            }
            var web=Application.OpenForms.OfType<SettingsWindow>().Single();WebSettingsTest.Page(web,1);
            void ClickWeb(string selector)
            {
                var bounds=WebSettingsTest.Script<JsonElement>(web,"(()=>{const r=document.querySelector("+JsonSerializer.Serialize(selector)+").getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,w:innerWidth,h:innerHeight}})()");
                var client=new Point((int)(bounds.GetProperty("x").GetDouble()*web.Browser.ClientSize.Width/bounds.GetProperty("w").GetDouble()),(int)(bounds.GetProperty("y").GetDouble()*web.Browser.ClientSize.Height/bounds.GetProperty("h").GetDouble()));
                var screen=web.Browser.PointToScreen(client);Native.SetCursorPos(screen.X,screen.Y);Mouse(2);Pump(50);Mouse(4);Pump(250);
            }
            ClickWeb("[data-field=SettingsTheme] .picker-trigger");ClickWeb("[data-field=SettingsTheme] [data-value='2']");
            WebSettingsTest.Wait(()=>WebSettingsTest.Value(web,"SettingsTheme")==2);
            result["PhysicalCapsuleChoicePersistsDarkTheme"]=Settings.Load().SettingsTheme==SettingsTheme.Dark&&WebSettingsTest.Script<string>(web,"document.documentElement.dataset.theme")=="dark";
            ClickWeb("[data-field=OpeningMode] .picker-trigger");
            var esc=new Native.Input{Type=1,Keyboard=new Native.KeyInput{Key=27}};Send(esc);Pump(50);esc.Keyboard.Flags=2;Send(esc);Pump(180);
            result["PhysicalEscapeClosesCapsuleMenu"]=WebSettingsTest.Script<bool>(web,"document.querySelector('[data-field=OpeningMode] .picker-menu').hidden");
            ClickWeb("[data-field=SettingsTheme] .picker-trigger");ClickWeb("[data-field=SettingsTheme] [data-value='0']");
            WebSettingsTest.Wait(()=>WebSettingsTest.Value(web,"SettingsTheme")==0);
            result["PhysicalSystemThemeChoicePersists"]=Settings.Load().SettingsTheme==SettingsTheme.System;
            // Opening settings must not disable the user's chosen tray interactions.
            WebSettingsTest.Set(web,"ExpansionMode",1);NativeOverflow.TryClose(app.Bridge.ExplorerWindow);Pump(500);app.Panel.Hide();
            void ClickAt(Point point){Native.SetCursorPos(point.X,point.Y);Mouse(2);Pump(40);Mouse(4);}
            var chevron=NativeOverflow.ChevronBounds(app.Bridge.ExplorerWindow);if(chevron.IsEmpty)throw new InvalidOperationException("System chevron missing");
            var chevronPoint=new Point(chevron.Left+chevron.Width/2,chevron.Top+chevron.Height/2);
            ClickAt(chevronPoint);Pump(950);
            result["SettingsVisibleAllowsPhysicalChevronFollow"]=web.Visible&&web.WindowState==FormWindowState.Normal&&NativeOverflow.Visible&&app.Panel.Visible;
            ClickAt(chevronPoint);Pump(650);result["SettingsVisibleKeepsNormalChevronClose"]=!app.Panel.Visible&&!NativeOverflow.Visible;
            void Key(ushort code,bool up=false)=>Send(new Native.Input{Type=1,Keyboard=new Native.KeyInput{Key=code,Flags=up?2u:0u}});
            void Hotkey(){Key(18);Key(68);Key(68,true);Key(18,true);}
            ClickWeb("h1");Hotkey();Pump(900);
            result["SettingsVisibleAllowsGlobalShortcut"]=web.Visible&&app.Panel.Visible&&NativeOverflow.Visible;
            Hotkey();Pump(650);
            ClickWeb("#Shortcut");Pump(150);Hotkey();Pump(350);
            result["ShortcutRecordingStillProtectsPanel"]=web.RecordingShortcut&&!app.Panel.Visible;
            Key(27);Key(27,true);Pump(200);
            WebSettingsTest.Set(web,"ExpansionMode",2);NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(450);app.Panel.Hide();
            var hover=app.Entry.Bounds;Native.SetCursorPos(hover.Left+hover.Width/2,hover.Top+hover.Height/2);Pump(800);
            result["SettingsVisibleAllowsEntryHover"]=web.Visible&&app.Panel.Visible;
            WebSettingsTest.Set(web,"ExpansionMode",0);app.Panel.Hide();
            using var hints=new System.Windows.Forms.Timer{Interval=100};int shownHints=0;
            hints.Tick+=(_,_)=>{var dialog=Application.OpenForms.OfType<BlankCellHint>().FirstOrDefault();if(dialog!=null){shownHints++;dialog.DialogResult=DialogResult.OK;dialog.Close();}};
            hints.Start();WebSettingsTest.Set(web,"BlankCellOpens",true);Pump(200);int beforeHelp=shownHints;
            ClickWeb("#blankHint");Pump(300);hints.Stop();
            result["InlineHelpLinkOpensOriginalIllustration"]=shownHints==beforeHelp+1;
            result["HelpIsUnderlinedWithinOption"]=WebSettingsTest.Script<bool>(web,"!!document.querySelector('#RowBlankCellOpens #blankHint')&&!document.getElementById('RowblankHint')&&getComputedStyle(document.getElementById('blankHint')).textDecorationLine.includes('underline')");
            var ownIcons=new List<OpeningRegression.BlankFixture>();
            try
            {
                Point blank=Point.Empty;Rectangle slot=Rectangle.Empty;
                NativeOverflow.TryOpen(app.Bridge.ExplorerWindow);Pump(500);app.Panel.Hide();
                for(int attempt=0;attempt<6&&slot.IsEmpty;attempt++)
                {
                    ownIcons.Add(new OpeningRegression.BlankFixture());Pump(350);Native.GetWindowRect(NativeOverflow.Window,out var raw);var host=Native.Rect(raw);
                    var nodes=AutomationElement.FromHandle(NativeOverflow.Window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
                    var cells=new List<Rectangle>();foreach(AutomationElement node in nodes){var rect=node.Current.BoundingRectangle;if(!node.Current.IsOffscreen&&!rect.IsEmpty)cells.Add(new Rectangle((int)Math.Round(rect.X),(int)Math.Round(rect.Y),(int)Math.Round(rect.Width),(int)Math.Round(rect.Height)));}
                    for(int y=host.Top+15;y<host.Bottom&&slot.IsEmpty;y+=10)for(int x=host.Left+15;x<host.Right&&slot.IsEmpty;x+=10){var candidate=OverflowBlankCell.Find(new Point(x,y),host,cells);if(!candidate.IsEmpty){slot=candidate;blank=new Point(candidate.Left+candidate.Width/2,candidate.Top+candidate.Height/2);}}
                }
                if(slot.IsEmpty)throw new InvalidOperationException("No verified blank cell available");
                ClickAt(blank);Pump(70);ClickAt(blank);Pump(850);
                result["SettingsVisibleAllowsBlankDoubleClickOpen"]=web.Visible&&app.Panel.Visible;
                ClickAt(blank);Pump(70);ClickAt(blank);Pump(750);
                result["SettingsVisibleAllowsBlankDoubleClickClose"]=web.Visible&&!app.Panel.Visible;
            }
            finally{foreach(var fixture in ownIcons)fixture.Dispose();}
            Application.OpenForms.OfType<SettingsWindow>().Single().Close();
            for(int i=0;i<5;i++){Open();app.OwnMenu.Close();Pump(30);}result["RepeatedOpenCloseDoesNotStick"]=!app.OwnMenu.Visible;
            void Open(){var work=Screen.PrimaryScreen!.WorkingArea;Native.SetCursorPos(work.Right-60,work.Bottom-100);NativeTray.PostMessage(app.Entry.Handle,0x8000+741,0,(nint)((401u<<16)|0x7B));Pump(150);if(!app.OwnMenu.Visible)throw new InvalidOperationException("Entry menu failed to open.");}
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{Native.SetCursorPos(previous.X,previous.Y);app.Dispose();result["StartupUnchanged"]=Settings.StartupEnabled==startup;File.WriteAllText(Path.Combine(Settings.DirectoryPath,"own-menu-test.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));}
        return errors.Count==0&&!result.Values.Any(value=>value is false)?0:1;
    }
    private static void Mouse(uint flags)=>Send(new Native.Input{Mouse=new Native.MouseInput{Flags=flags}});
    private static void Send(Native.Input input){if(Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>())!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
}
