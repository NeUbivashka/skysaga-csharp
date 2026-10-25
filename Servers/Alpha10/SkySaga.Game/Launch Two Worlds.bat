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

echo Starting The Old City adventure on UDP 42071...
start "SkySaga The Old City (adventure)" /d "%BIN%" cmd /k "set SKYSAGA_WORLD=adventure&&set SKYSAGA_INSTANCE_LOG=server-adventure.log&&set SKYSAGA_MAP=WorldMap.json&&set SKYSAGA_ADVENTURE=ftue&&set SKYSAGA_GAME_PORT=42071&&set SKYSAGA_HOME_PORT=42069&&set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&SkySaga.Game.exe"

echo Starting Mastery test adventures on UDP 42072-42074...
start "SkySaga Forest Mastery (test)" /d "%BIN%" cmd /k "set SKYSAGA_WORLD=adventure&&set SKYSAGA_INSTANCE_ID=mastery-forest&&set SKYSAGA_INSTANCE_LOG=server-mastery-forest.log&&set SKYSAGA_MAP=TestZoo.json&&set SKYSAGA_MAP_SIZE=12,4,12&&set SKYSAGA_SOLID_FLOOR=1&&set SKYSAGA_ADVENTURE=mastery-forest&&set SKYSAGA_GAME_PORT=42072&&set SKYSAGA_HOME_PORT=42069&&set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&SkySaga.Game.exe"
start "SkySaga Desert Mastery (test)" /d "%BIN%" cmd /k "set SKYSAGA_WORLD=adventure&&set SKYSAGA_INSTANCE_ID=mastery-desert&&set SKYSAGA_INSTANCE_LOG=server-mastery-desert.log&&set SKYSAGA_MAP=TestZoo.json&&set SKYSAGA_MAP_SIZE=12,4,12&&set SKYSAGA_SOLID_FLOOR=1&&set SKYSAGA_ADVENTURE=mastery-desert&&set SKYSAGA_GAME_PORT=42073&&set SKYSAGA_HOME_PORT=42069&&set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&SkySaga.Game.exe"
start "SkySaga Jungle Mastery (test)" /d "%BIN%" cmd /k "set SKYSAGA_WORLD=adventure&&set SKYSAGA_INSTANCE_ID=mastery-jungle&&set SKYSAGA_INSTANCE_LOG=server-mastery-jungle.log&&set SKYSAGA_MAP=TestZoo.json&&set SKYSAGA_MAP_SIZE=12,4,12&&set SKYSAGA_SOLID_FLOOR=1&&set SKYSAGA_ADVENTURE=mastery-jungle&&set SKYSAGA_GAME_PORT=42074&&set SKYSAGA_HOME_PORT=42069&&set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&SkySaga.Game.exe"

echo Starting the home world on UDP 42069...
start "SkySaga Game" /d "%BIN%" cmd /k "set SKYSAGA_COMPLETE_CHALLENGES=1&&set SKYSAGA_DATA2_UNIFORM=fd&&set SKYSAGA_SOCIAL_HUB_PORT=42070&&set SKYSAGA_FTUE_PORT=42071&&set SKYSAGA_MASTERY_FOREST_PORT=42072&&set SKYSAGA_MASTERY_DESERT_PORT=42073&&set SKYSAGA_MASTERY_JUNGLE_PORT=42074&&set SKYSAGA_ADVENTURE_SUPPLIES_CHESTS=1&&set SKYSAGA_CRAFTING_TEST_ROW=1&&set SKYSAGA_BIOME_INDEX=3&&set SKYSAGA_REGION_INDEX=23&&set SKYSAGA_ADVENTURE_INDEX=88&&set SKYSAGA_PALETTE=34&&SkySaga.Game.exe"

echo Home, City, Old City, and three Mastery test worlds are starting in separate windows.
timeout /t 5
