namespace NestedTray;
internal sealed class EntryIcon:NativeWindow,IDisposable
{
    private const uint Id=401,Callback=0x8000+741;
    private readonly Guid guid=new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Environment.ProcessPath!)).AsSpan(0,16));
    private readonly System.Windows.Forms.Timer clickDelay=new(){Interval=50};
    private readonly Icon icon;
    internal Action? Open,Menu,ExplorerRestarted,DiagnosticsRequested;
    private readonly uint taskbarCreated=Native.RegisterWindowMessageW("TaskbarCreated");
    private readonly uint diagnosticsMessage=Native.RegisterWindowMessageW("NestedTray.Diagnostics.v1");
    internal EntryIcon()
    {
        CreateHandle(new CreateParams{Caption="NestedTray entry",ExStyle=0x08000080});
        using var bitmap=new Bitmap(32,32);using(var graphics=Graphics.FromImage(bitmap))
        {graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;graphics.Clear(Color.Transparent);using var pen=new Pen(Color.FromArgb(82,166,255),3);graphics.DrawRectangle(pen,5,5,20,20);graphics.DrawRectangle(pen,12,12,14,14);}
        nint handle=bitmap.GetHicon();try{icon=(Icon)Icon.FromHandle(handle).Clone();}finally{Native.DestroyIcon(handle);}
        Add();clickDelay.Tick+=(_,_)=>{clickDelay.Stop();Open?.Invoke();};
    }
    private void Add()
    {
        var data=NativeTray.New(Handle,Id,guid);data.Flags=1|2|4|0x20|0x80;data.CallbackMessage=Callback;data.Icon=icon.Handle;data.Tip="嵌套面板";
        if(!NativeTray.Notify(0,ref data))throw new InvalidOperationException("无法创建托盘入口图标。");
        data.Version=4;if(!NativeTray.Notify(4,ref data))throw new InvalidOperationException("无法设置托盘入口交互版本。");
    }
    internal Rectangle Bounds=>Native.GetIconRect(Handle,Id,guid);
    internal void Notify(string code)
    {
        var data=NativeTray.New(Handle,Id,guid);data.Flags=0x10|0x20;data.InfoTitle="TrayNest · "+code;
        data.Info=ErrorCodes.Message(code)+" 右键入口可打开“诊断与反馈”。";data.InfoFlags=2;
        NativeTray.Notify(1,ref data);
    }
    protected override void WndProc(ref Message message)
    {
        if((uint)message.Msg==diagnosticsMessage){DiagnosticsRequested?.Invoke();message.Result=1;return;}
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
        clickDelay.Dispose();if(Handle!=0){var data=NativeTray.New(Handle,Id,guid);data.Flags=0x20;NativeTray.Notify(2,ref data);DestroyHandle();}icon.Dispose();
    }
}
