param(
    [Parameter(Mandatory = $true)]
    [string] $BinDirectory
)

$ErrorActionPreference = 'Stop'
$serverExecutable = Join-Path $BinDirectory 'SkySaga.Game.exe'

if (-not (Test-Path -LiteralPath $serverExecutable -PathType Leaf)) {
    Write-Error "Game server executable not found: $serverExecutable"
    exit 1
}

$sharedEnvironment = @{
    SKYSAGA_WORLD = 'adventure'
    SKYSAGA_HOME_PORT = '42069'
    SKYSAGA_SOCIAL_HUB_PORT = '42070'
    SKYSAGA_FTUE_PORT = '42071'
    SKYSAGA_MASTERY_FOREST_PORT = '42072'
    SKYSAGA_MASTERY_DESERT_PORT = '42073'
    SKYSAGA_MASTERY_JUNGLE_PORT = '42074'
    SKYSAGA_COMPLETE_CHALLENGES = '1'
    SKYSAGA_DATA2_UNIFORM = 'fd'
}

$adventures = @(
    @{
        Name = 'The Old City'
        Adventure = 'ftue'
        Map = 'WorldMap.json'
        Port = '42071'
        Log = 'server-adventure.log'
    },
    @{
        Name = 'Forest Mastery'
        Adventure = 'mastery-forest'
        InstanceId = 'mastery-forest'
        Map = 'AdventureForest01.json'
        Port = '42072'
        Log = 'server-mastery-forest.log'
        SolidFloor = '1'
    },
    @{
        Name = 'Desert Mastery'
        Adventure = 'mastery-desert'
        InstanceId = 'mastery-desert'
        Map = 'AdventureDesert01.json'
        Port = '42073'
        Log = 'server-mastery-desert.log'
        SolidFloor = '1'
    },
    @{
        Name = 'Jungle Mastery'
        Adventure = 'mastery-jungle'
        InstanceId = 'mastery-jungle'
        Map = 'AdventureJungle01.json'
        Port = '42074'
        Log = 'server-mastery-jungle.log'
        SolidFloor = '1'
    }
)

$processes = @()
foreach ($adventure in $adventures) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($serverExecutable)
    $startInfo.WorkingDirectory = $BinDirectory
    $startInfo.UseShellExecute = $false

    foreach ($entry in $sharedEnvironment.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = $entry.Value
    }

    $instanceEnvironment = @{
        SKYSAGA_INSTANCE_LOG = $adventure.Log
        SKYSAGA_MAP = $adventure.Map
        SKYSAGA_ADVENTURE = $adventure.Adventure
        SKYSAGA_GAME_PORT = $adventure.Port
    }
    if ($adventure.ContainsKey('InstanceId')) {
        $instanceEnvironment.SKYSAGA_INSTANCE_ID = $adventure.InstanceId
    }
    if ($adventure.ContainsKey('SolidFloor')) {
        $instanceEnvironment.SKYSAGA_SOLID_FLOOR = $adventure.SolidFloor
    }
    foreach ($entry in $instanceEnvironment.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = $entry.Value
    }

    try {
        $process = [System.Diagnostics.Process]::Start($startInfo)
        $processes += $process
        Write-Host ("Started {0} on UDP {1} (PID {2}; log: {3})" -f `
            $adventure.Name, $adventure.Port, $process.Id, $adventure.Log)
    }
    catch {
        Write-Error ("Could not start {0} on UDP {1}: {2}" -f `
            $adventure.Name, $adventure.Port, $_.Exception.Message)
    }
}

if ($processes.Count -eq 0) {
    Write-Error 'No adventure servers were started.'
    exit 1
}

Write-Host 'Adventure servers share this console. Their detailed logs remain separate in this folder.'
Write-Host 'Close this window to stop the adventure processes.'
while (@($processes | Where-Object { -not $_.HasExited }).Count -gt 0) {
    Start-Sleep -Seconds 1
}
Write-Host 'All adventure server processes have exited.'
