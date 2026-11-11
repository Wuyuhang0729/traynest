using System.IO.Pipes;
using System.Text.Json;
using TrayNest.Installation;

namespace NestedTray;

internal sealed class MaintenanceEndpoint : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Control dispatch = new();
    private readonly Action exit;
    internal MaintenanceEndpoint(Action exit) { this.exit = exit; _ = dispatch.Handle; }
    internal void Start() => _ = ServeAsync();
    private async Task ServeAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream("TrayNest.Maintenance." + Environment.ProcessId, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                using var clientTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); clientTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var reader = new StreamReader(pipe, leaveOpen: true); using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                string request = await reader.ReadLineAsync(clientTimeout.Token) ?? "";
                if (request.Length > 4096) continue;
                using var json = JsonDocument.Parse(request);
                if (json.RootElement.GetProperty("action").GetString() != "exit" || !string.Equals(json.RootElement.GetProperty("path").GetString(), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) continue;
                await writer.WriteLineAsync("stopping".AsMemory(), clientTimeout.Token);
                if (!dispatch.IsDisposed) dispatch.BeginInvoke(exit);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!lifetime.IsCancellationRequested) AppDiagnostics.Warn("TN4001", error: error); }
        }
    }
    internal static async void SignalReady(string[] args)
    {
        int at = Array.IndexOf(args, "--maintenance-handshake");
        if (at < 0 || at + 1 >= args.Length || !Guid.TryParseExact(args[at + 1], "N", out _)) return;
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var pipe = new NamedPipeClientStream(".", "TrayNest.Ready." + args[at + 1], PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(cancel.Token);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { Token = args[at + 1], Process = Environment.ProcessId, Path = AppContext.BaseDirectory.TrimEnd('\\'), Hash = Package.Hash(Path.Combine(AppContext.BaseDirectory, "NestedTray.dll")) }));
        }
        catch (Exception error) { AppDiagnostics.Warn("TN4001", error: error); }
    }
    public void Dispose() { lifetime.Cancel(); dispatch.Dispose(); lifetime.Dispose(); }
}
