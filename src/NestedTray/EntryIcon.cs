namespace NestedTray;
internal sealed class EntryIcon:NativeWindow,IDisposable
{
    private const uint Id=401,Callback=0x8000+741;
    private readonly Guid independentGuid=new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Environment.ProcessPath!)).AsSpan(0,16));
    private readonly Guid linkedGuid=new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Environment.ProcessPath!+"|linked-entry")).AsSpan(0,16));
    private Guid guid;
    private readonly System.Windows.Forms.Timer clickDelay=new(){Interval=50};
    private Icon icon;
    private bool independent,expanded,needsRedraw;
    private readonly EntryArrowMotion arrowMotion=new();
    private readonly System.Windows.Forms.Timer arrowFrames=new(){Interval=15};
    internal bool Expanded=>expanded;
    internal bool Independent=>independent;
    internal Guid Identity=>guid;
    internal Action? Open,Menu,ExplorerRestarted,DiagnosticsRequested,SettingsRequested;
    private readonly uint taskbarCreated=Native.RegisterWindowMessageW("TaskbarCreated");
    private readonly uint diagnosticsMessage=Native.RegisterWindowMessageW("NestedTray.Diagnostics.v1");
    private readonly uint settingsMessage=Native.RegisterWindowMessageW("NestedTray.Settings.v1");
    internal EntryIcon(OpeningMode mode=OpeningMode.Linked,string caption="NestedTray entry")
    {
        CreateHandle(new CreateParams{Caption=caption,ExStyle=0x08000080});
        independent=mode==OpeningMode.Independent;guid=independent?independentGuid:linkedGuid;
        icon=independent?CreateIndependentIcon():AppIcon.Load(32);
        arrowFrames.Tick+=(_,_)=>
        {
            bool painted=UpdateImage(true,false);
            if(painted&&!arrowMotion.Active(Environment.TickCount64))arrowFrames.Stop();
        };
        Add();clickDelay.Tick+=(_,_)=>{clickDelay.Stop();Open?.Invoke();};
    }
    private void Add()
    {
        var data=NativeTray.New(Handle,Id,guid);data.Flags=1|2|4|0x20|0x80;data.CallbackMessage=Callback;data.Icon=icon.Handle;data.Tip=independent?"TrayNest · 独立收纳面板":"TrayNest · 托盘收纳盒";
        if(!NativeTray.Notify(0,ref data))throw new InvalidOperationException("无法创建托盘入口图标。");
        data.Version=4;if(!NativeTray.Notify(4,ref data))throw new InvalidOperationException("无法设置托盘入口交互版本。");
    }
    internal static Icon CreateIndependentIcon(bool expanded=false)=>CreateIndependentIcon(expanded?180:0);
    internal static Icon CreateIndependentIcon(double angle)
    {
        using var bitmap=new Bitmap(32,32,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen=new Pen(Color.FromArgb(0x25,0x63,0xeb),6.4f){StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round,LineJoin=System.Drawing.Drawing2D.LineJoin.Round};
            graphics.TranslateTransform(16,15.5f);graphics.RotateTransform((float)angle);graphics.TranslateTransform(-16,-15.5f);
            graphics.DrawLines(pen,[new PointF(8,19),new PointF(16,11),new PointF(24,19)]);
        }
        nint handle=bitmap.GetHicon();try{using var borrowed=Icon.FromHandle(handle);return (Icon)borrowed.Clone();}finally{Native.DestroyIcon(handle);}
    }
    internal void SetIndependent(bool value)
    {
        if(independent==value)return;
        arrowFrames.Stop();clickDelay.Stop();arrowMotion.Reset();expanded=false;
        var previousGuid=guid;var previousIcon=icon;bool previousMode=independent;
        var replacement=value?CreateIndependentIcon():AppIcon.Load(32);
        var data=NativeTray.New(Handle,Id,guid);data.Flags=0x20;
        if(!NativeTray.Notify(2,ref data)){replacement.Dispose();throw new InvalidOperationException("无法切换托盘入口。");}
        guid=value?independentGuid:linkedGuid;icon=replacement;independent=value;
        try{Add();previousIcon.Dispose();}
        catch{guid=previousGuid;icon=previousIcon;independent=previousMode;replacement.Dispose();Add();throw;}
    }
    internal void SetExpanded(bool value)
    {
        if(!independent)return;
        if(expanded==value){if(needsRedraw)UpdateImage(true,false);return;}
        expanded=value;arrowMotion.SetTarget(value,Environment.TickCount64,SystemAnimation.Enabled);
        UpdateImage(true,false);
        if(arrowMotion.Active(Environment.TickCount64)||needsRedraw)arrowFrames.Start();else arrowFrames.Stop();
    }
    private bool UpdateImage(bool value,bool updateTooltip=true)
    {
        var replacement=value?CreateIndependentIcon(arrowMotion.Angle(Environment.TickCount64)):AppIcon.Load(32);
        var data=NativeTray.New(Handle,Id,guid);data.Flags=2|0x20|(updateTooltip?4u:0u);data.Icon=replacement.Handle;
        data.Tip=value?"TrayNest · 独立收纳面板":"TrayNest · 托盘收纳盒";
        if(!NativeTray.Notify(1,ref data)){replacement.Dispose();needsRedraw=true;return false;}
        var previous=icon;icon=replacement;previous.Dispose();needsRedraw=false;return true;
    }
    internal Rectangle Bounds=>Native.GetIconRect(Handle,Id,guid);
    internal void Notify(string code)
    {
        var data=NativeTray.New(Handle,Id,guid);data.Flags=0x10|0x20;data.InfoTitle="TrayNest · "+code;
        data.Info=ErrorCodes.Message(code)+" 可在设置 → 关于中打开诊断与反馈。";data.InfoFlags=2;
        NativeTray.Notify(1,ref data);
    }
    internal void NotifyUpdate(string version)
    {
        var data=NativeTray.New(Handle,Id,guid);data.Flags=0x10|0x20;data.InfoTitle="TrayNest 有重要更新";
        data.Info="发现版本 "+version+"，可在设置 → 常规中查看并下载。";data.InfoFlags=1;NativeTray.Notify(1,ref data);
    }
    protected override void WndProc(ref Message message)
    {
        if((uint)message.Msg==diagnosticsMessage){DiagnosticsRequested?.Invoke();message.Result=1;return;}
        if((uint)message.Msg==settingsMessage){SettingsRequested?.Invoke();message.Result=1;return;}
        if((uint)message.Msg==taskbarCreated)ExplorerRestarted?.Invoke();
        if((uint)message.Msg==Callback)
        {
            uint code=unchecked((uint)message.LParam.ToInt64())&0xFFFF;
            if(code is 0x400 or 0x401 or 0x202){clickDelay.Stop();clickDelay.Start();}
            if(code==0x7B)Menu?.Invoke();
        }
        base.WndProc(ref message);
    }
    public void Dispose()
    {
        arrowFrames.Dispose();clickDelay.Dispose();if(Handle!=0){var data=NativeTray.New(Handle,Id,guid);data.Flags=0x20;NativeTray.Notify(2,ref data);DestroyHandle();}icon.Dispose();
    }
}
