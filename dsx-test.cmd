@echo off
rem Tests whether DSX accepts light bar colours from our bridge. Result: logs\dsx-test.log
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dsx-test.ps1"
timeout /t 4 >nul
