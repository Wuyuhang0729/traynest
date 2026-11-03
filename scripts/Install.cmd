@echo off
setlocal
rem Use Windows PowerShell's own modules even when started from PowerShell 7.
set "PSModulePath="
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-TrayNest.ps1" %*
if errorlevel 1 (
 echo.
 echo TrayNest installation failed. See the message above.
 pause
 exit /b 1
)
echo.
pause
