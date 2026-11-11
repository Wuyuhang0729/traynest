using System.Runtime.InteropServices;
namespace NestedTray;
internal sealed class GlobalShortcut:NativeWindow,IDisposable
{
    private int currentId,nextId=100;
    private Keys current;
    internal Action? Pressed;
    internal GlobalShortcut(){CreateHandle(new CreateParams{Caption="TrayNest shortcut",Parent=new nint(-3)});}
    internal static bool Valid(Keys value)
    {
        if(value==Keys.None)return true;
        var key=value&Keys.KeyCode;var mods=value&Keys.Modifiers;
        return mods!=Keys.None&&(mods&~(Keys.Control|Keys.Alt|Keys.Shift))==0&&(key is >=Keys.A and <=Keys.Z or >=Keys.D0 and <=Keys.D9 or >=Keys.F1 and <=Keys.F24);
    }
    internal static string Display(Keys value)=>value==Keys.None?"未设置":new KeysConverter().ConvertToString(value)??"";
    // Reserve first, persist, then release the old registration. Failure keeps the old hotkey.
    internal void Change(Settings settings,Keys value,bool both)
    {
        if(!Valid(value))throw new ShortcutException("请使用 Ctrl、Alt 或 Shift 加字母、数字或功能键。");
        int reserved=0;
        if(value!=Keys.None&&value!=current)
        {
            reserved=++nextId;
            if(!RegisterHotKey(Handle,reserved,Modifiers(value),(uint)(value&Keys.KeyCode)))throw new ShortcutException("此快捷键已被占用或系统保留，请换一个组合。原设置已保留。");
        }
        var previous=(settings.Shortcut,settings.ShortcutBothPanels,settings.ShortcutConfigured,settings.ShortcutTargetConfigured);
        settings.Shortcut=value;settings.ShortcutBothPanels=both;settings.ShortcutConfigured=true;settings.ShortcutTargetConfigured=true;
        try{settings.Save();}
        catch{(settings.Shortcut,settings.ShortcutBothPanels,settings.ShortcutConfigured,settings.ShortcutTargetConfigured)=previous;if(reserved!=0)UnregisterHotKey(Handle,reserved);throw;}
        if(value!=current){if(currentId!=0)UnregisterHotKey(Handle,currentId);currentId=reserved;current=value;}
    }
    private static uint Modifiers(Keys value)=>0x4000|((value&Keys.Alt)!=0?1u:0)|((value&Keys.Control)!=0?2u:0)|((value&Keys.Shift)!=0?4u:0);
    internal bool Restore(Keys value)
    {
        if(value==Keys.None)return true;if(!Valid(value))return false;
        int id=++nextId;if(!RegisterHotKey(Handle,id,Modifiers(value),(uint)(value&Keys.KeyCode)))return false;
        currentId=id;current=value;return true;
    }
    protected override void WndProc(ref Message message){if(message.Msg==0x312&&message.WParam.ToInt32()==currentId){Pressed?.Invoke();return;}base.WndProc(ref message);}
    public void Dispose(){if(currentId!=0)UnregisterHotKey(Handle,currentId);currentId=0;DestroyHandle();}
    [DllImport("user32.dll",SetLastError=true)]internal static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]internal static extern bool UnregisterHotKey(nint window,int id);
}
internal sealed class ShortcutException(string message):Exception(message);
