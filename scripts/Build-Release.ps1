param([string]$Dotnet='dotnet',[string]$Iscc=(Join-Path (Split-Path $PSScriptRoot -Parent) '.tools/innosetup-7.1.1/ISCC.exe'))
$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot -Parent
$project=Join-Path $workspace 'src\NestedTray\NestedTray.csproj'
[xml]$xml=Get-Content -LiteralPath $project -Raw
$version=@($xml.Project.PropertyGroup.Version | Where-Object {$_})[0]
if($version-notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$'){throw 'Invalid project version.'}
$work=Join-Path $workspace ('artifacts\work\'+[Guid]::NewGuid().ToString('N'))
$publish=Join-Path $work 'TrayNest'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
if(!(Test-Path -LiteralPath (Join-Path $workspace '.tools/native-bridge/generated/winrt/Windows.UI.Xaml.Controls.h'))){& (Join-Path $PSScriptRoot 'Prepare-NativeBridgeTools.ps1') -Dotnet $Dotnet}
& (Join-Path $PSScriptRoot 'Build-NativeBridge.ps1')
& $Dotnet publish $project -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -p:DebugType=None -p:DebugSymbols=false -o $publish
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
$setup=Join-Path $work 'setup'
& $Dotnet publish (Join-Path $workspace 'src/TrayNest.Setup/TrayNest.Setup.csproj') -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $setup
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
Copy-Item -LiteralPath (Join-Path $setup 'TrayNest.Maintenance.exe') -Destination $publish
& (Join-Path $PSScriptRoot 'Copy-RuntimeNotices.ps1') -AssetsFile (Join-Path $workspace 'src\NestedTray\obj\project.assets.json') -Destination $publish
Copy-Item -LiteralPath (Join-Path $workspace 'artifacts/native-bridge/payload/licenses/native-bridge') -Destination (Join-Path $publish 'licenses') -Recurse -Force
foreach($name in @('Create-DesktopShortcut.ps1','Install-TrayNest.ps1','Install.cmd')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $publish}
foreach($name in @('README.md','CHANGELOG.md','LICENSE')){Copy-Item -LiteralPath (Join-Path $workspace $name) -Destination $publish -Force}
New-Item -ItemType Directory -Path (Join-Path $publish 'docs') -Force | Out-Null
foreach($name in @('USER_GUIDE.md','COMPATIBILITY.md','ERROR_CODES.md','INSTALLATION-NOTES.txt','RELEASE-0.2.0-preview.10.md','RELEASE-0.2.0-preview.12.md','RELEASE-0.2.0-preview.13.md',('RELEASE-'+$version+'.md'))){Copy-Item -LiteralPath (Join-Path $workspace ('docs/'+$name)) -Destination (Join-Path $publish 'docs') -Force}
if(Test-Path -LiteralPath (Join-Path $workspace 'assets/readme')){
 New-Item -ItemType Directory -Path (Join-Path $publish 'assets') -Force|Out-Null
 Copy-Item -LiteralPath (Join-Path $workspace 'assets/readme') -Destination (Join-Path $publish 'assets') -Recurse -Force
 New-Item -ItemType Directory -Path (Join-Path $publish 'promo/readme') -Force|Out-Null
 foreach($relative in @('CREDITS.md','v2/CREDITS.md','review/SOURCES.md','review/personas/说明与来源.md')){
  $destination=Join-Path $publish ('promo/readme/'+$relative)
  New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force|Out-Null
  Copy-Item -LiteralPath (Join-Path $workspace ('promo/readme/'+$relative)) -Destination $destination -Force
 }
}
& (Join-Path $PSScriptRoot 'Write-PackageManifest.ps1') -Directory $publish -Version $version
if(!(Test-Path -LiteralPath $Iscc -PathType Leaf)){throw 'Inno Setup 7.1.1 compiler missing. Run scripts/Prepare-InnoSetup.ps1.'}
& $Iscc ('/DPayload='+$publish) ('/DAppVersion='+$version) ('/DOutput='+$workspace+'\artifacts') (Join-Path $workspace 'installer/TrayNest.iss')
if($LASTEXITCODE-ne 0){throw 'Inno Setup compilation failed.'}
$installer=Join-Path $workspace ("artifacts\TrayNest-Setup-v$version-win-x64.exe")
Copy-Item -LiteralPath $installer -Destination (Join-Path $work 'TrayNest.Setup.exe')
((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($installer)) | Set-Content -LiteralPath ($installer+'.sha256') -Encoding ASCII
$archive=Join-Path $workspace ("artifacts\TrayNest-v$version-win-x64.zip")
Compress-Archive -LiteralPath @($publish,(Join-Path $work 'TrayNest.Setup.exe')) -DestinationPath $archive -Force
$hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
($hash+'  '+[IO.Path]::GetFileName($archive)) | Set-Content -LiteralPath ($archive+'.sha256') -Encoding ASCII
[ordered]@{Version=$version;Directory=$publish;Installer=$installer;Archive=$archive;SHA256=$hash} | ConvertTo-Json

