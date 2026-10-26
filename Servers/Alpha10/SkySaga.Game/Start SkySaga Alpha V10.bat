@echo off
setlocal
title SkySaga Alpha V10 launcher
set "ROOT=%~dp0"
set "BIN=%ROOT%bin\Debug\net10.0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK is required to build the Alpha V10 server.
    echo Install the .NET 10 SDK, then launch this file again.
    pause
    exit /b 1
)

set "PYTHON_COMMAND="
where python >nul 2>&1
if not errorlevel 1 set "PYTHON_COMMAND=python"
if not defined PYTHON_COMMAND (
    where py >nul 2>&1
    if not errorlevel 1 set "PYTHON_COMMAND=py"
)
if not defined PYTHON_COMMAND (
    echo Python 3 is required to run the web server.
    echo Install Python 3 and make sure python.exe or py.exe is available on PATH.
    pause
    exit /b 1
)

echo Stopping previous Alpha V10 game server processes...
taskkill /f /t /im SkySaga.Game.exe >nul 2>&1
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":5001" ^| findstr "LISTENING"') do taskkill /f /pid %%a >nul 2>&1
timeout /t 1 /nobreak >nul

echo Building the Alpha V10 game server...
dotnet build "%ROOT%SkySaga.Game.csproj" -nologo -v q
if errorlevel 1 (
    echo Build failed. No servers were started.
    pause
    exit /b 1
)
if not exist "%BIN%\SkySaga.Game.exe" (
    echo Build completed but SkySaga.Game.exe was not found at:
    echo %BIN%
    pause
    exit /b 1
)

echo Starting the web server on TCP 5001...
if "%PYTHON_COMMAND%"=="py" (
    start "SkySaga Web" /d "%ROOT%" cmd /k "set PYTHONUNBUFFERED=1&&py -3 server.py"
) else (
    start "SkySaga Web" /d "%ROOT%" cmd /k "set PYTHONUNBUFFERED=1&&python -u server.py"
)

echo Starting the City of Last Light prototype on UDP 42070...
start "SkySaga City of Last Light (prototype)" /d "%BIN%" cmd /k "set SKYSAGA_WORLD=social&&set SKYSAGA_MAP=SocialHub.json&&set SKYSAGA_ADVENTURE=home&&set SKYSAGA_GAME_PORT=42070&&set SKYSAGA_HOME_PORT=42069&&set SKYSAGA_FTUE_PORT=42071&&set SKYSAGA_MASTERY_FOREST_PORT=42072&&set SKYSAGA_MASTERY_DESERT_PORT=42073&&set SKYSAGA_MASTERY_JUNGLE_PORT=42074&&set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&SkySaga.Game.exe"

echo Starting all four adventure servers in one console...
start "SkySaga Adventures" /d "%BIN%" powershell.exe -NoProfile -NoExit -ExecutionPolicy Bypass -File "%ROOT%Start Adventures.ps1" -BinDirectory "%BIN%"

echo Starting the home world on UDP 42069...
start "SkySaga Game" /d "%BIN%" cmd /k "set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&set SKYSAGA_SOCIAL_HUB_PORT=42070&&set SKYSAGA_FTUE_PORT=42071&&set SKYSAGA_MASTERY_FOREST_PORT=42072&&set SKYSAGA_MASTERY_DESERT_PORT=42073&&set SKYSAGA_MASTERY_JUNGLE_PORT=42074&&set SKYSAGA_ADVENTURE_SUPPLIES_CHESTS=1&&set SKYSAGA_CRAFTING_TEST_ROW=1&&set SKYSAGA_BIOME_INDEX=3&&set SKYSAGA_REGION_INDEX=23&&set SKYSAGA_ADVENTURE_INDEX=88&&set SKYSAGA_PALETTE=34&&SkySaga.Game.exe"

echo Home, City, Old City, and three Mastery test worlds are starting.
echo Close this launcher window; the server consoles will remain open.
endlocal
