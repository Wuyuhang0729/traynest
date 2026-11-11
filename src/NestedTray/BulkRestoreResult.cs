namespace NestedTray;

internal readonly record struct BulkRestoreResult(int Restored,int ClearedOffline,int Failed,bool RecoveryBlocked)
{
    internal string Message=>$"已移回 {Restored} 个图标，已清除 {ClearedOffline} 个离线条目，失败 {Failed} 项。"+
        (RecoveryBlocked?"\n恢复记录无法安全更新，已停止后续清空。请保留恢复文件，通过“诊断与反馈”检查，正常退出后重新打开再重试。":
        Failed>0?"\n部分完成。未完成条目已保留，可再次点击“全部移出”重试。":"");
}

internal static class BulkRestoreDialog
{
    internal static Form Create()
    {
        var dialog=new Form{Icon=AppIcon.Load(),Text="移出全部图标",StartPosition=FormStartPosition.CenterScreen,ClientSize=new Size(440,160),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false};
        var body=new Label{Text="将正在运行的软件图标移回系统托盘，并清空收纳列表、排列顺序和启动文件设置。未运行的软件不会因此启动。",Bounds=new Rectangle(20,20,400,70)};
        var confirm=new Button{Text="全部移出",DialogResult=DialogResult.OK,Bounds=new Rectangle(210,110,105,32)};
        var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(325,110,90,32),TabIndex=0};
        dialog.Controls.AddRange([body,confirm,cancel]);dialog.AcceptButton=cancel;dialog.CancelButton=cancel;dialog.ActiveControl=cancel;return dialog;
    }
}
