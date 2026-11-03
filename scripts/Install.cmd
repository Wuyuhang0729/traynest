@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-TrayNest.ps1" %*
if errorlevel 1 (
 echo.
 echo TrayNest installation failed. See the message above.
 pause
 exit /b 1
)
echo.
pause
