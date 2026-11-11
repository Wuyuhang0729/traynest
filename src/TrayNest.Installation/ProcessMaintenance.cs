using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace TrayNest.Installation;

public static class ProcessMaintenance
{
    public static async Task StopAsync(string target, string profile, CancellationToken token)
    {
        string executable = Path.Combine(Path.GetFullPath(target), "NestedTray.exe");
        var processes = Process.GetProcessesByName("NestedTray");
        var observed=new HashSet<int>();
        foreach (var process in processes)
        {
            using (process)
            {
                if (process.HasExited || !string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) continue;
                observed.Add(process.Id);
                // A recovery watcher shares the executable, but deliberately has no command endpoint.
                bool watcher = File.Exists(Path.Combine(profile, "recovery-" + process.Id + ".json")) == false;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await using var pipe = new NamedPipeClientStream(".", "TrayNest.Maintenance." + process.Id, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.ConnectAsync(timeout.Token);
                    using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true }; using var reader = new StreamReader(pipe, leaveOpen: true);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { action = "exit", path = executable }).AsMemory(), timeout.Token);
                    if (await reader.ReadLineAsync(timeout.Token) != "stopping") throw new IOException("程序未确认正常退出请求。");
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested && watcher) { continue; }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("此运行版本尚未提供更新退出入口，请从 TrayNest 设置正常退出后重试。"); }
            }
        }
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(token); wait.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            bool running = false;
            foreach (var process in Process.GetProcessesByName("NestedTray"))
                using (process) if (!process.HasExited && string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) running = true;
            bool recovery = observed.Any(id=>File.Exists(Path.Combine(profile,"recovery-"+id+".json")));
            if (!running && !recovery) return;
            try { await Task.Delay(150, wait.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("程序或图标恢复尚未结束，未替换文件。请正常退出后重试。"); }
        }
    }
    public static async Task StartAndVerifyAsync(string target, CancellationToken token, string? profile=null)
    {
        string nonce = Guid.NewGuid().ToString("N");
        string expected = Package.Hash(Path.Combine(target, "NestedTray.dll"));
        await using var pipe = new NamedPipeServerStream("TrayNest.Ready." + nonce, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo(Path.Combine(target, "NestedTray.exe")) { UseShellExecute = false, WorkingDirectory = target, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--maintenance-handshake"); start.ArgumentList.Add(nonce);
        if(profile is not null)start.Environment["NESTEDTRAY_TEST_PROFILE"]=profile;
        using var child = Process.Start(start) ?? throw new IOException("未能启动新版本。");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await pipe.WaitForConnectionAsync(timeout.Token);
            using var reader = new StreamReader(pipe); var line = await reader.ReadLineAsync(timeout.Token);
            if (line is null || line.Length > 4096) throw new IOException("新版启动确认无效。");
            using var json = JsonDocument.Parse(line); var root = json.RootElement;
            if (root.GetProperty("Token").GetString() != nonce || root.GetProperty("Process").GetInt32() != child.Id || !string.Equals(root.GetProperty("Path").GetString(), Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || root.GetProperty("Hash").GetString() != expected) throw new IOException("新版启动路径或文件摘要不一致。");
            await Task.Delay(750, token); child.Refresh();
            if (child.HasExited) throw new IOException("新版本在启动确认后退出。");
        }
        catch (Exception original)
        {
            try { if (!child.HasExited) await StopAsync(target, profile??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestedTray"), CancellationToken.None); }
            catch (Exception cleanup) { throw new IOException("新版未通过启动确认且未能正常退出，保留备份等待恢复。", new AggregateException(original, cleanup)); }
            throw new IOException("新版未通过启动确认，正在恢复原版。", original);
        }
    }
}
