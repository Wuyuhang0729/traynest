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
            result["ExitAvailableInSettings"]=window?.Controls.Find("ExitApplication",true).Length==1;
            window?.Close();
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
