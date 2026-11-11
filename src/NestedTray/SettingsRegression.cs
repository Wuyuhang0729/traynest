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
            using var first=new IconRecord();using var second=new IconRecord();using var selected=new IconRecord{Selected=true};using var added=new IconRecord();
            var changing=new Dictionary<string,IconRecord>{{"first",first},{"second",second},{"selected",selected}};int queries=0;var expectedBounds=new Rectangle(10,20,30,40);
            TrayApplication.CacheNativeBoundsSnapshot(changing.Values,record=>{queries++;if(queries==1){changing.Remove("second");changing.Add("added",added);}return expectedBounds;});
            checks["BoundsCacheSurvivesReentrantIconRegistration"]=queries==2&&first.NativeBounds==expectedBounds&&second.NativeBounds==expectedBounds;
            checks["BoundsCacheSkipsSelectedAndDefersNewIcons"]=selected.NativeBounds.IsEmpty&&added.NativeBounds.IsEmpty;
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
            checks["OldProfileDefaultsToSystemWindowTheme"]=Settings.Load().SettingsTheme==SettingsTheme.System;
            checks["OldProfileDefaultsToIndependentUpdateSwitches"]=Settings.Load().AutoCheckUpdates&&Settings.Load().ImportantUpdateReminders;
            foreach(bool automatic in new[]{false,true})foreach(bool important in new[]{false,true})
            {settings.ChangeUpdatePreference(automatic,important);var read=Settings.Load();checks[$"UpdateSwitchesPersist_{automatic}_{important}"]=read.AutoCheckUpdates==automatic&&read.ImportantUpdateReminders==important;}
            settings.BeforeSave=()=>throw new IOException("controlled update preference failure");bool updateSaveFailed=false;
            try{settings.ChangeUpdatePreference(false,false);}catch(TrayNestException){updateSaveFailed=true;}
            checks["UpdatePreferenceFailureRollsBack"]=updateSaveFailed&&settings.AutoCheckUpdates&&settings.ImportantUpdateReminders;settings.BeforeSave=null;
            bool startup=false;using var window=new SettingsWindow(settings,()=>{},()=>startup,value=>startup=value);
            window.Show();WebSettingsTest.Ready(window);
            checks["IndependentTaskbarWindow"]=window.ShowInTaskbar&&!window.TopMost;
            WebSettingsTest.Page(window,0);
            checks["UpdateControlsInGeneral"]=WebSettingsTest.Exists(window,"AutoCheckUpdates")&&WebSettingsTest.Exists(window,"ImportantUpdateReminders")&&WebSettingsTest.Exists(window,"checkUpdate");
            WebSettingsTest.Set(window,"AutoCheckUpdates",false);
            checks["ManualCheckAvailableWithAutoOff"]=WebSettingsTest.Script<bool>(window,"!document.getElementById('checkUpdate').disabled")&&!Settings.Load().AutoCheckUpdates;
            checks["AutoDoesNotDisableReminderPreference"]=WebSettingsTest.Checked(window,"ImportantUpdateReminders");
            WebSettingsTest.Set(window,"ImportantUpdateReminders",false);
            checks["ReminderCanBeDisabledSeparately"]=!Settings.Load().ImportantUpdateReminders;
            settings.BeforeSave=()=>throw new IOException("controlled update UI save failure");WebSettingsTest.Set(window,"AutoCheckUpdates",true);
            checks["UpdateUIFailureRestoresToggle"]=!WebSettingsTest.Checked(window,"AutoCheckUpdates")&&!settings.AutoCheckUpdates;settings.BeforeSave=null;
            WebSettingsTest.Script<bool>(window,"updateView({Status:'test',Version:'test',Summary:'<img src=x onerror=alert(1)>',CanDownload:true});true");
            checks["ReleaseTextNeverBecomesMarkup"]=WebSettingsTest.Script<bool>(window,"!document.querySelector('#update-info img')&&document.querySelector('#update-info').textContent.includes('<img')");
            WebSettingsTest.Capture(window,Path.Combine(root,"settings-updates.png"));
            WebSettingsTest.Page(window,1);
            checks["ExpansionOrderVisibleForFollowOnly"]=WebSettingsTest.Visible(window,"RowNativeExpansionOrder");
            WebSettingsTest.Set(window,"ExpansionMode",2);checks["WindowControlPersistsExclusiveHover"]=Settings.Load().HoverToOpen&&!Settings.Load().FollowNativeOverflow;
            checks["ExpansionOrderHiddenForHover"]=!WebSettingsTest.Exists(window,"NativeExpansionOrder");
            settings.BeforeSave=()=>throw new IOException("controlled UI failure");WebSettingsTest.Set(window,"ExpansionMode",1);
            checks["WindowControlFailureRestoresMode"]=WebSettingsTest.Value(window,"ExpansionMode")==2&&settings.HoverToOpen;settings.BeforeSave=null;
            foreach(var theme in new[]{SettingsTheme.Light,SettingsTheme.Dark,SettingsTheme.System})
            {
                WebSettingsTest.Set(window,"SettingsTheme",(int)theme);
                checks[$"ThemePersists_{theme}"]=Settings.Load().SettingsTheme==theme;
                checks[$"ThemeDoesNotChangePanel_{theme}"]=settings.Appearance==PanelAppearance.FollowSystem;
                WebSettingsTest.Capture(window,Path.Combine(root,$"settings-theme-{theme}.png"));
            }
            void Emulate(string scheme){var task=window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia",JsonSerializer.Serialize(new{features=new[]{new{name="prefers-color-scheme",value=scheme}}}));WebSettingsTest.Wait(()=>task.IsCompleted);task.GetAwaiter().GetResult();Pump(120);}
            WebSettingsTest.Set(window,"SettingsTheme",0);Emulate("dark");checks["SystemThemeTracksDarkLive"]=WebSettingsTest.Script<string>(window,"document.documentElement.dataset.theme")=="dark"&&settings.SettingsTheme==SettingsTheme.System;
            Emulate("light");checks["SystemThemeTracksLightLive"]=WebSettingsTest.Script<string>(window,"document.documentElement.dataset.theme")=="light";
            WebSettingsTest.Set(window,"SettingsTheme",2);checks["ExplicitDarkIgnoresSystemLight"]=WebSettingsTest.Script<string>(window,"document.documentElement.dataset.theme")=="dark";
            WebSettingsTest.Script<bool>(window,"document.querySelector('[data-field=OpeningMode] .picker-trigger').click();true");WebSettingsTest.Capture(window,Path.Combine(root,"capsule-menu-dark.png"));
            checks["CapsuleMenuHasSelectedCheck"]=WebSettingsTest.Script<bool>(window,"!!document.querySelector('[data-field=OpeningMode] [role=option][aria-selected=true] svg')");WebSettingsTest.Script<bool>(window,"closePickers();true");
            WebSettingsTest.Set(window,"SettingsTheme",0);
            settings.BeforeSave=()=>throw new IOException("controlled theme failure");WebSettingsTest.Set(window,"SettingsTheme",2);
            checks["ThemeFailureRestoresPersistedAndDisplayedValue"]=settings.SettingsTheme==SettingsTheme.System&&WebSettingsTest.Value(window,"SettingsTheme")==0;settings.BeforeSave=null;
            WebSettingsTest.Page(window,2);WebSettingsTest.Set(window,"Appearance",3);WebSettingsTest.Set(window,"Columns",7);
            checks["AppearanceAndColumnsPersist"]=Settings.Load().Appearance==PanelAppearance.Dark&&Settings.Load().Columns==7;
            settings.BeforeSave=()=>throw new IOException("controlled appearance failure");WebSettingsTest.Set(window,"Appearance",1);WebSettingsTest.Set(window,"Columns",5);
            checks["AppearanceAndColumnsRollback"]=WebSettingsTest.Value(window,"Appearance")==3&&WebSettingsTest.Value(window,"Columns")==7;settings.BeforeSave=null;
            WebSettingsTest.Page(window,0);WebSettingsTest.Set(window,"StartupEnabled",true);checks["StartupUsesExistingService"]=startup;
            settings.ChangeBehavior(true,false,settings.MatchPanelMaterials);window.RefreshValues();WebSettingsTest.Page(window,1);
            checks["SelectedTypographyBaselineApplied"]=WebSettingsTest.Script<bool>(window,"getComputedStyle(document.querySelector('.setting-copy label')).fontSize==='17px'&&getComputedStyle(document.querySelector('.setting-copy p')).fontSize==='14px'&&getComputedStyle(document.querySelector('.page-heading h1')).fontSize==='32px'&&getComputedStyle(document.querySelector('#navigation button')).fontSize==='15px'&&getComputedStyle(document.querySelector('.setting-row')).minHeight==='92px'");
            checks["LinkedOptionsReachableWithScrolling"]=WebSettingsTest.Script<bool>(window,"(()=>{const main=document.querySelector('main');main.scrollTop=main.scrollHeight;const last=document.getElementById('RowBlankCellOpens').getBoundingClientRect();const visible=last.top>=main.getBoundingClientRect().top&&last.bottom<=innerHeight;main.scrollTop=0;return visible})()");
            checks["DeveloperTunerAbsentFromProduct"]=WebSettingsTest.Script<bool>(window,"!document.querySelector('#typography-panel,#typography-toggle,#native-developer-typography')");
            WebSettingsTest.Set(window,"OpeningMode",1);
            checks["IndependentShowsEffectiveModeAndKeepsPreference"]=WebSettingsTest.Value(window,"ExpansionMode")==0&&settings.FollowNativeOverflow;
            checks["LinkedOnlyControlsHiddenInIndependent"]=!WebSettingsTest.Exists(window,"ShortcutTarget")&&!WebSettingsTest.Exists(window,"BlankCellOpens");
            WebSettingsTest.Set(window,"ExpansionMode",2);checks["IndependentHoverPersists"]=settings.HoverToOpen&&!settings.FollowNativeOverflow;
            WebSettingsTest.Page(window,2);checks["IndependentPositionShowsActualFixedAbove"]=WebSettingsTest.Value(window,"Placement")==1&&!WebSettingsTest.Enabled(window,"Placement");
            WebSettingsTest.Page(window,1);WebSettingsTest.Set(window,"OpeningMode",0);checks["LinkedControlsReturnAfterModeSwitch"]=WebSettingsTest.Exists(window,"BlankCellOpens");
            checks["RemovedDecorationAbsent"]=WebSettingsTest.Script<bool>(window,"!document.querySelector('.eyebrow,.nav-caption,.prototype-note,.save-state')");
            foreach(var size in new[]{new Size(760,570),new Size(1100,800)})
            {
                window.Size=size;Pump(100);
                foreach(int section in new[]{0,1,2,3}){WebSettingsTest.Page(window,section);checks[$"HorizontalBounds_{size.Width}_{section}"]=WebSettingsTest.Script<bool>(window,"document.documentElement.scrollWidth<=innerWidth&&[...document.querySelectorAll('main .setting-row')].every(e=>e.getBoundingClientRect().right<=innerWidth)");WebSettingsTest.Capture(window,Path.Combine(root,$"settings-{size.Width}-{section}.png"));}
            }
            WebSettingsTest.Script<bool>(window,"send('Columns',999);true");Pump(200);
            checks["InvalidBridgeValueCannotChangeConfiguration"]=settings.Columns==7&&Settings.Load().Columns==7;
            window.Close();

        }
        catch(Exception error){errors.Add(error.ToString());}
        return Report(root,"settings",checks,errors);
    }
    private static int Report(string root,string name,Dictionary<string,bool> checks,List<string> errors)
    {bool passed=errors.Count==0&&checks.Count>0&&checks.Values.All(value=>value);File.WriteAllText(Path.Combine(root,name+"-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors},DiagnosticStore.JsonOptions));return passed?0:1;}
    private static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
}
