param([Parameter(Mandatory=$true)][string]$Payload,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot -Parent
$payload=[IO.Path]::GetFullPath($Payload)
$root=[IO.Path]::GetFullPath($OutputDirectory)
if(!$root.StartsWith((Join-Path $workspace 'artifacts')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Tests must remain under project artifacts.'}
New-Item -ItemType Directory -Path $root -Force | Out-Null
$tnProfile=Join-Path $root 'profile';New-Item -ItemType Directory -Path $tnProfile -Force | Out-Null
$config=Join-Path $tnProfile 'settings.json';[IO.File]::WriteAllText($config,'{"Collected":[],"Tools":[],"Columns":7}')
$configHash=(Get-FileHash -LiteralPath $config).Hash
$target=Join-Path $root 'installed'
$iscc=Join-Path $workspace '.tools/innosetup-7.1.1/ISCC.exe'
$checks=[ordered]@{}
function Run-Installer([string]$Exe,[string]$Log){
 $process=Start-Process -FilePath $Exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/TASKS=','/NOICONS',('/DIR="'+$target+'"'),('/LOG="'+$Log+'"')) -WindowStyle Hidden -Wait -PassThru
 return $process.ExitCode
}
function Compile-Test([string]$Source,[string]$Name){
 $output=Join-Path $root $Name
 & $iscc ('/DPayload='+$Source) '/DAppVersion=0.2.0-preview.17' ('/DOutput='+$output) ('/DTestProfile='+$tnProfile) (Join-Path $workspace 'installer/TrayNest.iss') *> (Join-Path $root ($Name+'-compile.log'))
 if($LASTEXITCODE-ne 0){throw 'Test installer compile failed.'}
 return Join-Path $output 'TrayNest-Setup-v0.2.0-preview.17-win-x64.exe'
}
$installer=Compile-Test $payload 'good'
$checks.FreshInstall=(Run-Installer $installer (Join-Path $root 'fresh.log'))-eq 0
$checks.NativeEntryExists=Test-Path -LiteralPath (Join-Path $target 'NestedTray.exe')
$checks.UninstallerExists=Test-Path -LiteralPath (Join-Path $target 'unins000.exe')
$checks.ConfigPreservedOnInstall=(Get-FileHash -LiteralPath $config).Hash-eq $configHash
$dll=Join-Path $target 'NestedTray.dll';$expected=(Get-FileHash -LiteralPath (Join-Path $payload 'NestedTray.dll')).Hash
$checks.PayloadHashMatches=(Get-FileHash -LiteralPath $dll).Hash-eq $expected
[IO.File]::WriteAllText((Join-Path $target 'local-note.txt'),'preserve local history')
$checks.ExistingInstallUpdates=(Run-Installer $installer (Join-Path $root 'update.log'))-eq 0
$checks.ConfigPreservedOnUpdate=(Get-FileHash -LiteralPath $config).Hash-eq $configHash
$checks.LocalHistoryPreserved=(Get-Content -LiteralPath (Join-Path $target 'local-note.txt') -Raw)-eq 'preserve local history'
$states=@(Get-ChildItem -LiteralPath (Join-Path $tnProfile 'maintenance') -Filter 'install-*.json' | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json})
$checks.UpdatedWithVerifiedBackup=@($states | Where-Object {$_.Existed-and $_.Completed-and (Test-Path -LiteralPath $_.Backup)}).Count-gt 0
$badPayload=Join-Path $root 'corrupt-payload';Copy-Item -LiteralPath $payload -Destination $badPayload -Recurse
[IO.File]::WriteAllText((Join-Path $badPayload 'NestedTray.dll'),'controlled corrupt candidate')
$badInstaller=Compile-Test $badPayload 'corrupt'
$badExit=Run-Installer $badInstaller (Join-Path $root 'corrupt.log')
$checks.CorruptInstallFails=$badExit-ne 0
$checks.RealInstallerRestoresPrevious=(Test-Path -LiteralPath $dll)-and (Get-FileHash -LiteralPath $dll).Hash-eq $expected
$checks.ConfigPreservedOnFailure=(Get-FileHash -LiteralPath $config).Hash-eq $configHash
$uninstaller=Join-Path $target 'unins000.exe'
if(Test-Path -LiteralPath $uninstaller){
 $remove=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'uninstall.log')+'"')) -WindowStyle Hidden -Wait -PassThru
 $checks.UninstallSucceeds=$remove.ExitCode-eq 0
 $checks.ProgramRemoved=!(Test-Path -LiteralPath (Join-Path $target 'NestedTray.exe'))
 $checks.ConfigPreservedOnUninstall=(Get-FileHash -LiteralPath $config).Hash-eq $configHash
}
$report=[ordered]@{Passed=@($checks.Values | Where-Object {!$_}).Count-eq 0;Checks=$checks;CorruptExitCode=$badExit;Scope='Dedicated Inno AppId and profile; no tray launch or real startup changes.'}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'installer-tests.json') -Encoding utf8
$report | ConvertTo-Json -Depth 4
if(!$report.Passed){throw 'Installer acceptance failed; preserve logs and backups.'}
