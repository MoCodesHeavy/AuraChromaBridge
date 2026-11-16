@echo off
rem Step 1b: gives every ASUS header its own colour for 30 seconds, so you can see which one the fans are on.
setlocal
cd /d "%~dp0"
fltmc >nul 2>&1
if errorlevel 1 (
    echo Asking Windows for administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
title AuraChromaBridge - 1b. find the fan header
call "%~dp0build.cmd" test
if errorlevel 1 goto done
echo.
"%~dp0AuraChromaBridge-test.exe" identify
:done
echo.
echo Finished. Press any key to close this window.
pause >nul
