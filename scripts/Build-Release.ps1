param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot -Parent
$project=Join-Path $workspace 'src\NestedTray\NestedTray.csproj'
[xml]$xml=Get-Content -LiteralPath $project -Raw
$version=@($xml.Project.PropertyGroup.Version | Where-Object {$_})[0]
if($version-notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$'){throw 'Invalid project version.'}
$work=Join-Path $workspace ('artifacts\work\'+[Guid]::NewGuid().ToString('N'))
$publish=Join-Path $work 'TrayNest'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
& $Dotnet publish $project -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -p:DebugType=None -p:DebugSymbols=false -o $publish
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
& (Join-Path $PSScriptRoot 'Copy-RuntimeNotices.ps1') -AssetsFile (Join-Path $workspace 'src\NestedTray\obj\project.assets.json') -Destination $publish
foreach($name in @('Create-DesktopShortcut.ps1','Install-TrayNest.ps1','Install.cmd')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $publish}
foreach($name in @('README.md','BUILDING.md','LICENSE')){Copy-Item -LiteralPath (Join-Path $workspace $name) -Destination $publish -Force}
Copy-Item -LiteralPath (Join-Path $workspace 'docs') -Destination $publish -Recurse -Force
if(Test-Path -LiteralPath (Join-Path $workspace 'assets/readme')){
 New-Item -ItemType Directory -Path (Join-Path $publish 'assets') -Force|Out-Null
 Copy-Item -LiteralPath (Join-Path $workspace 'assets/readme') -Destination (Join-Path $publish 'assets') -Recurse -Force
 New-Item -ItemType Directory -Path (Join-Path $publish 'promo/readme') -Force|Out-Null
 foreach($relative in @('CREDITS.md','review/SOURCES.md','review/personas/说明与来源.md')){
  $destination=Join-Path $publish ('promo/readme/'+$relative)
  New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force|Out-Null
  Copy-Item -LiteralPath (Join-Path $workspace ('promo/readme/'+$relative)) -Destination $destination -Force
 }
}
$archive=Join-Path $workspace ("artifacts\TrayNest-v$version-win-x64.zip")
Compress-Archive -LiteralPath $publish -DestinationPath $archive -Force
$hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
($hash+'  '+[IO.Path]::GetFileName($archive)) | Set-Content -LiteralPath ($archive+'.sha256') -Encoding ASCII
[ordered]@{Version=$version;Directory=$publish;Archive=$archive;SHA256=$hash} | ConvertTo-Json
