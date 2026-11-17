@echo off
rem Builds with the C# compiler that ships inside Windows (.NET Framework 4.x).
rem   build.cmd        -> AuraChromaBridge.exe       (the sync, with a console window)
rem   build.cmd test   -> AuraChromaBridge-test.exe  (used by the test scripts, so they never clash with a running sync)
rem   build.cmd bg     -> AuraChromaBridge-bg.exe    (no window - the copy that starts with Windows)
rem   build.cmd selftest -> AuraChromaBridge-selftest.exe (checks the parts that need no Razer or ASUS hardware)
setlocal
cd /d "%~dp0"
set "KIND=%~1"
if "%KIND%"=="" set "KIND=main"
set "OUT=AuraChromaBridge.exe"
set "TARGET=exe"
set "SOURCES=src\*.cs"
set "EXTRA="
if /i "%KIND%"=="test" set "OUT=AuraChromaBridge-test.exe"
if /i "%KIND%"=="bg" set "OUT=AuraChromaBridge-bg.exe"
if /i "%KIND%"=="bg" set "TARGET=winexe"
if /i "%KIND%"=="selftest" set "OUT=AuraChromaBridge-selftest.exe"
if /i "%KIND%"=="selftest" set "SOURCES=src\*.cs tests\*.cs"
if /i "%KIND%"=="selftest" set "EXTRA=/main:AuraChromaBridge.Tests.SelfTest"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Could not find the C# compiler that comes with Windows at:
    echo   %CSC%
    exit /b 1
)
if not exist logs mkdir logs
echo Building %OUT% ...
"%CSC%" /nologo /noconfig /target:%TARGET% /platform:x64 /optimize+ /warn:4 /out:%OUT% /win32manifest:app.manifest /r:System.dll /r:System.Core.dll %EXTRA% %SOURCES% > "logs\build-%KIND%.log" 2>&1
set "ERR=%errorlevel%"
type "logs\build-%KIND%.log"
if not "%ERR%"=="0" (
    echo.
    echo BUILD FAILED - details are in logs\build-%KIND%.log
    exit /b %ERR%
)
echo Build OK.
exit /b 0
