param([Parameter(Mandatory=$true)][string]$AssetsFile,[Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$assets=Get-Content -LiteralPath $AssetsFile -Raw | ConvertFrom-Json
$dependencies=@($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object {$_.name -in 'Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64'} | Sort-Object name -Unique)
if($dependencies.Count-ne 2){throw 'Expected self-contained runtime dependencies were not found.'}
foreach($dependency in $dependencies){
 $version=($dependency.version -split ',')[0].Trim('[',']','(',')',' ')
 $package=$null
 foreach($root in $assets.packageFolders.PSObject.Properties.Name){
  $candidate=Join-Path (Join-Path $root $dependency.name.ToLowerInvariant()) $version
  if(Test-Path -LiteralPath $candidate){$package=$candidate;break}
 }
 if(!$package){throw ('Runtime package unavailable: '+$dependency.name)}
 $notices=@(Get-ChildItem -LiteralPath $package -File | Where-Object {$_.Name -like 'LICENSE*' -or $_.Name -like '*NOTICES*'})
 if(!$notices){throw ('Runtime license missing: '+$dependency.name)}
 $folder=Join-Path (Join-Path $Destination 'licenses') $dependency.name
 New-Item -ItemType Directory -Path $folder -Force | Out-Null
 foreach($notice in $notices){Copy-Item -LiteralPath $notice.FullName -Destination $folder -Force}
}
