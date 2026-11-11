param(
 [string]$SourceDirectory=$PSScriptRoot,
 [string]$InstallDirectory=(Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\TrayNest'),
 [switch]$NoLaunch,
 [switch]$PassThru
)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$target=[IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
$installer=Join-Path (Split-Path $source -Parent) 'TrayNest.Setup.exe'
if(!(Test-Path -LiteralPath $installer -PathType Leaf)){throw '未找到 Inno 安装入口，请完整解压下载包后运行外层 TrayNest.Setup.exe。'}
if($NoLaunch){throw '当前入口需要通过向导完成安装与启动核对；不支持跳过启动核对。'}
$arguments=@('/NORESTART','/NOCLOSEAPPLICATIONS',('/DIR="'+$target+'"'))
$process=Start-Process -FilePath $installer -ArgumentList $arguments -WindowStyle Normal -PassThru
$process.WaitForExit()
if($process.ExitCode-ne 0){throw ('安装未完成，退出码：'+$process.ExitCode)}
if($PassThru){[pscustomobject]@{Installed=$true;Directory=$target;Installer='Inno Setup'}}