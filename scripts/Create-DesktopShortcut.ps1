param([string]$InstallDirectory=$PSScriptRoot)
$ErrorActionPreference='Stop'
$exe=Join-Path ([IO.Path]::GetFullPath($InstallDirectory)) 'NestedTray.exe'
$icon=Join-Path ([IO.Path]::GetFullPath($InstallDirectory)) 'TrayNest.ico'
if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $icon)){throw 'TrayNest executable or icon is missing.'}
$destination=Join-Path ([Environment]::GetFolderPath('Desktop')) 'TrayNest.lnk'
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($destination)
$shortcut.TargetPath=$exe
$shortcut.WorkingDirectory=[IO.Path]::GetDirectoryName($exe)
$shortcut.IconLocation=$icon+',0'
$shortcut.Description='TrayNest · 托盘收纳盒'
$shortcut.Save()
[pscustomobject]@{Shortcut=$destination;Target=$shortcut.TargetPath;Icon=$shortcut.IconLocation}|ConvertTo-Json
