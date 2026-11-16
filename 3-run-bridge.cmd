@echo off
rem Step 3: runs the sync - one effect on Razer and ASUS. Close the window to stop; Synapse and Armoury Crate get the lights back.
setlocal
cd /d "%~dp0"
fltmc >nul 2>&1
if errorlevel 1 (
    echo Asking Windows for administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
title AuraChromaBridge - running
call "%~dp0build.cmd"
if errorlevel 1 goto done
:run
echo.
"%~dp0AuraChromaBridge.exe" run
if "%errorlevel%"=="3" (
    echo Restarting in 2 seconds...
    timeout /t 2 /nobreak >nul
    goto run
)
:done
echo.
echo Stopped. Press any key to close this window.
pause >nul
