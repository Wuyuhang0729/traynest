using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace NestedTray;
internal enum SettingsTheme{System,Light,Dark}
internal enum OpeningMode{Linked,Independent}
internal enum NativeExpansionOrder{Simultaneous,AfterNative}
internal sealed class Settings
{
    public List<string> Collected {get;set;}=new();
    public List<SavedTool> Tools {get;set;}=new();
    public SettingsTheme SettingsTheme {get;set;}=SettingsTheme.System;
    public int Columns {get;set;}=5;
    public PopupPlacement Placement {get;set;}=PopupPlacement.Left;
    public PanelAppearance Appearance {get;set;}=PanelAppearance.FollowSystem;
    public bool FollowNativeOverflow {get;set;}
    public bool HoverToOpen {get;set;}
    public bool MatchPanelMaterials {get;set;}
    public NativeExpansionOrder ExpansionOrder {get;set;}=NativeExpansionOrder.Simultaneous;
    public OpeningMode OpeningMode {get;set;}=OpeningMode.Linked;
    internal const Keys DefaultShortcut=Keys.Alt|Keys.D;
    public Keys Shortcut {get;set;}=DefaultShortcut;
    public bool ShortcutConfigured {get;set;}
    public bool EntryPlacementInitialized {get;set;}
    public OpeningMode? EntryPlacementMode {get;set;}
    public bool? LinkedEntryOnTaskbar {get;set;}
    public bool? IndependentEntryOnTaskbar {get;set;}
    public bool ShortcutTargetConfigured {get;set;}
    public bool ShortcutBothPanels {get;set;}=true;
    public bool BlankCellOpens {get;set;}
    public bool BlankCellHintSeen {get;set;}
    public bool AutoCheckUpdates {get;set;}=true;
    public bool ImportantUpdateReminders {get;set;}=true;
    internal void ChangeUpdatePreference(bool automatic,bool important)
    {
        var previous=(AutoCheckUpdates,ImportantUpdateReminders);
        (AutoCheckUpdates,ImportantUpdateReminders)=(automatic,important);
        try{Save();}catch{(AutoCheckUpdates,ImportantUpdateReminders)=previous;throw;}
    }
    public int BlankCellHintVersion {get;set;}
    internal void ChangeOpeningMode(OpeningMode value)
    {var previous=OpeningMode;OpeningMode=value;try{Save();}catch{OpeningMode=previous;throw;}}
    internal Action? BeforeSave;
    internal static string DirectoryPath=>Environment.GetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NestedTray");
    internal static string FilePath=>Path.Combine(DirectoryPath,"settings.json");
    internal static Settings Load()
    {
        if(!File.Exists(FilePath))return new();
        try
        {
            var value=JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
            if(value?.Collected==null||value.Collected.Any(rule=>string.IsNullOrWhiteSpace(rule)))throw new JsonException("Invalid collected rules.");
            if(value.Tools==null||value.Tools.Any(tool=>tool==null||string.IsNullOrWhiteSpace(tool.Rule)||tool.IconPng==null||tool.Name==null||tool.Executable==null||tool.LaunchTarget==null||tool.IconPng.Length>65536))throw new JsonException("Invalid saved tools.");
            ExecutableIdentity.NormalizeSettings(value);
            value.Columns=Math.Clamp(value.Columns,5,8);
            if(!Enum.IsDefined(value.Placement))value.Placement=PopupPlacement.Left;
            if(!Enum.IsDefined(value.SettingsTheme))value.SettingsTheme=SettingsTheme.System;
            if(!Enum.IsDefined(value.Appearance))value.Appearance=PanelAppearance.FollowSystem;
            if(!Enum.IsDefined(value.ExpansionOrder))value.ExpansionOrder=NativeExpansionOrder.Simultaneous;
            if(!Enum.IsDefined(value.OpeningMode))value.OpeningMode=OpeningMode.Linked;
            if(!value.ShortcutConfigured&&value.Shortcut==Keys.None)value.Shortcut=DefaultShortcut;
            if(!value.ShortcutTargetConfigured)value.ShortcutBothPanels=true;
            if(value.EntryPlacementMode.HasValue&&!Enum.IsDefined(value.EntryPlacementMode.Value))value.EntryPlacementMode=null;
            if(!GlobalShortcut.Valid(value.Shortcut))value.Shortcut=Keys.None;
            if(value.FollowNativeOverflow&&value.HoverToOpen)value.HoverToOpen=false;
            value.Collected=value.Collected.Distinct(StringComparer.Ordinal).ToList();
            value.Tools=value.Tools.Where(tool=>value.Collected.Contains(tool.Rule)).DistinctBy(tool=>tool.Rule).ToList();return value;
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {throw new TrayNestException("TN1002",e);}
    }
    internal void Save()
    {
        try
        {
            BeforeSave?.Invoke();
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
            if(File.Exists(FilePath))File.Copy(FilePath,FilePath+".bak",true);
            AtomicFile.Commit(FilePath+".tmp",FilePath);
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){throw new TrayNestException("TN4001",error);}
    }
    internal void ChangePlacement(PopupPlacement value)
    {
        var previous=Placement;Placement=value;
        try{Save();}catch{Placement=previous;throw;}
    }
    internal void ChangeSettingsTheme(SettingsTheme value)
    {var previous=SettingsTheme;SettingsTheme=value;try{Save();}catch{SettingsTheme=previous;throw;}}
    internal void ChangeAppearance(PanelAppearance value)
    {
        var previous=Appearance;Appearance=value;
        try{Save();}catch{Appearance=previous;throw;}
    }
    internal void ChangeBehavior(bool follow,bool hover,bool match)
    {
        if(follow&&hover)throw new ArgumentException("自动展开方式只能选择一种。");
        var previous=(FollowNativeOverflow,HoverToOpen,MatchPanelMaterials);
        (FollowNativeOverflow,HoverToOpen,MatchPanelMaterials)=(follow,hover,match);
        try{Save();}catch{(FollowNativeOverflow,HoverToOpen,MatchPanelMaterials)=previous;throw;}
    }
    internal void ChangeExpansionOrder(NativeExpansionOrder value)
    {var previous=ExpansionOrder;ExpansionOrder=value;try{Save();}catch{ExpansionOrder=previous;throw;}}
    internal static bool StartupEnabled
    {
        get=>StartupRegistration.Enabled;
        set=>StartupRegistration.Enabled=value;
    }
}

internal sealed record RecoveryItem(long Window,uint Id,Guid Guid,int ProcessId,uint OriginalState);
internal sealed class RecoveryJournal
{
    internal readonly string PathName=Path.Combine(Settings.DirectoryPath,"recovery-"+Environment.ProcessId+".json");
    private Process? watcher;
    internal Action? BeforeWrite;
    internal void Start()
    {
        Directory.CreateDirectory(Settings.DirectoryPath);
        Write(Array.Empty<RecoveryItem>());
        watcher=Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--recover-after-exit",Environment.ProcessId.ToString(),PathName}});
        if(watcher==null)throw new InvalidOperationException("无法启动图标恢复保护进程。");
    }
    internal void Write(IEnumerable<RecoveryItem> items)
    {
        try{BeforeWrite?.Invoke();File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(items));AtomicFile.Commit(PathName+".tmp",PathName);}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){throw new TrayNestException("TN6002",error);}
    }
    internal void Complete() {File.Delete(PathName);watcher?.Dispose();}
    internal static void Watch(int parent,string path)
    {
        try {using var process=Process.GetProcessById(parent);process.WaitForExit();}catch(ArgumentException){}
        if(!File.Exists(path))return;
        var diagnostics=new DiagnosticStore(System.IO.Path.GetDirectoryName(path)!);
        diagnostics.Record("TN6003","Warning");
        try
        {
            var items=JsonSerializer.Deserialize<RecoveryItem[]>(File.ReadAllText(path))??[];
            bool allRestored=true;
            foreach(var item in items)
            {
                nint window=(nint)item.Window;
                Native.GetWindowThreadProcessId(window,out uint owner);
                if(!Native.IsWindow(window)||owner!=item.ProcessId)continue;
                var data=NativeTray.New(window,item.Id,item.Guid);data.Flags=8|(item.Guid==Guid.Empty?0u:0x20u);data.StateMask=1;data.State=item.OriginalState;
                bool restored=false;
                for(int attempt=0;attempt<20;attempt++)
                {
                    if(NativeTray.Notify(1,ref data)){restored=true;break;}
                    if(!Native.IsWindow(window)){restored=true;break;}
                    Thread.Sleep(250);
                }
                allRestored&=restored;
            }
            if(allRestored){File.Delete(path);diagnostics.Record("TN0003",context:new(){["RecoveryItems"]=items.Length.ToString()});}
            else diagnostics.Record("TN6001","Error",context:new(){["RecoveryItems"]=items.Length.ToString()});
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException)
        {new DiagnosticStore(System.IO.Path.GetDirectoryName(path)!).Record("TN6001","Error",e); /* Keep recovery file for manual inspection. */ }
    }
}
