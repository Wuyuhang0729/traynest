using Microsoft.Win32;
namespace NestedTray;
internal static class EntryPlacementPreference
{
    internal static bool Initialize(Settings settings,Guid identity,string executable)
    {
        try
        {
            using var root=Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");if(root==null)return false;
            foreach(var name in root.GetSubKeyNames())
            {
                using var read=root.OpenSubKey(name);if(read==null||!Matches(read,identity,executable))continue;
                using var own=Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings\"+name,true);
                if(own==null||!Matches(own,identity,executable))return false;
                return Initialize(settings,()=>own.GetValue("IsPromoted"),value=>{if(value.HasValue)own.SetValue("IsPromoted",value.Value,RegistryValueKind.DWord);else own.DeleteValue("IsPromoted",false);},true);
            }
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException or TrayNestException){AppDiagnostics.Warn("TN1101",error:error);}
        return false;
    }
    internal static void Capture(Settings settings,OpeningMode mode,Guid identity,string executable)
    {
        try
        {
            using var root=Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");if(root==null)return;
            foreach(var name in root.GetSubKeyNames())
            {
                using var key=root.OpenSubKey(name);if(key==null||!Matches(key,identity,executable)||key.GetValue("IsPromoted") is not int value||value is not (0 or 1))continue;
                bool? previous=mode==OpeningMode.Linked?settings.LinkedEntryOnTaskbar:settings.IndependentEntryOnTaskbar;
                if(previous==(value==1))return;
                if(mode==OpeningMode.Linked)settings.LinkedEntryOnTaskbar=value==1;else settings.IndependentEntryOnTaskbar=value==1;
                try{settings.Save();}catch{if(mode==OpeningMode.Linked)settings.LinkedEntryOnTaskbar=previous;else settings.IndependentEntryOnTaskbar=previous;throw;}return;
            }
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException or TrayNestException){AppDiagnostics.Warn("TN1101",error:error);}
    }
    private static bool Matches(RegistryKey key,Guid identity,string executable)=>key.GetValue("IconGuid") is string text&&Guid.TryParse(text,out var parsed)&&parsed==identity&&key.GetValue("ExecutablePath") is string path&&string.Equals(Environment.ExpandEnvironmentVariables(path),executable,StringComparison.OrdinalIgnoreCase);
    internal static bool Initialize(Settings settings,Func<object?> read,Action<int?> write,bool separateIdentity=false)
    {
        var previous=(settings.LinkedEntryOnTaskbar,settings.IndependentEntryOnTaskbar,settings.EntryPlacementMode,settings.EntryPlacementInitialized);
        object? raw=read();int? stored=raw is int value&&value is 0 or 1?value:null;bool changedRegistry=false;
        try
        {
            var old=separateIdentity?settings.OpeningMode:settings.EntryPlacementMode??(settings.EntryPlacementInitialized?OpeningMode.Independent:(OpeningMode?)null);
            if(old.HasValue&&stored.HasValue)Remember(old.Value,stored.Value==1);
            if(!old.HasValue&&stored.HasValue)Remember(settings.OpeningMode,stored.Value==1);
            bool desired=(settings.OpeningMode==OpeningMode.Linked?settings.LinkedEntryOnTaskbar:settings.IndependentEntryOnTaskbar)??settings.OpeningMode==OpeningMode.Independent;
            if(raw==null||stored.HasValue&&stored.Value!=(desired?1:0)){write(desired?1:0);changedRegistry=true;}
            Remember(settings.OpeningMode,desired);settings.EntryPlacementMode=settings.OpeningMode;settings.EntryPlacementInitialized=true;
            var current=(settings.LinkedEntryOnTaskbar,settings.IndependentEntryOnTaskbar,settings.EntryPlacementMode,settings.EntryPlacementInitialized);
            if(current!=previous)settings.Save();return true;
        }
        catch
        {
            (settings.LinkedEntryOnTaskbar,settings.IndependentEntryOnTaskbar,settings.EntryPlacementMode,settings.EntryPlacementInitialized)=previous;
            if(changedRegistry)write(stored);throw;
        }
        void Remember(OpeningMode mode,bool visible){if(mode==OpeningMode.Linked)settings.LinkedEntryOnTaskbar=visible;else settings.IndependentEntryOnTaskbar=visible;}
    }
}
