@echo off
rem Step 4: finds which Razer app IDs Synapse accepts on this PC, then listens for 15 seconds to check
rem that Synapse's colours arrive. "4-scan-razer-ids.cmd all" tries every ID instead of stopping at the
rem first one Synapse accepts (each accepted one may be added to the Chroma Connect app list).
setlocal
cd /d "%~dp0"
rem No admin rights needed: Razer's broadcast feed works for normal programs.
title AuraChromaBridge - 4. scan Razer app IDs
call "%~dp0build.cmd" test
if errorlevel 1 goto done
echo.
"%~dp0AuraChromaBridge-test.exe" scan %*
:done
echo.
echo Finished. The results are also in logs\scan.log. Press any key to close this window.
pause >nul
