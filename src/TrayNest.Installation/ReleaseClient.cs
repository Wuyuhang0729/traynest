using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TrayNest.Installation;

public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? Preview) : IComparable<ReleaseVersion>
{
    public static ReleaseVersion Parse(string text)
    {
        var match = Regex.Match(text, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
        if (!match.Success) throw new FormatException("版本号格式无效。");
        var preview = match.Groups[4].Success ? match.Groups[4].Value : null;
        if (preview?.Split('.').Any(x => x.Length > 1 && x[0] == '0' && x.All(char.IsAsciiDigit)) == true) throw new FormatException("预览版本号格式无效。");
        return new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), preview);
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        foreach (var pair in new[] { (Major, other.Major), (Minor, other.Minor), (Patch, other.Patch) })
            if (pair.Item1.CompareTo(pair.Item2) is int comparison && comparison != 0) return comparison;
        if (Preview is null || other.Preview is null) return Preview == other.Preview ? 0 : Preview is null ? 1 : -1;
        var left = Preview.Split('.'); var right = other.Preview.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool a = left[i].All(char.IsAsciiDigit), b = right[i].All(char.IsAsciiDigit);
            int comparison = a && b ? left[i].Length.CompareTo(right[i].Length) : a != b ? a ? -1 : 1 : 0;
            if (comparison == 0) comparison = string.CompareOrdinal(left[i], right[i]);
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
}

public sealed record ReleaseCandidate(string Version, string Summary, string Page, bool Important, string? PackageUrl, long Size, string? SHA256, string? Problem);

public sealed class ReleaseClient : IDisposable
{
    public const string Repository = "https://github.com/Wuyuhang0729/traynest";
    public const string Api = "https://api.github.com/repos/Wuyuhang0729/traynest/releases";
    private readonly HttpClient http;
    public ReleaseClient(HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
        http.Timeout = Timeout.InfiniteTimeSpan;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TrayNest-Update/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
    }
    public async Task<ReleaseCandidate?> CheckAsync(string current, CancellationToken cancellationToken)
    {
        var version = ReleaseVersion.Parse(current);
        var found = new List<ReleaseCandidate>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        for (int page = 1; page <= 5; page++)
        {
            using var response = await RequestAsync(new Uri(Api + $"?per_page=100&page={page}"), timeout.Token);
            using var json = JsonDocument.Parse(await ReadLimitedAsync(response.Content, 4 * 1024 * 1024, timeout.Token));
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new IOException("更新服务返回了无法识别的数据。");
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean()) continue;
                var tag = release.GetProperty("tag_name").GetString() ?? "";
                ReleaseVersion remote;
                try { remote = ReleaseVersion.Parse(tag); } catch (Exception e) when (e is FormatException or OverflowException) { continue; }
                if (remote.CompareTo(version) <= 0 || version.Preview is null && (remote.Preview is not null || release.GetProperty("prerelease").GetBoolean())) continue;
                var body = release.TryGetProperty("body", out var value) ? value.GetString() ?? "" : "";
                var markers = Regex.Matches(body, @"<!--\s*traynest-update:(.*?)-->", RegexOptions.Singleline | RegexOptions.CultureInvariant);
                bool important = false; string? problem = null;
                if (markers.Count > 0)
                {
                    try
                    {
                        if (markers.Count != 1) throw new FormatException();
                        using var marker = JsonDocument.Parse(markers[0].Groups[1].Value);
                        if (marker.RootElement.GetProperty("schema").GetInt32() != 1) throw new FormatException();
                        string? importance = marker.RootElement.GetProperty("importance").GetString();
                        if (importance is not ("important" or "normal")) throw new FormatException();
                        important = importance == "important";
                    }
                    catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { problem = "更新标记无法确认，请查看版本说明。"; }
                }
                string expectedName=$"TrayNest-Setup-v{tag.TrimStart('v')}-win-x64.exe";
                var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == expectedName).ToArray();
                string? package = null, hash = null; long size = 0;
                if (assets.Length == 1)
                {
                    var asset = assets[0]; package = asset.GetProperty("browser_download_url").GetString(); size = asset.GetProperty("size").GetInt64();
                    string? digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
                    if (digest?.StartsWith("sha256:", StringComparison.Ordinal) == true && ValidHash(digest[7..])) hash = digest[7..].ToLowerInvariant();
                    var sidecars = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == expectedName+".sha256").ToArray();
                    if (sidecars.Length == 1)
                    {
                        var sidecarUrl = sidecars[0].GetProperty("browser_download_url").GetString()!;
                        EnsurePackageUrl(sidecarUrl);
                        using var sidecar = await RequestAsync(new Uri(sidecarUrl), timeout.Token);
                        var text = System.Text.Encoding.ASCII.GetString(await ReadLimitedAsync(sidecar.Content, 4096, timeout.Token)).Trim();
                        var expected = Regex.Match(text, @"^([a-fA-F0-9]{64})\s+\*?(\S+)\s*$");
                        if (!expected.Success || expected.Groups[2].Value != asset.GetProperty("name").GetString()) throw new IOException("发布包校验信息格式无效。");
                        var companion = expected.Groups[1].Value.ToLowerInvariant();
                        if (hash is not null && hash != companion) throw new IOException("发布包的两份校验信息不一致。");
                        hash = companion;
                    }
                    try { EnsurePackageUrl(package!); } catch (IOException) { package = null; }
                }
                if (package is null || hash is null || size <= 0 || size > 1024L * 1024 * 1024) { package = null; problem = "此版本缺少可验证的完整安装包，请打开发布页面。"; }
                var summary = Regex.Replace(body, @"<!--[\s\S]*?-->", "");
                summary = Regex.Replace(summary, @"<[^>]*>", "").Trim();
                if (summary.Length > 1200) summary = summary[..1200] + "…";
                found.Add(new(tag.TrimStart('v'), summary, Repository + "/releases/tag/" + Uri.EscapeDataString(tag), important, package, size, hash, problem));
            }
            if (json.RootElement.GetArrayLength() < 100) return found.OrderByDescending(x => ReleaseVersion.Parse(x.Version)).FirstOrDefault();
        }
        throw new IOException("版本列表超出检查范围，尚未完成检查，请打开项目发布页面。");
    }
    public async Task DownloadAsync(ReleaseCandidate candidate, string destination, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        if (candidate.PackageUrl is null || candidate.SHA256 is null || !ValidHash(candidate.SHA256)) throw new IOException("没有可验证的安装包。");
        EnsurePackageUrl(candidate.PackageUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string temporary = destination + ".partial";
        try
        {
            using var response = await RequestAsync(new Uri(candidate.PackageUrl), cancellationToken);
            if (response.Content.Headers.ContentLength is long length && length != candidate.Size) throw new IOException("下载大小与发布信息不符。");
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                var bytes = new byte[65536]; long total = 0;
                while (true)
                {
                    using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); stall.CancelAfter(TimeSpan.FromSeconds(30));
                    int count = await input.ReadAsync(bytes, stall.Token); if (count == 0) break;
                    total += count; if (total > candidate.Size) throw new IOException("下载内容超过发布大小。");
                    digest.AppendData(bytes, 0, count); await output.WriteAsync(bytes.AsMemory(0, count), cancellationToken);
                    progress?.Report((int)(total * 100 / candidate.Size));
                }
                if (total != candidate.Size || !Convert.ToHexString(digest.GetHashAndReset()).Equals(candidate.SHA256, StringComparison.OrdinalIgnoreCase)) throw new IOException("下载校验失败，原程序未改动。");
            }
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private async Task<HttpResponseMessage> RequestAsync(Uri uri, CancellationToken token)
    {
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            if (uri.Scheme != "https" || !AllowedHost(uri.Host)) throw new IOException("更新地址不属于受支持的项目下载来源。");
            using var headersTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            headersTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, headersTimeout.Token);
            if ((int)response.StatusCode is >= 300 and <= 399 && response.StatusCode != HttpStatusCode.NotModified)
            {
                var next = response.Headers.Location; response.Dispose();
                if (next is null) throw new IOException("下载服务未提供有效跳转地址。");
                uri = next.IsAbsoluteUri ? next : new Uri(uri, next); continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                bool limited = (int)response.StatusCode is 403 or 429;
                string retry = response.Headers.RetryAfter?.ToString() ?? (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) ? string.Join("", reset) : "");
                response.Dispose(); throw new IOException(limited ? "更新服务暂时限制请求，请稍后再试。" + (retry.Length > 0 ? "（重试信息：" + retry + "）" : "") : "更新服务暂不可用，请稍后重试。");
            }
            return response;
        }
        throw new IOException("下载服务跳转次数过多。");
    }
    public static async Task<byte[]> ReadLimitedAsync(HttpContent content, int limit, CancellationToken token)
    {
        await using var input = await content.ReadAsStreamAsync(token); using var output = new MemoryStream(); var bytes = new byte[16384];
        while (true) { int count = await input.ReadAsync(bytes, token); if (count == 0) return output.ToArray(); if (output.Length + count > limit) throw new IOException("更新响应过大，已停止读取。"); output.Write(bytes, 0, count); }
    }
    public static bool ValidHash(string value) => Regex.IsMatch(value, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant);
    private static bool AllowedHost(string host) => host is "api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
    private static void EnsurePackageUrl(string value)
    { if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith("/Wuyuhang0729/traynest/releases/download/", StringComparison.Ordinal) || url.UserInfo.Length > 0) throw new IOException("安装包地址不属于项目发布资产。"); }
    public void Dispose() => http.Dispose();
}
