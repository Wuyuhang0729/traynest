param(
 [string]$SourceDirectory=$PSScriptRoot,
 [string]$InstallDirectory=(Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\TrayNest'),
 [string]$DesktopDirectory=[Environment]::GetFolderPath('Desktop'),
 [switch]$NoLaunch,
 [switch]$PassThru
)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$target=[IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
$desktop=[IO.Path]::GetFullPath($DesktopDirectory)
$required=@('NestedTray.exe','NestedTray.dll','NestedTray.runtimeconfig.json','TrayNest.ico','Create-DesktopShortcut.ps1')
foreach($name in $required){
 if(!(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)){throw ('安装包不完整，请重新解压完整下载包。缺少：'+$name)}
}
if($source.Equals($target,[StringComparison]::OrdinalIgnoreCase) -or $target.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or $source.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){
 throw '安装目录与安装包目录必须分开。请先将下载包解压到其他文件夹，再安装。'
}
if(!(Test-Path -LiteralPath $desktop -PathType Container)){throw '未找到桌面文件夹，安装尚未开始。'}
if(Test-Path -LiteralPath $target){
 if(!(Test-Path -LiteralPath (Join-Path $target 'NestedTray.exe') -PathType Leaf)){throw '目标文件夹不是 TrayNest 安装目录，请选择其他位置。'}
 foreach($process in @(Get-Process -Name NestedTray -ErrorAction SilentlyContinue)){
  if($process.Path -and $process.Path.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){throw '请先打开 TrayNest 设置，点击左下方“退出 TrayNest”，再重新安装。'}
 }
}
$parent=Split-Path $target -Parent
$leaf=Split-Path $target -Leaf
if(!$parent -or !$leaf){throw '安装目录无效。'}
New-Item -ItemType Directory -Path $parent -Force|Out-Null
$suffix=[Guid]::NewGuid().ToString('N')
$stage=Join-Path $parent ($leaf+'.install-'+$suffix)
$backup=Join-Path $parent ($leaf+'.previous-'+$suffix)
$shortcut=Join-Path $desktop 'TrayNest.lnk'
$oldShortcut=if(Test-Path -LiteralPath $shortcut -PathType Leaf){[IO.File]::ReadAllBytes($shortcut)}else{$null}
$hadBackup=$false;$movedNew=$false;$committed=$false
function Remove-InstallTemporary([string]$Path){
 $resolved=[IO.Path]::GetFullPath($Path)
 if($resolved-ne $stage -and $resolved-ne $backup){throw 'Refusing to remove an unexpected install path.'}
 if((Split-Path $resolved -Parent)-ne $parent){throw 'Install cleanup must stay in its parent directory.'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
try {
 Write-Host '正在安装 TrayNest…'
 New-Item -ItemType Directory -Path $stage|Out-Null
 Get-ChildItem -LiteralPath $source -Force|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse -Force}
 foreach($name in $required){
  if((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash-ne (Get-FileHash -LiteralPath (Join-Path $stage $name)).Hash){throw ('安装文件校验失败：'+$name)}
 }
 if(Test-Path -LiteralPath $target){[IO.Directory]::Move($target,$backup);$hadBackup=$true}
 [IO.Directory]::Move($stage,$target);$movedNew=$true
 $shortcutJson=& (Join-Path $target 'Create-DesktopShortcut.ps1') -InstallDirectory $target -DesktopDirectory $desktop
 $shortcutResult=$shortcutJson|ConvertFrom-Json
 if(!(Test-Path -LiteralPath $shortcutResult.Shortcut -PathType Leaf)){throw '桌面快捷方式未能创建。'}
 $committed=$true
}catch{
 if($movedNew -and (Test-Path -LiteralPath $target)){
  # Move only the exact installation created above; never recursively delete the target.
  [IO.Directory]::Move($target,$stage)
 }
 if($hadBackup){[IO.Directory]::Move($backup,$target)}
 if($null-ne $oldShortcut){[IO.File]::WriteAllBytes($shortcut,$oldShortcut)}
 elseif(Test-Path -LiteralPath $shortcut -PathType Leaf){Remove-Item -LiteralPath $shortcut -Force}
 throw
}finally{
 Remove-InstallTemporary $stage
 if($committed -and $hadBackup){
  try{Remove-InstallTemporary $backup}catch{Write-Warning ('安装完成，旧文件暂存于：'+$backup)}
 }
}
Write-Host '安装完成，桌面上的 TrayNest 快捷方式已准备好。'
$launched=$false
if(!$NoLaunch){
 try{Start-Process -FilePath (Join-Path $target 'NestedTray.exe') -WorkingDirectory $target -WindowStyle Hidden;$launched=$true}
 catch{Write-Warning '安装已完成，请双击桌面上的 TrayNest 打开软件。'}
}
if($PassThru){[pscustomobject]@{Installed=$true;Directory=$target;Shortcut=$shortcutResult.Shortcut;Icon=$shortcutResult.Icon;Launched=$launched}}
