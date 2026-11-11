using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace TrayNest.Installation;

public sealed record PackageFile(string Path, long Size, string SHA256);
public sealed record PackageManifest(int Schema, string Version, string Platform, PackageFile[] Files);

public static class Package
{
    public const string ManifestName = "traynest-package.json";
    public static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    public static string Resolve(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.StartsWith('/') || relative.Split('/').Any(x => x is "." or ".." or "" || x.EndsWith(' ') || x.EndsWith('.') || x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) throw new IOException("安装包包含不安全的文件路径。");
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包文件路径越界。");
        return result;
    }
    public static PackageManifest Verify(string directory, string? expectedVersion = null,bool installed=false,IReadOnlyDictionary<string,string>? preserved=null)
    {
        directory = Path.GetFullPath(directory);
        RejectReparse(directory);
        string manifestPath = Path.Combine(directory, ManifestName);
        if (new FileInfo(manifestPath).Length > 8 * 1024 * 1024) throw new IOException("安装文件清单过大。");
        var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(manifestPath)) ?? throw new IOException("安装文件清单缺失。");
        if (manifest.Schema != 1 || manifest.Platform != "win-x64" || manifest.Files is null || manifest.Files.Length is 0 or > 12000) throw new IOException("安装文件清单不受支持。");
        ReleaseVersion.Parse(manifest.Version);
        if (expectedVersion is not null && manifest.Version != expectedVersion) throw new IOException("安装包版本与所选更新不一致。");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var item in manifest.Files)
        {
            if (item is null || item.Size < 0 || !ReleaseClient.ValidHash(item.SHA256) || !files.Add(item.Path) || item.Path.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装文件清单无效或重复。");
            string path = Resolve(directory, item.Path); RejectAncestors(directory, path);
            var file = new FileInfo(path); total += item.Size;
            if (total > 3L * 1024 * 1024 * 1024 || !file.Exists || file.Length != item.Size || !Hash(path).Equals(item.SHA256, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装文件校验失败：" + item.Path);
        }
        foreach (string required in new[] { "NestedTray.exe", "NestedTray.dll", "NestedTray.runtimeconfig.json", "NestedTray.deps.json", "TrayNest.ico", "coreclr.dll", "hostfxr.dll", "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.WinForms.dll", "native/bridge.json", "LICENSE" })
            if (!files.Contains(required)) throw new IOException("安装包缺少必需文件：" + required);
        if(!files.Contains("WebView2Loader.dll")&&!files.Contains("runtimes/win-x64/native/WebView2Loader.dll"))throw new IOException("安装包缺少 WebView2Loader。");
        if (!files.Any(x => x.StartsWith("licenses/", StringComparison.OrdinalIgnoreCase))) throw new IOException("安装包缺少许可证。");
        foreach (string path in EnumerateFiles(directory))
        {
            string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            if (!relative.Equals(ManifestName, StringComparison.OrdinalIgnoreCase) && !files.Contains(relative)&&!(installed&&System.Text.RegularExpressions.Regex.IsMatch(relative,@"^unins\d{3}\.(exe|dat|msg)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))&&!(installed&&preserved is not null&&preserved.TryGetValue(relative,out var prior)&&Hash(path)==prior)) throw new IOException("安装包包含未登记文件：" + relative);
        }
        return manifest;
    }
    public static IEnumerable<string> EnumerateFiles(string directory)
    {
        RejectReparse(directory);
        foreach (string file in Directory.EnumerateFiles(directory)) { RejectReparse(file); yield return file; }
        foreach (string child in Directory.EnumerateDirectories(directory)) foreach (string file in EnumerateFiles(child)) yield return file;
    }
    public static void RejectReparse(string path)
    { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("安装路径不能包含链接或重解析点。"); }
    private static void RejectAncestors(string root, string path)
    {
        RejectReparse(path);
        for (string? parent = Path.GetDirectoryName(path); parent is not null && !parent.Equals(root, StringComparison.OrdinalIgnoreCase); parent = Path.GetDirectoryName(parent)) RejectReparse(parent);
    }
    public static string Extract(string archive, string destination, string expectedVersion)
    {
        if (Directory.Exists(destination)) throw new IOException("解压暂存目录已存在。");
        Directory.CreateDirectory(destination);
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count is 0 or > 16000) throw new IOException("安装包文件数量无效。");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var entry in zip.Entries)
        {
            bool directory = entry.FullName.EndsWith('/');
            var name = directory ? entry.FullName.TrimEnd('/') : entry.FullName;
            string path = Resolve(destination, name);
            if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new IOException("安装包包含重复路径或链接。");
            total += entry.Length;
            if (entry.Length > 1024L * 1024 * 1024 || total > 3L * 1024 * 1024 * 1024) throw new IOException("安装包展开大小超出限制。");
            if (directory) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = entry.Open(); using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            var buffer=new byte[65536];long written=0;
            while(true){int count=source.Read(buffer);if(count==0)break;written+=count;if(written>entry.Length)throw new IOException("解压文件超过声明大小。");target.Write(buffer,0,count);}
            if (target.Length != entry.Length) throw new IOException("解压文件大小不符。");
        }
        var candidates = Directory.EnumerateFiles(destination, ManifestName, SearchOption.AllDirectories).ToArray();
        if (candidates.Length != 1) throw new IOException("安装包缺少唯一的文件清单。");
        string root = Path.GetDirectoryName(candidates[0])!;
        foreach (var file in EnumerateFiles(destination)) if (!file.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包包含程序目录外的文件。");
        Verify(root, expectedVersion); return root;
    }
    public static void CopyVerified(string source, string target)
    {
        var manifest = Verify(source);
        if (Directory.Exists(target)) throw new IOException("安装暂存目录已存在。");
        Directory.CreateDirectory(target);
        foreach (var file in manifest.Files)
        {
            string destination = Resolve(target, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Resolve(source, file.Path), destination);
        }
        File.Copy(Path.Combine(source, ManifestName), Path.Combine(target, ManifestName));
        Verify(target, manifest.Version);
    }
}
