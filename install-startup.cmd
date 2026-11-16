@echo off
rem One-time setup (asks for admin once). Afterwards the sync starts by itself when you sign in to Windows,
rem runs in the background with admin rights and no window, and start-sync.cmd / stop-sync.cmd control it
rem without any prompts. Run this again after changing the code to install the new version.
setlocal
cd /d "%~dp0"
fltmc >nul 2>&1
if errorlevel 1 (
    echo Asking Windows for administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
title AuraChromaBridge - install
set "INSTALL=%ProgramFiles%\AuraChromaBridge"

echo Stopping any running copies...
taskkill /im AuraChromaBridge.exe /f >nul 2>&1
taskkill /im AuraChromaBridge-bg.exe /f >nul 2>&1
timeout /t 2 /nobreak >nul

call "%~dp0build.cmd" bg
if errorlevel 1 goto failed

echo Copying to %INSTALL% ...
if not exist "%INSTALL%" mkdir "%INSTALL%"
copy /y "%~dp0AuraChromaBridge-bg.exe" "%INSTALL%\AuraChromaBridge-bg.exe" >nul
if errorlevel 1 goto failed

echo Registering the start-with-Windows task...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-task.ps1" -Exe "%INSTALL%\AuraChromaBridge-bg.exe" -Data "%CD%"
if errorlevel 1 goto failed

echo Starting it now...
schtasks /run /tn "AuraChromaBridge" >nul
if errorlevel 1 goto failed

echo.
echo Done. The sync is running in the background and starts by itself every time you sign in.
echo Its log is logs\run.log. stop-sync.cmd and start-sync.cmd stop and start it with no prompts.
goto end

:failed
echo.
echo Something went wrong - see the messages above.

:end
echo.
echo Press any key to close this window.
pause >nul
