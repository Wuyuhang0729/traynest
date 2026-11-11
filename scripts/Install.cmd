@echo off
setlocal
if not exist "%~dp0..\TrayNest.Setup.exe" (
 echo The TrayNest installer is missing. Extract the complete package first.
 pause
 exit /b 1
)
start "" "%~dp0..\TrayNest.Setup.exe"
