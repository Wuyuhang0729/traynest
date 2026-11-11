using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using TrayNest.Installation;

namespace NestedTray;

internal sealed record UpdateView(string Status, bool Busy, bool CanDownload, bool CanInstall, string? Version, string? Summary, string? Page, bool Important, string? Problem);
internal sealed class UpdateController : IDisposable
{
    private sealed record CheckState(DateTimeOffset LastAttempt, string? Notified);
    private readonly Settings settings;
    private readonly ReleaseClient client = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30000 };
    private CancellationTokenSource? operation;
    private readonly string statePath = Path.Combine(Settings.DirectoryPath, "update-state.json");
    private CheckState state = new(DateTimeOffset.MinValue, null);
    private ReleaseCandidate? candidate;
    private string? prepared;
    private bool disposed;
    internal event Action? Changed;
    internal Action<string>? Notify;
    internal UpdateView View { get; private set; } = new("尚未检查更新", false, false, false, null, null, null, false, null);
    internal UpdateController(Settings settings)
    {
        this.settings = settings;
        try { if (File.Exists(statePath)) state = JsonSerializer.Deserialize<CheckState>(File.ReadAllText(statePath)) ?? state; } catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        timer.Tick += async (_, _) => { if (settings.AutoCheckUpdates && operation is null && DateTimeOffset.UtcNow - state.LastAttempt >= TimeSpan.FromDays(1)) await CheckAsync(false); };
        timer.Start();
    }
    private void Persist() { Directory.CreateDirectory(Settings.DirectoryPath); File.WriteAllText(statePath + ".tmp", JsonSerializer.Serialize(state)); File.Move(statePath + ".tmp", statePath, true); }
    private void Set(string status, bool busy = false)
    {
        if (disposed) return;
        View = new(status, busy, !busy && candidate?.PackageUrl is not null && prepared is null, !busy && prepared is not null, candidate?.Version, candidate?.Summary, candidate?.Page, candidate?.Important ?? false, candidate?.Problem);
        Changed?.Invoke();
    }
    internal void PreferencesChanged()
    {
        if (!settings.AutoCheckUpdates && automaticOperation) operation?.Cancel();
        Changed?.Invoke();
    }
    private bool automaticOperation;
    internal async Task CheckAsync(bool manual)
    {
        if (operation is not null || disposed) return;
        automaticOperation = !manual;
        using var cancel = new CancellationTokenSource(); operation = cancel;
        try
        {
            Set("正在检查更新…", true);
            if (!manual && !settings.AutoCheckUpdates) return;
            state = state with { LastAttempt = DateTimeOffset.UtcNow }; Persist();
            var result = await client.CheckAsync(Application.ProductVersion.Split('+')[0], cancel.Token);
            if (candidate?.Version != result?.Version) prepared = null;
            candidate = result;
            Set(candidate is null ? "当前没有较新版本" : "发现新版本 " + candidate.Version);
            if (!manual && settings.AutoCheckUpdates && settings.ImportantUpdateReminders && candidate is { Important: true } && state.Notified != candidate.Version)
            {
                // Persist before notifying: a restart never repeats an already shown version.
                state = state with { Notified = candidate.Version }; Persist(); Notify?.Invoke(candidate.Version);
            }
        }
        catch (OperationCanceledException) { Set(cancel.IsCancellationRequested ? "检查已取消" : "检查更新超时，请重试"); }
        catch (HttpRequestException) { Set("无法连接更新服务，请检查网络后重试"); }
        catch (Exception error) { Set(error is IOException ? error.Message : "更新响应无法识别，请稍后重试"); AppDiagnostics.Warn("TN4001", error: error); }
        finally { operation = null; automaticOperation = false; if (!disposed && View.Busy) Set("检查已结束"); }
    }
    internal async Task DownloadAsync()
    {
        if (operation is not null || disposed || candidate?.PackageUrl is null) return;
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(30)); operation = cancel;
        string work = Path.Combine(Settings.DirectoryPath, "updates", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work); Set("正在下载更新…", true);
            var progress = new Progress<int>(percent => {if(operation==cancel&&prepared is null)Set($"正在下载更新… {percent}%", true);});
            prepared=null;
            await client.DownloadAsync(candidate, Path.Combine(work, "TrayNest.Setup.exe"), progress, cancel.Token);
            prepared=Path.Combine(work,"TrayNest.Setup.exe");
            if(ReleaseVersion.Parse(FileVersionInfo.GetVersionInfo(prepared).ProductVersion?.Trim()??"").CompareTo(ReleaseVersion.Parse(candidate.Version))!=0)throw new IOException("安装程序版本与所选更新不一致。");
            Set("下载与校验完成，可退出并更新");
        }
        catch (OperationCanceledException) { prepared = null; Set("下载已取消或超时，原程序未改动"); }
        catch (Exception error) { prepared = null; Set(error is IOException ? error.Message : "下载未完成，请重试"); AppDiagnostics.Warn("TN4001", error: error); }
        finally { operation = null; }
    }
    internal void Cancel() => operation?.Cancel();
    internal void Install()
    {
        if (prepared is null || operation is not null) return;
        if(candidate?.SHA256 is null||!Package.Hash(prepared).Equals(candidate.SHA256,StringComparison.OrdinalIgnoreCase))throw new IOException("安装程序校验发生变化，请重新下载。");
        LaunchMaintenance("--install", prepared);
    }
    internal static void LaunchMaintenance(string operation,string installer)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "TrayNest.Maintenance.exe");
        if (!File.Exists(source)) throw new IOException("安装维护组件缺失，请重新取得完整安装包。");
        string runner = Path.Combine(Settings.DirectoryPath, "maintenance", "runners", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(runner);
        string executable = Path.Combine(runner, "TrayNest.Maintenance.exe"); File.Copy(source, executable);
        if (Package.Hash(source) != Package.Hash(executable)) throw new IOException("安装维护组件校验失败。");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = runner, WindowStyle = ProcessWindowStyle.Normal };
        start.ArgumentList.Add(operation);start.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd('\\'));start.ArgumentList.Add(Settings.DirectoryPath);start.ArgumentList.Add(installer);
        if (Process.Start(start) is null) throw new IOException("未能打开安装向导。");
    }
    internal static void LaunchUninstaller()
    {
        string uninstaller=Path.Combine(AppContext.BaseDirectory,"unins000.exe");
        if(!File.Exists(uninstaller))throw new IOException("当前目录未登记安装卸载入口。便携版请正常退出后移除程序目录，配置默认保留。");
        LaunchMaintenance("--uninstall",uninstaller);
    }
    public void Dispose() { disposed = true; timer.Stop(); timer.Dispose(); operation?.Cancel(); client.Dispose(); }
}
