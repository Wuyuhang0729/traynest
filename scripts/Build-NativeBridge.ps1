param([string]$OutputDirectory='artifacts/native-bridge')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=Join-Path $root '.tools/native-bridge/llvm-mingw-20261104-ucrt-x86_64/bin/clang++.exe'
$headers=Join-Path $root '.tools/native-bridge/generated'
$output=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sourceHashes=@('Bridge.cpp','Bootstrap.cpp','BridgeProtocol.h','Bridge.def','include/xamlOM.h') | ForEach-Object {(Get-FileHash -LiteralPath (Join-Path $root ('src/TrayNest.NativeBridge/'+$_))).Hash}
$sourceHashes+=@(Get-FileHash -LiteralPath (Join-Path $root '.tools/native-bridge/toolchain-manifest.json'),(Join-Path $env:WINDIR 'System32/WinMetadata/Windows.UI.Xaml.winmd') | ForEach-Object {$_.Hash})
$seed=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($sourceHashes -join '|')))
$engine=[Convert]::ToHexString($seed).Substring(0,16).ToLowerInvariant()
$tail=($seed[8..15] | ForEach-Object {'0x'+$_.ToString('x2')}) -join ','
$clsid=('{0:x8},{1:x4},{2:x4}' -f [BitConverter]::ToUInt32($seed,0),[BitConverter]::ToUInt16($seed,4),[BitConverter]::ToUInt16($seed,6)).Split(',') | ForEach-Object {'0x'+$_}
$buildHeader=@('#pragma once',('inline constexpr wchar_t GeneratedBridgeWindowClass[]=L"TrayNest.MaterialBridge.v1.'+$engine+'";'),('inline constexpr CLSID GeneratedBridgeClassId={'+($clsid -join ',')+',{'+$tail+'}};'))
$buildHeader | Set-Content -LiteralPath (Join-Path $headers 'BridgeBuild.h') -Encoding utf8
$flags=@('--target=x86_64-w64-mingw32','-std=c++20','-fms-extensions','-O2','-DWINRT_NO_MODULE_LOCK',('-I'+$headers),'-static')
& $compiler @flags -shared (Join-Path $root 'src/TrayNest.NativeBridge/Bridge.cpp') (Join-Path $root 'src/TrayNest.NativeBridge/Bridge.def') -o (Join-Path $output 'TrayNest.NativeBridge.v1.dll') -lole32 -loleaut32 -lruntimeobject -luuid
if($LASTEXITCODE){throw 'Native bridge compilation failed'}
& $compiler @flags -municode (Join-Path $root 'src/TrayNest.NativeBridge/Bootstrap.cpp') -o (Join-Path $output 'TrayNest.NativeBootstrap.exe') -lole32 -lruntimeobject -luuid
if($LASTEXITCODE){throw 'Native bootstrap compilation failed'}
& $compiler @flags -municode (Join-Path $root 'src/TrayNest.NativeBridge/Fixture.cpp') -o (Join-Path $output 'TrayNest.NativeFixture.exe') -lole32 -loleaut32 -lruntimeobject -luuid
if($LASTEXITCODE){throw 'Native fixture compilation failed'}
Copy-Item -LiteralPath (Join-Path $root 'src/TrayNest.NativeBridge/Fixture.manifest') -Destination (Join-Path $output 'TrayNest.NativeFixture.exe.manifest') -Force
Get-FileHash -LiteralPath (Join-Path $output 'TrayNest.NativeBridge.v1.dll'),(Join-Path $output 'TrayNest.NativeBootstrap.exe') | Select-Object Path,Hash
$payload=Join-Path $output 'payload/native'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$dllHash=(Get-FileHash -LiteralPath (Join-Path $output 'TrayNest.NativeBridge.v1.dll')).Hash
$bootstrapHash=(Get-FileHash -LiteralPath (Join-Path $output 'TrayNest.NativeBootstrap.exe')).Hash
$dllName='TrayNest.NativeBridge.v1.'+$dllHash.Substring(0,12).ToLowerInvariant()+'.dll'
Copy-Item -LiteralPath (Join-Path $output 'TrayNest.NativeBridge.v1.dll') -Destination (Join-Path $payload $dllName) -Force
Copy-Item -LiteralPath (Join-Path $output 'TrayNest.NativeBootstrap.exe') -Destination $payload -Force
foreach($old in @(Get-ChildItem -LiteralPath $payload -File -Filter 'TrayNest.NativeBridge.v1.*.dll' | Where-Object Name -ne $dllName)){Remove-Item -LiteralPath $old.FullName}
[ordered]@{Protocol=1;Engine=$engine;Dll=$dllName;Bootstrap='TrayNest.NativeBootstrap.exe';DllSHA256=$dllHash;BootstrapSHA256=$bootstrapHash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'bridge.json') -Encoding utf8
$notices=Join-Path $output 'payload/licenses/native-bridge'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root '.tools/native-bridge/cppwinrt/LICENSE') -Destination (Join-Path $notices 'LICENSE.CppWinRT.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'src/TrayNest.NativeBridge/include/LICENSE.win32metadata') -Destination (Join-Path $notices 'LICENSE.win32metadata.txt') -Force
Copy-Item -LiteralPath (Join-Path $root '.tools/native-bridge/llvm-mingw-20261104-ucrt-x86_64/LICENSE.TXT') -Destination (Join-Path $notices 'LICENSE.LLVM.txt') -Force
foreach($name in @('COPYING.MinGW-w64.txt','COPYING.MinGW-w64-runtime.txt','COPYING.winpthreads.txt')){Copy-Item -LiteralPath (Join-Path $root ('.tools/native-bridge/llvm-mingw-20261104-ucrt-x86_64/x86_64-w64-mingw32/share/mingw32/'+$name)) -Destination $notices -Force}
