using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

namespace NestedTray;
internal static class SettingsRegression
{
    internal static int Run(string root)
    {
        Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(Path.GetFullPath(root),"profile"));
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try
        {
            checks["LimitedProcessQueryReadsOwnExecutable"]=string.Equals(ProcessExecutable.Read(Environment.ProcessId),Environment.ProcessPath,StringComparison.OrdinalIgnoreCase);
            checks["MissingProcessReturnsEmptyPath"]=ProcessExecutable.Read(int.MaxValue)=="";
            Directory.CreateDirectory(Settings.DirectoryPath);File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[]}");
            var settings=Settings.Load();checks["ExistingProfileDefaultsOff"]=!settings.FollowNativeOverflow&&!settings.HoverToOpen&&!settings.MatchPanelMaterials;
            checks["ExistingProfilesKeepSimultaneousEntrance"]=settings.ExpansionOrder==NativeExpansionOrder.Simultaneous;
            settings.ChangeExpansionOrder(NativeExpansionOrder.AfterNative);checks["ExpansionOrderPersists"]=Settings.Load().ExpansionOrder==NativeExpansionOrder.AfterNative;
            settings.BeforeSave=()=>throw new IOException("Controlled order save failure");bool orderFailed=false;try{settings.ChangeExpansionOrder(NativeExpansionOrder.Simultaneous);}catch(TrayNestException){orderFailed=true;}
            checks["ExpansionOrderSaveFailureRollsBack"]=orderFailed&&settings.ExpansionOrder==NativeExpansionOrder.AfterNative;settings.BeforeSave=null;
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"FollowNativeOverflow\":true,\"HoverToOpen\":true}");
            var legacy=Settings.Load();checks["LegacyConflictingModesNormalizeToFollow"]=legacy.FollowNativeOverflow&&!legacy.HoverToOpen;
            foreach(bool follow in new[]{false,true})foreach(bool hover in new[]{false,true})foreach(bool match in new[]{false,true})
            {if(follow&&hover)continue;settings.ChangeBehavior(follow,hover,match);var read=Settings.Load();checks[$"PersistsExclusiveMode_{follow}_{hover}_{match}"]=read.FollowNativeOverflow==follow&&read.HoverToOpen==hover&&read.MatchPanelMaterials==match;}
            bool rejected=false;try{settings.ChangeBehavior(true,true,true);}catch(ArgumentException){rejected=true;}
            checks["SimultaneousModesRejected"]=rejected&&settings.FollowNativeOverflow&&!settings.HoverToOpen;
            settings.BeforeSave=()=>throw new IOException("Controlled settings save failure");bool failed=false;try{settings.ChangeBehavior(false,false,false);}catch(TrayNestException){failed=true;}
            checks["BehaviorSaveFailureRollsBack"]=failed&&settings.FollowNativeOverflow&&!settings.HoverToOpen&&settings.MatchPanelMaterials;settings.BeforeSave=null;
            var controller=new AutoExpand();
            checks["DisabledDoesNotOpen"]=controller.Evaluate(0,false,false,true,false,true,false)==AutoExpandReason.None;
            checks["FollowOpensWhenEnabled"]=controller.Evaluate(10,true,false,true,false,false,false)==AutoExpandReason.NativeOverflow;
            checks["ManualCloseDoesNotReopenInSameCycle"]=controller.Evaluate(20,true,false,true,false,false,false)==AutoExpandReason.None;
            controller.Evaluate(30,true,false,false,false,false,false);
            checks["ReopeningNativeStartsNewCycle"]=controller.Evaluate(40,true,false,true,false,false,false)==AutoExpandReason.NativeOverflow;
            controller=new();checks["PassingEntryDoesNotOpen"]=controller.Evaluate(0,false,true,false,false,true,false)==AutoExpandReason.None;
            controller.Evaluate(200,false,true,false,false,false,false);controller.Evaluate(250,false,true,false,false,true,false);
            checks["HoverDelayResetsAfterLeaving"]=controller.Evaluate(500,false,true,false,false,true,false)==AutoExpandReason.None;
            checks["HoverOpensAfterDelay"]=controller.Evaluate(710,false,true,false,false,true,false)==AutoExpandReason.Hover;
            checks["HoverDoesNotLoop"]=controller.Evaluate(1200,false,true,false,false,true,false)==AutoExpandReason.None;
            controller=new();checks["MenuOrDragBlocksFollow"]=controller.Evaluate(0,true,true,true,false,true,true)==AutoExpandReason.None;
            checks["FollowResumesAfterInteraction"]=controller.Evaluate(600,true,true,true,false,true,false)==AutoExpandReason.NativeOverflow;
            controller=new();controller.Evaluate(0,false,true,false,false,true,false);controller.SuppressCurrentCycle(false);
            checks["ManualCloseSuppressesHover"]=controller.Evaluate(1000,false,true,false,false,true,false)==AutoExpandReason.None;
            bool startup=false;using var window=new SettingsWindow(settings,()=>{},()=>startup,value=>startup=value);
            window.Show();Pump(150);
            checks["IndependentTaskbarWindow"]=window.ShowInTaskbar&&!window.TopMost&&window.FormBorderStyle==FormBorderStyle.Sizable;
            var expansion=window.Controls.Find("ExpansionMode",true).OfType<ComboBox>().Single();
            checks["ExpansionOrderVisibleForFollowOnly"]=window.Controls.Find("ExpansionOrderRow",true).Single().Visible;
            expansion.SelectedIndex=2;checks["WindowControlPersistsExclusiveHover"]=!Settings.Load().FollowNativeOverflow&&Settings.Load().HoverToOpen;
            checks["ExpansionOrderHiddenForHover"]=!window.Controls.Find("ExpansionOrderRow",true).Single().Visible;
            settings.BeforeSave=()=>throw new IOException("Controlled UI save failure");expansion.SelectedIndex=1;
            checks["WindowControlFailureRestoresMode"]=expansion.SelectedIndex==2&&!Settings.Load().FollowNativeOverflow;settings.BeforeSave=null;
            foreach(int page in new[]{0,1,2}){window.SelectPage(page);Pump(100);using var bitmap=new Bitmap(window.Width,window.Height);window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,$"settings-{page}.png"));}
            window.SelectPage(1);var appearance=window.Controls.Find("Appearance",true).OfType<ComboBox>().Single();appearance.SelectedIndex=3;
            checks["WindowAppearancePersists"]=Settings.Load().Appearance==PanelAppearance.Dark;
            var columns=window.Controls.Find("Columns",true).OfType<ComboBox>().Single();columns.SelectedIndex=2;
            checks["WindowColumnsPersist"]=Settings.Load().Columns==7;window.Close();
        }
        catch(Exception error){errors.Add(error.ToString());}
        return Report(root,"settings",checks,errors);
    }
    private static int Report(string root,string name,Dictionary<string,bool> checks,List<string> errors)
    {bool passed=errors.Count==0&&checks.Count>0&&checks.Values.All(value=>value);File.WriteAllText(Path.Combine(root,name+"-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;}
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
}
