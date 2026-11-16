@echo off
rem Starts the background sync. Needs install-startup.cmd to have been run once. No admin prompt.
schtasks /run /tn "AuraChromaBridge"
if errorlevel 1 (
    echo.
    echo Could not start it. Run install-startup.cmd first.
    pause
    exit /b 1
)
echo Started. Its log is logs\run.log.
timeout /t 3 >nul
