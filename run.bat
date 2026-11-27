@echo off
setlocal enabledelayedexpansion
title RadioServer

echo ========================================
echo  RadioServer - Internet Radio on ASP.NET Core
echo ========================================
echo.

:: Working directory = folder where this .bat file is located
set "PROJECT_DIR=%~dp0"
cd /d "%PROJECT_DIR%" 2>nul
if errorlevel 1 (
    echo [ERROR] Cannot access project directory.
    pause
    exit /b 1
)

echo Project folder: %PROJECT_DIR%

:: Check dotnet
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found in PATH.
    echo         Install .NET SDK 6/7/8 or 10 from https://dotnet.microsoft.com
    echo.
    pause
    exit /b 1
)
echo dotnet found.

echo [1/3] Checking project files...
if not exist "RadioServer.csproj" (
    echo [ERROR] RadioServer.csproj not found in %PROJECT_DIR%
    echo         Place run.bat inside the RadioServer project folder.
    echo.
    pause
    exit /b 1
)
echo Project file found.

echo [2/3] Building (Release)...
dotnet build -c Release --nologo > "%TEMP%\RadioServer_build.log" 2>&1
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. See %TEMP%\RadioServer_build.log
    type "%TEMP%\RadioServer_build.log"
    echo.
    pause
    exit /b 1
)
echo Build OK.

:: Free port 5033 if occupied by a previous RadioServer instance
echo [3/3] Checking port 5033...
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5033"') do (
    echo Port 5033 occupied by PID %%a - terminating...
    taskkill /PID %%a /F >nul 2>&1
)
timeout /t 2 >nul

echo Starting server on http://127.0.0.1:5033 ...
echo         Open in browser: http://127.0.0.1:5033/
echo         To stop, press Ctrl+C in this window.
echo ----------------------------------------

set "ASPNETCORE_URLS=http://127.0.0.1:5033"
dotnet run -c Release > "%TEMP%\RadioServer_run.log" 2>&1

echo.
echo Server stopped.
if errorlevel 1 (
    echo [ERROR] Server exited with an error. See %TEMP%\RadioServer_run.log
    type "%TEMP%\RadioServer_run.log"
)
echo.
pause
endlocal
