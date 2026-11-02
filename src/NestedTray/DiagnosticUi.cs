using System.Diagnostics;

namespace NestedTray;

internal static class DiagnosticUi
{
    internal const string IssueUrl="https://github.com/Wuyuhang0729/traynest/issues/new?template=bug_report.yml";
    internal static int OpenWindows{get;private set;}
    internal static void Show(DiagnosticEvent? problem=null)
    {
        using var window=new Form
        {
            Icon=AppIcon.Load(),Text=problem==null?"TrayNest · 诊断与反馈":$"TrayNest · {problem.Code}",
            ClientSize=new Size(680,460),MinimumSize=new Size(520,360),StartPosition=FormStartPosition.CenterScreen,
            ShowInTaskbar=false,TopMost=true,AutoScaleMode=AutoScaleMode.Dpi
        };
        var description=new Label{Dock=DockStyle.Top,Height=74,Padding=new Padding(12),Text=problem==null
            ?"遇到问题时，可复制或导出诊断报告，并在 GitHub 填写复现步骤。报告会过滤个人路径和图标文本，提交前请检查内容。"
            :$"{problem.Message}\n错误编号：{problem.Code}    事件 ID：{problem.EventId}"};
        var report=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font(FontFamily.GenericMonospace,9)};
        var status=new Label{Dock=DockStyle.Bottom,Height=28,Padding=new Padding(12,4,0,0)};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,Padding=new Padding(8),WrapContents=false};
        var copy=new Button{Text="复制诊断报告",AutoSize=true};
        var export=new Button{Text="导出诊断报告…",AutoSize=true};
        var issue=new Button{Text="打开 GitHub 反馈",AutoSize=true};
        var close=new Button{Text="关闭",AutoSize=true,DialogResult=DialogResult.Cancel};
        actions.Controls.AddRange([copy,export,issue,close]);
        window.Controls.Add(report);window.Controls.Add(description);window.Controls.Add(status);window.Controls.Add(actions);window.CancelButton=close;
        var snapshot=AppDiagnostics.Snapshot();
        report.Text=AppDiagnostics.Store.Report(snapshot,problem);
        copy.Click+=(_,_)=>
        {
            try{Clipboard.SetText(report.Text);status.Text="诊断报告已复制。请检查内容后粘贴到 Issue。";}
            catch(Exception error){AppDiagnostics.Error("TN5002",error);status.Text=ErrorCodes.Message("TN5002");}
        };
        export.Click+=(_,_)=>
        {
            using var dialog=new SaveFileDialog{Title="导出 TrayNest 诊断报告",Filter="JSON 诊断报告 (*.json)|*.json",FileName=$"TrayNest-report-{DateTime.Now:yyyyMMdd-HHmmss}.json",OverwritePrompt=true,AddExtension=true};
            if(dialog.ShowDialog(window)!=DialogResult.OK)return;
            try{AppDiagnostics.Store.Export(dialog.FileName,snapshot,problem);status.Text="诊断报告已保存，可作为附件添加到 Issue。";}
            catch(Exception error){AppDiagnostics.Error("TN5001",error);status.Text=ErrorCodes.Message("TN5001");}
        };
        issue.Click+=(_,_)=>
        {
            try{Process.Start(new ProcessStartInfo(IssueUrl){UseShellExecute=true});status.Text="请填写复现步骤，并粘贴报告或添加导出的 JSON 附件。";}
            catch(Exception error){AppDiagnostics.Error("TN5002",error);status.Text=ErrorCodes.Message("TN5002");}
        };
        OpenWindows++;
        try{window.ShowDialog();}finally{OpenWindows--;}
    }

    internal static void Error(string code,Exception? error=null)=>Show(AppDiagnostics.Error(code,error));
}

internal static class CompatibilityProbe
{
    internal static bool Supported(bool windows,int build,System.Runtime.InteropServices.Architecture process,System.Runtime.InteropServices.Architecture os)
        =>windows&&build>=22000&&process==System.Runtime.InteropServices.Architecture.X64&&os==System.Runtime.InteropServices.Architecture.X64;

    internal static void EnsureStartup(bool logon=false)
    {
        if(!Supported(OperatingSystem.IsWindows(),Environment.OSVersion.Version.Build,System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture,System.Runtime.InteropServices.RuntimeInformation.OSArchitecture))
            throw new TrayNestException("TN1001");
        if(!WaitForTray(()=>Native.FindWindowW("Shell_TrayWnd",null)!=0,()=>Environment.TickCount64,Thread.Sleep,logon?120000:20000))throw new TrayNestException("TN1004");
        AppDiagnostics.Info("TN0011",new(){["LaunchMode"]=logon?"Logon":"Manual"});
    }
    internal static bool WaitForTray(Func<bool> ready,Func<long> clock,Action<int> sleep,long timeout)
    {
        long started=clock();
        while(!ready()){if(clock()-started>=timeout)return false;sleep(200);}
        return true;
    }
}
