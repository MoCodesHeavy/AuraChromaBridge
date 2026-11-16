@echo off
rem Step 1: checks that ASUS Aura can drive the case fans (red, green, blue, white, one device at a time, rainbow).
setlocal
cd /d "%~dp0"
fltmc >nul 2>&1
if errorlevel 1 (
    echo Asking Windows for administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
title AuraChromaBridge - 1. fan test
call "%~dp0build.cmd" test
if errorlevel 1 goto done
echo.
"%~dp0AuraChromaBridge-test.exe" aura-test
:done
echo.
echo Finished. Press any key to close this window.
pause >nul
