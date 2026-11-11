using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace NestedTray;

internal sealed class SettingsWindow:Form
{
    private readonly Settings settings;
    private readonly Action changed;
    private readonly Func<bool> readStartup;
    private readonly Action<bool> writeStartup;
    private WebView2 browser=new(){Dock=DockStyle.Fill};
    private readonly Label loading=new(){Dock=DockStyle.Fill,Text="正在打开设置…",TextAlign=ContentAlignment.MiddleCenter};
    private bool started,operationInProgress;
    private int page=1;
    private string? pendingStatus;
    private bool pendingError;
    internal bool IsReady{get;private set;}
    internal string? InitializationError{get;private set;}
    internal bool RecordingShortcut{get;private set;}
    internal Action? OpenDiagnostics;
    internal Action? OpenPanelRequested,RefreshIconsRequested,RestoreAllRequested,ExitRequested;
    internal Action<string>? UpdateRequested;
    internal Func<UpdateView>? ReadUpdate;
    internal void RefreshUpdate()=>Post(new{type="update",value=ReadUpdate?.Invoke()});
    internal Func<bool,Task>? ChangeMaterialSync;
    internal Action<Keys,bool>? ChangeShortcut;
    internal WebView2 Browser=>browser;
    internal SettingsWindow(Settings settings,Action changed,Func<bool> readStartup,Action<bool> writeStartup)
    {
        this.settings=settings;this.changed=changed;this.readStartup=readStartup;this.writeStartup=writeStartup;
        Text="TrayNest 设置";Name="TrayNestSettings";Icon=AppIcon.Load();StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(1020,632);MinimumSize=new Size(760,570);
        Font=SystemFonts.MessageBoxFont;Controls.Add(browser);Controls.Add(loading);loading.BringToFront();ApplyFrameTheme();
        Shown+=async(_,_)=>{if(!started)await InitializeAsync();};
        Deactivate+=(_,_)=>RecordingShortcut=false;
    }
    private static string Resource(string name)
    {using var stream=typeof(SettingsWindow).Assembly.GetManifestResourceStream("NestedTray.Settings."+name)??throw new InvalidOperationException("Missing settings resource: "+name);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    internal static string Document()
    {
        using var icon=AppIcon.Load();using var bitmap=icon.ToBitmap();using var buffer=new MemoryStream();bitmap.Save(buffer,System.Drawing.Imaging.ImageFormat.Png);
        return Resource("index.html").Replace("{{DAISY}}",Resource("daisyui.css")).Replace("{{STYLE}}",Resource("styles.css")).Replace("{{SCRIPT}}",Resource("app.js")).Replace("{{ICON}}","data:image/png;base64,"+Convert.ToBase64String(buffer.ToArray()));
    }
    private async Task InitializeAsync()
    {
        started=true;InitializationError=null;
        try
        {
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Settings.DirectoryPath,"settings-webview"));
            if(IsDisposed)return;
            await browser.EnsureCoreWebView2Async(environment);
            if(IsDisposed)return;
            var core=browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;
            SetBrowserTheme();
            bool initialDocument=true;
            core.NavigationStarting+=(_,e)=>{if(initialDocument&&e.Uri.StartsWith("data:text/html;charset=utf-8;base64,",StringComparison.Ordinal)){initialDocument=false;return;}e.Cancel=true;};
            core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            core.WebResourceRequested+=(_,e)=>{if(Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https")e.Response=environment.CreateWebResourceResponse(null,403,"Blocked","");};
            core.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess&&!IsDisposed){InitializationError="Navigation: "+e.WebErrorStatus;ShowLoadFailure("设置界面未能加载，请重试。");}};
            // Leave the WebView event before opening native dialogs or closing the browser.
            core.WebMessageReceived+=(_,e)=>{string source=e.Source,json=e.WebMessageAsJson;if(!IsDisposed)BeginInvoke((Action)(()=>OnMessage(source,json)));};
            core.ProcessFailed+=(_,_)=>{IsReady=false;ShowLoadFailure("设置界面进程已退出，请重试。");};
            browser.NavigateToString(Document());
        }
        catch(Exception error){if(IsDisposed)return;InitializationError=error.Message;AppDiagnostics.Error("TN4001",error);ShowLoadFailure("设置界面未能加载，请重试。请确认 Microsoft Edge WebView2 Runtime 可用。");}
    }
    private void ShowLoadFailure(string text)
    {
        if(IsDisposed)return;IsReady=false;loading.Text=text;loading.Visible=true;loading.BringToFront();
        if(Controls.Find("RetrySettings",false).Length>0)return;
        var quit=new Button{Name="ExitApplication",Text="退出 TrayNest",Size=new Size(112,34),Anchor=AnchorStyles.Bottom|AnchorStyles.Left,Location=new Point(22,ClientSize.Height-56)};quit.Click+=(_,_)=>ExitRequested?.Invoke();Controls.Add(quit);quit.BringToFront();
        var retry=new Button{Name="RetrySettings",Text="重试",Size=new Size(86,34),Anchor=AnchorStyles.Bottom|AnchorStyles.Right,Location=new Point(ClientSize.Width-110,ClientSize.Height-56)};
        retry.Click+=async(_,_)=>{Controls.Remove(retry);retry.Dispose();Controls.Remove(quit);quit.Dispose();Controls.Remove(browser);browser.Dispose();browser=new WebView2{Dock=DockStyle.Fill};Controls.Add(browser);ApplyFrameTheme();await InitializeAsync();};Controls.Add(retry);retry.BringToFront();
    }
    private void SetBrowserTheme()
    {if(browser.CoreWebView2!=null)browser.CoreWebView2.Profile.PreferredColorScheme=settings.SettingsTheme switch{SettingsTheme.Light=>CoreWebView2PreferredColorScheme.Light,SettingsTheme.Dark=>CoreWebView2PreferredColorScheme.Dark,_=>CoreWebView2PreferredColorScheme.Auto};}
    private void ApplyFrameTheme(bool? actualDark=null)
    {
        bool systemDark=false;try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");systemDark=key?.GetValue("AppsUseLightTheme") is int value&&value==0;}catch{}
        bool dark=actualDark??(settings.SettingsTheme==SettingsTheme.Dark||settings.SettingsTheme==SettingsTheme.System&&systemDark);
        BackColor=dark?Color.FromArgb(28,29,32):Color.White;ForeColor=dark?Color.Gainsboro:Color.FromArgb(41,52,68);browser.DefaultBackgroundColor=BackColor;
        if(IsHandleCreated){int enabled=dark?1:0;Native.DwmSetWindowAttribute(Handle,20,ref enabled,sizeof(int));}
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ApplyFrameTheme();}
    private void Post(object message){if(IsDisposed||!IsReady||browser.CoreWebView2==null)return;try{browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));}catch(InvalidOperationException){}}
    internal void SelectPage(int index){page=Math.Clamp(index,0,3);Post(new{type="page",value=page});}
    internal void RefreshValues(){SendSnapshot();}
    private void SendSnapshot()
    {
        bool startup=false,available=true;try{startup=readStartup();}catch{available=false;}
        Post(new{type="snapshot",value=new{settings.SettingsTheme,settings.OpeningMode,settings.FollowNativeOverflow,settings.HoverToOpen,settings.ExpansionOrder,settings.Columns,settings.Placement,settings.Appearance,settings.MatchPanelMaterials,settings.BlankCellOpens,settings.ShortcutBothPanels,settings.AutoCheckUpdates,settings.ImportantUpdateReminders,Update=ReadUpdate?.Invoke(),Shortcut=(int)settings.Shortcut,ShortcutDisplay=GlobalShortcut.Display(settings.Shortcut),StartupEnabled=startup,StartupAvailable=available,Version=Application.ProductVersion.Split('+')[0]}});
    }
    internal void ShowOperationStatus(string text,bool error=false){pendingStatus=text;pendingError=error;Post(new{type="status",text,error});}
    private static int Integer(JsonElement value,int min,int max){if(!value.TryGetInt32(out int number)||number<min||number>max)throw new ArgumentException("设置值无效。");return number;}
    private static bool Boolean(JsonElement value)=>value.ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,_=>throw new ArgumentException("设置值无效。")};
    private void ApplyShortcut(Keys key,bool both){if(!GlobalShortcut.Valid(key))throw new ShortcutException("请选择有效的组合键。");if(ChangeShortcut==null)throw new ShortcutException("快捷键服务尚未就绪。");ChangeShortcut(key,both);}
    private async void OnMessage(string source,string json)
    {
        if(IsDisposed||source!="about:blank")return;
        try
        {
            using var document=JsonDocument.Parse(json);var root=document.RootElement;string? op=root.GetProperty("op").GetString();var value=root.GetProperty("value");
            if(op=="ready"){IsReady=true;loading.Hide();SendSnapshot();SelectPage(page);if(pendingStatus!=null)ShowOperationStatus(pendingStatus,pendingError);return;}
            if(op=="themeApplied"){ApplyFrameTheme(Boolean(value));return;}
            if(op=="recordShortcut"){RecordingShortcut=Boolean(value);return;}
            if(operationInProgress)return;operationInProgress=true;
            try
            {
                bool hint=false;
                switch(op)
                {
                    case "SettingsTheme":settings.ChangeSettingsTheme((SettingsTheme)Integer(value,0,2));SetBrowserTheme();ApplyFrameTheme();break;
                    case "OpeningMode":settings.ChangeOpeningMode((OpeningMode)Integer(value,0,1));break;
                    case "ExpansionMode":int mode=Integer(value,0,2);if(mode==1&&settings.OpeningMode!=OpeningMode.Linked)throw new ArgumentException("独立模式不跟随系统隐藏区。");settings.ChangeBehavior(mode==1,mode==2,settings.MatchPanelMaterials);break;
                    case "NativeExpansionOrder":settings.ChangeExpansionOrder((NativeExpansionOrder)Integer(value,0,1));break;
                    case "Columns":int oldColumns=settings.Columns;settings.Columns=Integer(value,5,8);try{settings.Save();}catch{settings.Columns=oldColumns;throw;}break;
                    case "Placement":if(settings.OpeningMode!=OpeningMode.Linked)throw new ArgumentException("独立模式固定在入口上方。");settings.ChangePlacement((PopupPlacement)Integer(value,0,1));break;
                    case "Appearance":settings.ChangeAppearance((PanelAppearance)Integer(value,0,3));break;
                    case "Shortcut":ApplyShortcut((Keys)Integer(value,0,int.MaxValue),settings.ShortcutBothPanels);break;
                    case "ShortcutTarget":ApplyShortcut(settings.Shortcut,!Boolean(value));break;
                    case "StartupEnabled":writeStartup(Boolean(value));break;
                    case "AutoCheckUpdates":settings.ChangeUpdatePreference(Boolean(value),settings.ImportantUpdateReminders);UpdateRequested?.Invoke("preferences");break;
                    case "ImportantUpdateReminders":settings.ChangeUpdatePreference(settings.AutoCheckUpdates,Boolean(value));UpdateRequested?.Invoke("preferences");break;
                    case "checkUpdate":case "downloadUpdate":case "installUpdate":case "cancelUpdate":case "releasePage":case "uninstall":UpdateRequested?.Invoke(op);return;
                    case "BlankCellOpens":var previous=(settings.BlankCellOpens,settings.BlankCellHintSeen,settings.BlankCellHintVersion);settings.BlankCellOpens=Boolean(value);hint=settings.BlankCellOpens&&(!settings.BlankCellHintSeen||settings.BlankCellHintVersion<2);if(hint){settings.BlankCellHintSeen=true;settings.BlankCellHintVersion=2;}try{settings.Save();}catch{(settings.BlankCellOpens,settings.BlankCellHintSeen,settings.BlankCellHintVersion)=previous;throw;}break;
                    case "MatchPanelMaterials":if(ChangeMaterialSync==null)throw new NativeMaterialException("材质组件尚未准备好。");ShowOperationStatus("正在应用主面板材质…");await ChangeMaterialSync(Boolean(value));break;
                    case "openPanel":OpenPanelRequested?.Invoke();return;
                    case "refreshIcons":RefreshIconsRequested?.Invoke();ShowOperationStatus("图标已刷新");return;
                    case "restoreAll":RestoreAllRequested?.Invoke();SendSnapshot();return;
                    case "blankHint":BlankCellHint.ShowFor(this);return;
                    case "homepage":Process.Start(new ProcessStartInfo("https://github.com/Wuyuhang0729/traynest"){UseShellExecute=true});return;
                    case "diagnostics":OpenDiagnostics?.Invoke();return;
                    case "exit":ExitRequested?.Invoke();return;
                    default:throw new ArgumentException("不支持的设置操作。");
                }
                if(op!="SettingsTheme")changed();SendSnapshot();ShowOperationStatus("已保存");if(hint)BlankCellHint.ShowFor(this);
            }
            catch(Exception error){AppDiagnostics.Error(error is TrayNestException known?known.Code:"TN4001",error);SendSnapshot();ShowOperationStatus(error is ShortcutException or NativeMaterialException?error.Message:"未能保存，已保留原设置。",true);}
            finally{operationInProgress=false;}
        }
        catch(Exception error)when(error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException){AppDiagnostics.Warn("TN4001",error:error);SendSnapshot();ShowOperationStatus("未能应用这项设置，请重试。",true);}
    }
    protected override void Dispose(bool disposing){if(disposing){RecordingShortcut=false;IsReady=false;browser.Dispose();}base.Dispose(disposing);}
}
