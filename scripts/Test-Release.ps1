param([Parameter(Mandatory=$true)][string]$CandidateDirectory,[Parameter(Mandatory=$true)][string]$FixtureExe,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot -Parent
$exe=Join-Path ([IO.Path]::GetFullPath($CandidateDirectory)) 'NestedTray.exe'
$fixture=[IO.Path]::GetFullPath($FixtureExe)
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path (Split-Path $exe) 'NestedTray.dll')).ProductVersion
if(!$OutputDirectory){$OutputDirectory=Join-Path $workspace 'artifacts\qa'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results=[Collections.Generic.List[object]]::new()
$cases=@(
 @{Name='appearance';Mode='--self-test-appearance';Report='appearance-test.json';Fixture=$false},
 @{Name='entry-hover';Mode='--self-test-entry-hover';Report='entry-hover-test.json';Fixture=$true},
 @{Name='placement';Mode='--self-test-placement';Report='placement-test.json';Fixture=$false},
 @{Name='experience';Mode='--self-test-experience';Report='experience-test.json';Fixture=$false},
 @{Name='fixed-tools';Mode='--self-test-fixed-tools';Report='fixed-tools-test.json';Fixture=$false},
 @{Name='reorder-motion';Mode='--self-test-reorder-motion';Report='reorder-motion-test.json';Fixture=$false},
 @{Name='panel-reorder';Mode='--self-test-panel-reorder';Report='panel-reorder-test.json';Fixture=$false},
 @{Name='startup';Mode='--self-test-startup';Report='startup-test.json';Fixture=$false},
 @{Name='tray-connection';Mode='--self-test-tray-connection';Report='tray-connection-test.json';Fixture=$false},
 @{Name='diagnostics';Mode='--self-test-diagnostics';Report='diagnostics-test.json';Fixture=$false},
 @{Name='reliability';Mode='--self-test-reliability';Report='reliability-test.json';Fixture=$true},
 @{Name='animation';Mode='--self-test-animation';Report='dismiss-animation-test.json';Fixture=$false},
 @{Name='own-menu';Mode='--self-test-menu';Report='own-menu-test.json';Fixture=$false},
 @{Name='legacy';Mode='--self-test-legacy';Report='product-self-test.json';Fixture=$true},
 @{Name='guid';Mode='--self-test';Report='product-self-test.json';Fixture=$true},
 @{Name='ole';Mode='--self-test-ole-live';Report='ole-live-test.json';Fixture=$true},
 @{Name='layout';Mode='--self-test-layout';Report='panel-layout-test.json';Fixture=$false},
 @{Name='hidden';Mode='--self-test-hidden';Report='hidden-state-test.json';Fixture=$true}
)
foreach($item in $cases){
 $profile=Join-Path $OutputDirectory $item.Name;$arguments=@($item.Mode,$profile);if($item.Fixture){$arguments+=$fixture}
 $clock=[Diagnostics.Stopwatch]::StartNew()
 $start=[Diagnostics.ProcessStartInfo]::new($exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 $run=[Diagnostics.Process]::Start($start)
 if(!$run.WaitForExit(45000)){$run.Kill();throw ('Own regression process timed out: '+$item.Name)}
 $result=[pscustomobject]@{Case=$item.Name;ExitCode=$run.ExitCode;Seconds=[Math]::Round($clock.Elapsed.TotalSeconds,2);Report=(Join-Path $profile $item.Report)}
 $results.Add($result);$result | ConvertTo-Json -Compress
 [ordered]@{Version=$version;SelfContained=$true;Runtime='10.0.14';ProgramSHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) 'NestedTray.dll')).Hash;Cases=$results.ToArray();AllPassed=($results.Count-eq $cases.Count -and @($results | Where-Object {$_.ExitCode-ne 0}).Count-eq 0)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
 if($run.ExitCode-ne 0){Get-Content -LiteralPath $result.Report -Raw;exit $run.ExitCode}
}
