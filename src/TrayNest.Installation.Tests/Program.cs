using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using TrayNest.Installation;

if(args.Length==3&&args[0]=="--live")
{
    using var live=new ReleaseClient();
    try{var found=await live.CheckAsync(args[1],CancellationToken.None);File.WriteAllText(args[2],JsonSerializer.Serialize(new{Current=args[1],Candidate=found,NoNewerVersion=found is null}));Console.WriteLine(found is null?"No newer published version":"New version: "+found.Version);return 0;}
    catch(Exception error){Console.WriteLine(error.Message);return 1;}
}
var root=Path.GetFullPath(args.Length>0?args[0]:throw new ArgumentException("Provide an isolated test directory"));Directory.CreateDirectory(root);
var checks=new Dictionary<string,bool>();var errors=new List<string>();
void Check(string name,bool condition){checks[name]=condition;if(!condition)errors.Add(name);}
async Task Reject(string name,Func<Task> action){try{await action();Check(name,false);}catch(Exception e)when(e is IOException or InvalidDataException or FormatException or OperationCanceledException or JsonException){Check(name,true);}}
try
{
    Check("PreviewNumericOrder",ReleaseVersion.Parse("0.2.0-preview.10").CompareTo(ReleaseVersion.Parse("0.2.0-preview.9"))>0);
    Check("StableAfterPreview",ReleaseVersion.Parse("0.2.0").CompareTo(ReleaseVersion.Parse("0.2.0-preview.99"))>0);
    Check("BuildMetadataIgnored",ReleaseVersion.Parse("v0.2.0+abc").CompareTo(ReleaseVersion.Parse("0.2.0+xyz"))==0);
    Check("NumericBeforeText",ReleaseVersion.Parse("0.2.0-2").CompareTo(ReleaseVersion.Parse("0.2.0-alpha"))<0);
    await Reject("InvalidVersionRejected",()=>Task.Run(()=>ReleaseVersion.Parse("0.2.0-preview.01")));
    string hash=new('a',64);
    object Release(string version,string body="",string? digest=null,bool draft=false,bool prerelease=true)=>new {tag_name="v"+version,body,draft,prerelease,assets=new[]{new{name="TrayNest-Setup-v"+version+"-win-x64.exe",size=16,digest=digest??"sha256:"+hash,browser_download_url=ReleaseClient.Repository+"/releases/download/v"+version+"/TrayNest-Setup-v"+version+"-win-x64.exe"}}};
    async Task<ReleaseCandidate?> Read(object[] values,string current="0.2.0-preview.16")
    {using var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(values))})));return await client.CheckAsync(current,CancellationToken.None);}
    Check("NoUpdates",await Read([]) is null);
    Check("NoDowngrade",await Read([Release("0.2.0-preview.13")]) is null);
    var normal=await Read([Release("0.2.0-preview.17","Ordinary notes")]);
    Check("OrdinaryUpdate",normal is{Important:false,Version:"0.2.0-preview.17",PackageUrl:not null});
    var important=await Read([Release("0.2.0-preview.17","<!-- traynest-update:{\"schema\":1,\"importance\":\"important\"} -->\nFix")]);
    Check("ExplicitImportantMarker",important is{Important:true}&&important.Summary=="Fix");
    var malformed=await Read([Release("0.2.0-preview.17","<!-- traynest-update:broken -->")]);
    Check("MalformedMarkerNotImportant",malformed is{Important:false,Problem:not null});
    Check("DraftExcluded",await Read([Release("0.2.0-preview.17",draft:true)]) is null);
    Check("StableChannelExcludesPreviews",await Read([Release("0.3.0-preview.1")],"0.2.0") is null);
    Check("SelectHighestVersion",(await Read([Release("0.2.0-preview.18"),Release("0.2.0-preview.17")]))?.Version=="0.2.0-preview.18");
    var missing=await Read([Release("0.2.0-preview.17",digest:"")]);Check("NoDigestNoInstall",missing is{PackageUrl:null,Problem:not null});
    using(var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)))))await Reject("RateLimitIsNotNoUpdate",async()=>await client.CheckAsync("0.2.0",CancellationToken.None));
    using(var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")))))
    {try{await client.CheckAsync("0.2.0",CancellationToken.None);Check("OfflineVisible",false);}catch(HttpRequestException){Check("OfflineVisible",true);}}
    using(var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("not json")}))))await Reject("BadJsonRejected",async()=>await client.CheckAsync("0.2.0",CancellationToken.None));
    using(var client=new ReleaseClient(new FakeHandler(async(_,token)=>{await Task.Delay(10000,token);return new HttpResponseMessage(HttpStatusCode.OK);})))
    {using var cancel=new CancellationTokenSource(40);await Reject("CheckCanCancel",async()=>await client.CheckAsync("0.2.0",cancel.Token));}
    using(var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://example.com/asset")}}))))await Reject("UntrustedRedirectRejected",async()=>await client.CheckAsync("0.2.0",CancellationToken.None));
    var payload=Encoding.UTF8.GetBytes("verified payload");string payloadHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    var download=new ReleaseCandidate("0.2.0-preview.17","",ReleaseClient.Repository,false,ReleaseClient.Repository+"/releases/download/v0.2.0-preview.17/TrayNest-v0.2.0-preview.17-win-x64.zip",payload.Length,payloadHash,null);
    using(var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(payload)}))))
    {string file=Path.Combine(root,"download.zip");await client.DownloadAsync(download,file,null,CancellationToken.None);Check("DownloadVerified",File.ReadAllBytes(file).SequenceEqual(payload));}
    using(var client=new ReleaseClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(payload)}))))
    {string file=Path.Combine(root,"corrupt.zip");await Reject("CorruptDownloadRejected",()=>client.DownloadAsync(download with{SHA256=hash},file,null,CancellationToken.None));Check("NoPartialCorruptDownload",!File.Exists(file)&&!File.Exists(file+".partial"));}
    string CreatePackage(string name,string version="0.2.0-preview.17")
    {
        string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);
        foreach(string relative in new[]{"NestedTray.exe","NestedTray.dll","NestedTray.runtimeconfig.json","NestedTray.deps.json","TrayNest.ico","TrayNest.Setup.exe","coreclr.dll","hostfxr.dll","Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll","native/bridge.json","licenses/runtime.txt","LICENSE"})
        {string path=Package.Resolve(folder,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,relative+" "+version);}
        WriteManifest(folder,version);return folder;
    }
    void WriteManifest(string folder,string version)
    {var files=Package.EnumerateFiles(folder).Where(p=>Path.GetFileName(p)!=Package.ManifestName).Select(p=>new PackageFile(Path.GetRelativePath(folder,p).Replace('\\','/'),new FileInfo(p).Length,Package.Hash(p))).ToArray();File.WriteAllText(Path.Combine(folder,Package.ManifestName),JsonSerializer.Serialize(new PackageManifest(1,version,"win-x64",files)));}
    string source=CreatePackage("source");Check("CompleteManifestVerified",Package.Verify(source).Files.Length==14);
    foreach(string bad in new[]{"../outside","/absolute","C:/outside","native\\bridge.json","file:stream","a//b","a/../b","a./b"})await Reject("RejectPath_"+bad,()=>Task.Run(()=>Package.Resolve(root,bad)));
    string broken=CreatePackage("broken");File.AppendAllText(Path.Combine(broken,"NestedTray.dll"),"corrupt");await Reject("DamagedPayloadRejected",()=>Task.Run(()=>Package.Verify(broken)));
    string incomplete=CreatePackage("incomplete");File.Delete(Path.Combine(incomplete,"native/bridge.json"));await Reject("MissingNativeRejected",()=>Task.Run(()=>Package.Verify(incomplete)));
    string extra=CreatePackage("extra");File.WriteAllText(Path.Combine(extra,"unexpected.exe"),"extra");await Reject("UnlistedFilesRejected",()=>Task.Run(()=>Package.Verify(extra)));
    string zip=Path.Combine(root,"package.zip");ZipFile.CreateFromDirectory(source,zip);string extracted=Package.Extract(zip,Path.Combine(root,"unzip"),"0.2.0-preview.17");Check("ZipExtractVerified",Package.Verify(extracted).Version=="0.2.0-preview.17");
    string evil=Path.Combine(root,"evil.zip");using(var zipfile=ZipFile.Open(evil,ZipArchiveMode.Create)){using var writer=new StreamWriter(zipfile.CreateEntry("../escape.txt").Open());writer.Write("escape");}
    await Reject("ZipTraversalRejected",()=>Task.Run(()=>Package.Extract(evil,Path.Combine(root,"evil-out"),"0.2.0-preview.17")));Check("NoZipEscape",!File.Exists(Path.Combine(root,"escape.txt")));

    string historicalProfile=Path.Combine(root,"historical-profile");Directory.CreateDirectory(historicalProfile);
    string historical=Path.Combine(historicalProfile,"recovery-999999.json");File.WriteAllText(historical,"[]");
    using(var limit=new CancellationTokenSource(2000))await ProcessMaintenance.StopAsync(Path.Combine(root,"never-running"),historicalProfile,limit.Token);
    Check("UnrelatedHistoricalRecoveryPreservedAndNotBlocking",File.Exists(historical));
}
catch(Exception e){errors.Add(e.ToString());}
File.WriteAllText(Path.Combine(root,"update-install-tests.json"),JsonSerializer.Serialize(new{Passed=errors.Count==0,Checks=checks,Errors=errors},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{checks.Count} checks, {errors.Count} failures");foreach(var e in errors)Console.WriteLine(e);return errors.Count==0?0:1;

sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> response):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>response(request,token);}
