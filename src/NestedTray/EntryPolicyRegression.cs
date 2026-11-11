using System.Text.Json;
namespace NestedTray;
internal static class EntryPolicyRegression
{
    internal static int Run(string root)
    {
        root=Path.GetFullPath(root);Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(root,"profile"));
        var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try
        {
            int writes=0;object? stored=null;var settings=new Settings{OpeningMode=OpeningMode.Independent};
            void Write(int? value){writes++;stored=value;}
            EntryPlacementPreference.Initialize(settings,()=>stored,Write);
            checks["IndependentDefaultsToTaskbar"]=stored is 1&&settings.IndependentEntryOnTaskbar==true;
            stored=0;int before=writes;EntryPlacementPreference.Initialize(settings,()=>stored,Write);
            checks["UserHiddenChoiceRememberedWithoutForcing"]=writes==before&&settings.IndependentEntryOnTaskbar==false;
            settings.OpeningMode=OpeningMode.Linked;EntryPlacementPreference.Initialize(settings,()=>stored,Write);
            checks["LinkedDefaultsToHiddenArea"]=stored is 0&&settings.LinkedEntryOnTaskbar==false;
            stored=1;settings.OpeningMode=OpeningMode.Independent;EntryPlacementPreference.Initialize(settings,()=>stored,Write);
            checks["ModesKeepSeparateUserChoices"]=stored is 0&&settings.LinkedEntryOnTaskbar==true&&settings.IndependentEntryOnTaskbar==false;
            settings.OpeningMode=OpeningMode.Linked;EntryPlacementPreference.Initialize(settings,()=>stored,Write);
            checks["SwitchBackRestoresLinkedChoice"]=stored is 1;
            stored=0;before=writes;settings=Settings.Load();EntryPlacementPreference.Initialize(settings,()=>stored,Write);
            checks["RestartRespectsLatestWindowsChoice"]=stored is 0&&writes==before&&settings.LinkedEntryOnTaskbar==false;
            foreach(object choice in new object[]{0,1,"unknown"})
            {var first=new Settings{OpeningMode=OpeningMode.Independent};before=writes;EntryPlacementPreference.Initialize(first,()=>choice,value=>writes++);checks["ExistingChoicePreserved_"+choice]=writes==before;}
            settings=new Settings{OpeningMode=OpeningMode.Independent,BeforeSave=()=>throw new IOException("controlled failure")};stored=null;bool failed=false;
            try{EntryPlacementPreference.Initialize(settings,()=>stored,Write);}catch(TrayNestException){failed=true;}
            checks["SaveFailureRollsBackPreferenceAndMemory"]=failed&&!settings.EntryPlacementInitialized&&settings.EntryPlacementMode==null&&stored==null;
            settings.BeforeSave=null;EntryPlacementPreference.Initialize(settings,()=>stored,Write);checks["SaveFailureCanRetry"]=stored is 1&&settings.EntryPlacementInitialized;
            Directory.CreateDirectory(Settings.DirectoryPath);
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Shortcut\":0,\"ShortcutBothPanels\":false}");var migrated=Settings.Load();
            checks["OldDefaultsBecomeAltDAndBothPanels"]=migrated.Shortcut==(Keys.Alt|Keys.D)&&migrated.ShortcutBothPanels;
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Shortcut\":0,\"ShortcutConfigured\":true,\"ShortcutBothPanels\":false,\"ShortcutTargetConfigured\":true}");
            checks["ExplicitClearAndOnlyCollectedChoicePersist"]=Settings.Load().Shortcut==Keys.None&&!Settings.Load().ShortcutBothPanels;
            File.WriteAllText(Settings.FilePath,"{\"Collected\":[],\"Tools\":[],\"Shortcut\":458875}");checks["ExistingCustomShortcutPreserved"]=Settings.Load().Shortcut==(Keys.Control|Keys.Shift|Keys.Alt|Keys.F12);
            var click=new BlankCellDoubleClick();var point=new Point(100,100);var area=new Size(4,4);bool open=false;
            checks["SingleClickDoesNotToggle"]=!click.Observe(1,point,100,500,area,out _);
            if(click.Observe(1,point,200,500,area,out _))open=!open;checks["DoubleClickOpens"]=open;
            click.Observe(1,point,400,500,area,out _);if(click.Observe(1,point,500,500,area,out _))open=!open;checks["NextDoubleClickCloses"]=!open;
            click.Reset();click.Observe(1,point,1000,500,area,out _);checks["SlowClicksDoNotPair"]=!click.Observe(1,point,1600,500,area,out _);
            click.Reset();click.Observe(1,point,2000,500,area,out _);checks["DifferentCellDoesNotPair"]=!click.Observe(1,new Point(140,100),2100,500,area,out _);
            click.Reset();click.Observe(1,point,3000,500,area,out _);click.Reset();checks["DraggingCancelsPendingDoubleClick"]=!click.Observe(1,point,3100,500,area,out _);
            click.Reset();click.Observe(1,point,4000,500,area,out _);checks["DifferentNativeWindowDoesNotPair"]=!click.Observe(2,point,4100,500,area,out _);
            var separated=new Settings{OpeningMode=OpeningMode.Independent,EntryPlacementMode=OpeningMode.Linked,LinkedEntryOnTaskbar=false,IndependentEntryOnTaskbar=true};
            EntryPlacementPreference.Initialize(separated,()=>1,value=>throw new InvalidOperationException("Existing per-mode choice must not be changed"),true);
            checks["SeparateIdentityDoesNotOverwriteOtherModeMemory"]=separated.LinkedEntryOnTaskbar==false&&separated.IndependentEntryOnTaskbar==true;
            var motion=new EntryArrowMotion();motion.SetTarget(true,1000,true);
            checks["ArrowRotationStartsAtCurrentAngle"]=motion.Angle(1000)==0;
            checks["ArrowRotationHasIntermediateVectorAngles"]=Math.Abs(motion.Angle(1080)-90)<.001&&motion.Active(1080);
            checks["ArrowRotationFinishesDown"]=motion.Angle(1160)==180&&!motion.Active(1160);
            motion.Reset();motion.SetTarget(true,2000,true);double interrupted=motion.Angle(2060);motion.SetTarget(false,2060,true);
            checks["RapidReverseDoesNotJump"]=Math.Abs(motion.Angle(2060)-interrupted)<.001;checks["RapidReverseFinishesUp"]=motion.Angle(2300)==0;
            motion.SetTarget(true,3000,false);checks["SystemAnimationPreferenceRespected"]=motion.Angle(3000)==180&&!motion.Active(3000);
            using var image=BlankCellHint.RenderIllustration();image.Save(Path.Combine(root,"functional-help.png"));
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=errors.Count>0?false:checks.Values.All(x=>x);File.WriteAllText(Path.Combine(root,"entry-policy-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="No desktop input, no visible windows, no real registry writes; injected preference store and offscreen UI rendering."},DiagnosticStore.JsonOptions));return passed?0:1;
    }
}
