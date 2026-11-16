@echo off
rem Asks the running sync to stop; it hands the lights back to Synapse and Armoury Crate. No admin prompt.
cd /d "%~dp0"
echo stop> stop.request
echo Stop requested - the sync stops within a couple of seconds.
timeout /t 3 >nul
