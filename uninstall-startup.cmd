@echo off
rem Undoes install-startup.cmd: stops the background sync, removes the start-with-Windows task
rem and the installed copy in Program Files. Your project folder and settings stay. Asks for admin once.
setlocal
cd /d "%~dp0"
fltmc >nul 2>&1
if errorlevel 1 (
    echo Asking Windows for administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
title AuraChromaBridge - uninstall

echo Stopping the background sync...
echo stop> "%~dp0stop.request"
timeout /t 3 /nobreak >nul
taskkill /im AuraChromaBridge-bg.exe /f >nul 2>&1

echo Removing the start-with-Windows task...
schtasks /delete /tn "AuraChromaBridge" /f

echo Removing %ProgramFiles%\AuraChromaBridge ...
if exist "%ProgramFiles%\AuraChromaBridge" rd /s /q "%ProgramFiles%\AuraChromaBridge"

echo.
echo Done. Press any key to close this window.
pause >nul
