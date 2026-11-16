@echo off
rem Step 2: checks that Razer's Chroma SDK can set colours on the Razer devices (red, green, blue, white, spectrum).
setlocal
cd /d "%~dp0"
rem No admin rights needed: Razer's Chroma SDK works for normal programs.
title AuraChromaBridge - 2. Razer test
call "%~dp0build.cmd" test
if errorlevel 1 goto done
echo.
"%~dp0AuraChromaBridge-test.exe" razer-test
:done
echo.
echo Finished. Press any key to close this window.
pause >nul
