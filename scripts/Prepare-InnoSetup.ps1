$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root '.tools/innosetup-7.1.1'
if(Test-Path -LiteralPath (Join-Path $directory 'ISCC.exe')){return}
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$installer=Join-Path $directory 'innosetup-7.1.1-x64.exe'
Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe' -OutFile $installer -TimeoutSec 120
$signature=Get-AuthenticodeSignature -LiteralPath $installer
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B.V.'){throw 'Official Inno Setup signature validation failed.'}
$process=Start-Process -FilePath $installer -ArgumentList @('/PORTABLE=1','/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$directory+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Portable compiler setup failed.'}
if(!(Test-Path -LiteralPath (Join-Path $directory 'ISCC.exe'))){throw 'Compiler missing after portable setup.'}
