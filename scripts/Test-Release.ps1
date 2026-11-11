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
 @{Name='entry-policy';Mode='--self-test-entry-policy';Report='entry-policy-test.json';Fixture=$false},
 @{Name='opening';Mode='--self-test-opening';Report='opening-test.json';Fixture=$false},
 @{Name='opening-desktop';Mode='--self-test-opening-desktop';Report='opening-test.json';Fixture=$false;TimeoutMs=90000},
 @{Name='settings';Mode='--self-test-settings';Report='settings-test.json';Fixture=$false},
 @{Name='auto-expand';Mode='--self-test-auto-expand';Report='auto-expand-test.json';Fixture=$false},
 @{Name='appearance';Mode='--self-test-appearance';Report='appearance-test.json';Fixture=$false},
 @{Name='icon-feedback';Mode='--self-test-icon-feedback';Report='icon-feedback-test.json';Fixture=$false},
 @{Name='static-footer';Mode='--self-test-static-footer';Report='static-footer-test.json';Fixture=$false},
 @{Name='motion-footer';Mode='--self-test-motion-footer';Report='motion-footer-test.json';Fixture=$false},
 @{Name='motion-clip';Mode='--self-test-motion-clip';Report='motion-clip-test.json';Fixture=$false},
 @{Name='presentation-handoff';Mode='--self-test-presentation-handoff';Report='presentation-handoff-test.json';Fixture=$false},
 @{Name='frame-paint';Mode='--self-test-frame-paint';Report='frame-paint-test.json';Fixture=$false},
 @{Name='entry-hover';Mode='--self-test-entry-hover';Report='entry-hover-test.json';Fixture=$true},
 @{Name='native-drag-feedback';Mode='--self-test-native-drag-feedback';Report='native-drag-feedback-test.json';Fixture=$true;TimeoutMs=90000},
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
 if($item.Name -eq 'native-drag-feedback'){$start.Environment['TRAYNEST_FIXTURE_TIMEOUT_MS']='90000'}
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 $run=[Diagnostics.Process]::Start($start)
 $caseTimeout=if($item.TimeoutMs){$item.TimeoutMs}else{45000}
 if(!$run.WaitForExit($caseTimeout)){$run.Kill();throw ('Own regression process timed out: '+$item.Name)}
 $result=[pscustomobject]@{Case=$item.Name;ExitCode=$run.ExitCode;Seconds=[Math]::Round($clock.Elapsed.TotalSeconds,2);Report=(Join-Path $profile $item.Report)}
 $results.Add($result);$result | ConvertTo-Json -Compress
 [ordered]@{Version=$version;SelfContained=$true;Runtime='10.0.14';ProgramSHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) 'NestedTray.dll')).Hash;Cases=$results.ToArray();AllPassed=($results.Count-eq $cases.Count -and @($results | Where-Object {$_.ExitCode-ne 0}).Count-eq 0)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
 if($run.ExitCode-ne 0){Get-Content -LiteralPath $result.Report -Raw;exit $run.ExitCode}
}
