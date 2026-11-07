param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$toolRoot=Join-Path $root '.tools/native-bridge'
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
$archive=Join-Path $toolRoot 'llvm-mingw-20261104-ucrt-x86_64.zip'
if(!(Test-Path -LiteralPath $archive)){
 Write-Output 'Downloading portable C++ toolchain'
 Invoke-WebRequest -Uri 'https://github.com/mstorsjo/llvm-mingw/releases/download/20261104/llvm-mingw-20261104-ucrt-x86_64.zip' -OutFile $archive -TimeoutSec 300
}
if((Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant() -ne '552191b5754c49a5a20723276a6fc56541ea1922c4c50b7b669022333fc1b40f'){throw 'Toolchain digest mismatch'}
if(!(Test-Path -LiteralPath (Join-Path $toolRoot 'llvm-mingw-20261104-ucrt-x86_64/bin/clang++.exe'))){Expand-Archive -LiteralPath $archive -DestinationPath $toolRoot -Force}
$package=Join-Path $toolRoot 'cppwinrt-2.0.240405.15.zip'
$url='https://api.nuget.org/v3-flatcontainer/microsoft.windows.cppwinrt/2.0.240405.15/microsoft.windows.cppwinrt.2.0.240405.15.nupkg'
if(!(Test-Path -LiteralPath $package)){Invoke-WebRequest -Uri $url -OutFile $package -TimeoutSec 120}
$actual=[Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
$env:DOTNET_CLI_HOME=Join-Path $root '.tools/cli-home'
& $Dotnet nuget verify $package --all --verbosity minimal
if($LASTEXITCODE){throw 'C++ WinRT package signature verification failed'}
Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $toolRoot 'cppwinrt') -Force
& (Join-Path $toolRoot 'cppwinrt/bin/cppwinrt.exe') -input (Join-Path $env:WINDIR 'System32/WinMetadata') -output (Join-Path $toolRoot 'generated')
if($LASTEXITCODE){throw 'WinRT header generation failed'}
[ordered]@{ToolchainSHA256=(Get-FileHash -LiteralPath $archive).Hash;CppWinRTPackageSHA512=$actual} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $toolRoot 'toolchain-manifest.json') -Encoding utf8
Write-Output 'Native bridge tools verified and unpacked'
