$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $root ('verification/native-bridge/fixture-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$source=Join-Path $root 'artifacts/native-bridge'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class NativeMaterialFixture {
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")]public static extern bool PostMessageW(IntPtr h,uint msg,UIntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
}
'@
$manifest=Join-Path $evidence 'fixture.json'
$fixture=Start-Process -FilePath (Join-Path $source 'TrayNest.NativeFixture.exe') -ArgumentList ('"'+$manifest+'"') -PassThru -WindowStyle Hidden -RedirectStandardError (Join-Path $evidence 'fixture-errors.txt')
$mapping=$null;$view=$null
$owner=Start-Process -FilePath (Join-Path $source 'TrayNest.NativeBootstrap.exe') -ArgumentList '--owner-wait' -PassThru -WindowStyle Hidden
try{
 $clock=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath $manifest) -and !$fixture.HasExited -and $clock.ElapsedMilliseconds -lt 10000){Start-Sleep -Milliseconds 100}
 if(!(Test-Path -LiteralPath $manifest)){Get-Content (Join-Path $evidence 'fixture-errors.txt');throw 'Native XAML fixture failed to start'}
 $info=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
 [void][NativeMaterialFixture]::ShowWindow([IntPtr]$info.Window,5)
 [void][NativeMaterialFixture]::SetWindowPos([IntPtr]$info.Window,[IntPtr](-1),0,0,0,0,0x53)
 if(![NativeMaterialFixture]::IsWindowVisible([IntPtr]$info.Window)){throw 'Fixture window is not visible'}
 $name='Local\TrayNest.Material.Test.'+[Guid]::NewGuid().ToString('N')
 $mapping=[IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew($name,128)
 $view=$mapping.CreateViewAccessor()
 $view.Write(0,[uint32]0x544e4d31);$view.Write(4,[uint32]1);$view.Write(8,[uint32]$owner.Id);$view.Write(12,[uint32]$fixture.Id);$view.Write(16,[int64]$info.Window)
 function State { [pscustomobject]@{Connected=$view.ReadInt32(40);Targets=$view.ReadInt32(44);Applied=$view.ReadInt32(48);Ack=$view.ReadInt32(52);Error=$view.ReadInt32(56);Restored=$view.ReadInt32(60);OwnerDead=$view.ReadInt32(64);NativeVersion=$view.ReadInt32(68);Callbacks=$view.ReadInt32(72);NamedCallbacks=$view.ReadInt32(76);CallbackThread=$view.ReadInt32(80);ExpectedThread=$view.ReadInt32(84);AdviseResult=$view.ReadInt32(88)} }
 function Apply([int]$mode){$seq=$view.ReadInt32(24);$view.Write(24,[int]($seq+1));$view.Write(28,$mode);$view.Write(32,[int]($mode -ne 2));$view.Write(36,[int]1);[Threading.Thread]::MemoryBarrier();$view.Write(24,[int]($seq+2))}
 function Capture([string]$name){$rect=[NativeMaterialFixture+Rect]::new();[void][NativeMaterialFixture]::GetWindowRect([IntPtr]$info.Window,[ref]$rect);$bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top);try{$graphics=[Drawing.Graphics]::FromImage($bitmap);try{$graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)}finally{$graphics.Dispose()};$bitmap.Save((Join-Path $evidence ($name+'.png')));return $bitmap.GetPixel([int]($bitmap.Width/2),[int]($bitmap.Height/2)).ToArgb()}finally{$bitmap.Dispose()}}
 Start-Sleep -Milliseconds 400
 $before=Capture 'before'
 $bootstrap=Start-Process -FilePath (Join-Path $source 'TrayNest.NativeBootstrap.exe') -ArgumentList $fixture.Id,$name,('"'+(Join-Path $source 'TrayNest.NativeBridge.v1.dll')+'"') -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $evidence 'bootstrap.txt') -RedirectStandardError (Join-Path $evidence 'bootstrap-errors.txt')
 if(!$bootstrap.WaitForExit(12000)){throw 'Bridge bootstrap timed out'}
 $clock.Restart();while((State).Targets -lt 1 -and $clock.ElapsedMilliseconds -lt 6000){Start-Sleep -Milliseconds 100}
 $connected=State
 if($bootstrap.ExitCode -ne 0 -or $connected.Connected -ne 1 -or $connected.Targets -ne 1){Get-Content (Join-Path $evidence 'bootstrap.txt');throw ('Bridge target handshake failed: '+($connected | ConvertTo-Json -Compress))}
 Apply 4;Start-Sleep -Milliseconds 500;$painted=Capture 'test-color';$applied=State
 Apply 0;Start-Sleep -Milliseconds 500;$restored=Capture 'restored';$end=State
 $result=[ordered]@{Passed=($before -ne $painted -and $before -eq $restored -and $applied.Applied -eq 1 -and $end.Restored -eq 1 -and $end.Error -eq 0);BeforeColor=$before;PaintedColor=$painted;RestoredColor=$restored;Connected=$connected;Applied=$applied;Restored=$end;Evidence=$evidence}
 $modes=[ordered]@{}
 foreach($mode in 1,2,3){Apply $mode;Start-Sleep -Milliseconds 400;$color=Capture ('mode-'+$mode);$state=State;$modes[$mode.ToString()]=[pscustomobject]@{Applied=$state.Applied;Error=$state.Error;Color=$color};if($state.Applied -ne 1 -or $state.Error -ne 0){$result.Passed=$false}}
 $result['Modes']=$modes
 $owner.Kill();[void]$owner.WaitForExit(3000);Start-Sleep -Milliseconds 500
 $afterOwnerExit=State;$ownerExitColor=Capture 'owner-exit-restored'
 $result['OwnerExitRestores']=($afterOwnerExit.OwnerDead -eq 1 -and $afterOwnerExit.Restored -eq 1 -and $ownerExitColor -eq $before)
 $result.Passed=$result.Passed -and $result.OwnerExitRestores
 $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding utf8
 $result | ConvertTo-Json -Depth 4
 if(!$result.Passed){throw 'Native material visual/restore check failed'}
}finally{
 if($view){$view.Write(28,[int]0);$view.Write(24,[int]($view.ReadInt32(24)+2));Start-Sleep -Milliseconds 200;$view.Dispose()}
 if($mapping){$mapping.Dispose()}
 if($owner -and !$owner.HasExited){$owner.Kill();[void]$owner.WaitForExit(2000)}
 if(!$fixture.HasExited -and (Test-Path -LiteralPath $manifest)){$info=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json;[void][NativeMaterialFixture]::PostMessageW([IntPtr]$info.Window,0x10,[UIntPtr]::Zero,[IntPtr]::Zero);[void]$fixture.WaitForExit(3000)}
}
