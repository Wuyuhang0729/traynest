param([string]$InstallDirectory=$PSScriptRoot,[string]$DesktopDirectory=[Environment]::GetFolderPath('Desktop'))
$ErrorActionPreference='Stop'
$exe=Join-Path ([IO.Path]::GetFullPath($InstallDirectory)) 'NestedTray.exe'
$icon=Join-Path ([IO.Path]::GetFullPath($InstallDirectory)) 'TrayNest.ico'
if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $icon)){throw 'TrayNest executable or icon is missing.'}
if(!(Test-Path -LiteralPath $DesktopDirectory -PathType Container)){throw 'Desktop folder is unavailable.'}
$destination=Join-Path ([IO.Path]::GetFullPath($DesktopDirectory)) 'TrayNest.lnk'
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($destination)
$shortcut.TargetPath=$exe
$shortcut.Arguments=''
$shortcut.WorkingDirectory=[IO.Path]::GetDirectoryName($exe)
$shortcut.IconLocation=$icon+',0'
$shortcut.Description='TrayNest · 托盘收纳盒'
$shortcut.Save()
$saved=$shell.CreateShortcut($destination)
if($saved.TargetPath-ne $exe -or $saved.IconLocation-ne ($icon+',0') -or $saved.Arguments -ne '' -or $saved.WorkingDirectory-ne [IO.Path]::GetDirectoryName($exe)){throw 'Desktop shortcut verification failed.'}
[pscustomobject]@{Shortcut=$destination;Target=$shortcut.TargetPath;Icon=$shortcut.IconLocation}|ConvertTo-Json
