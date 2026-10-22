@echo off
title SkySaga two worlds launcher
set "ROOT=%~dp0"
set "BIN=%ROOT%bin\Debug\net10.0"

echo Stopping any old Alpha V10 game server processes...
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

echo Starting the web server on TCP 5001...
start "SkySaga Web" /d "%ROOT%" cmd /k "set PYTHONUNBUFFERED=1&&python server.py"

echo Starting the City of Last Light prototype on UDP 42070...
start "SkySaga City of Last Light (prototype)" /d "%BIN%" cmd /k "set SKYSAGA_WORLD=social&&set SKYSAGA_MAP=SocialHub.json&&set SKYSAGA_ADVENTURE=home&&set SKYSAGA_GAME_PORT=42070&&set SKYSAGA_HOME_PORT=42069&&set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&SkySaga.Game.exe"

echo Starting the home world on UDP 42069...
start "SkySaga Game" /d "%BIN%" cmd /k "set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&set SKYSAGA_SOCIAL_HUB_PORT=42070&&SkySaga.Game.exe"

echo Both game worlds are starting in separate windows.
timeout /t 5
