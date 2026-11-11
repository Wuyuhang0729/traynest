param([Parameter(Mandatory=$true)][string]$Directory,[Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Directory).TrimEnd('\')
$files=@(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {$_.Name -ne 'traynest-package.json'} | Sort-Object FullName | ForEach-Object {
 if(($_.Attributes-band [IO.FileAttributes]::ReparsePoint)-ne 0){throw 'Package files cannot be reparse points.'}
 [ordered]@{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Size=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
[ordered]@{Schema=1;Version=$Version;Platform='win-x64';Files=$files} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'traynest-package.json') -Encoding utf8
