# Kingdom Come: Together 0.18.2 - dedicated WORLD HOST (Steam, Modding Tools build).
#
# STATUS: UNTESTED. Written on a Game Pass machine, which cannot run the
# Modding Tools build. The Lua it drives is unit-tested (tools\Test-GamePassLua.py);
# this script itself has never been run. HOST-WORLD-GUIDE.md lists what to
# verify, in order.
#
# What it does:
#   1. installs the mod into <ModdingTools>\Mods\kdcmp (game must be closed),
#   2. starts the relay on this machine (unless -NoRelay),
#   3. starts the Modding Tools game,
#   4. waits until a save is loaded (the debug API on :1403 reports a world clock),
#   5. tells the mod this game is the world host and that its player should
#      follow the guests,
#   6. runs the agent under a name starting with "[HOST]", which is how every
#      other player's game knows to become a guest and not show this player.
param(
    [string] $GameExe    = '',          # Modding Tools KingdomCome.exe; found through Steam when empty
    [int]    $RelayPort  = 7778,
    [string] $HostName   = '[HOST] world',
    [switch] $NoRelay,                  # a relay is already running elsewhere / as a service
    [switch] $NoFollow,                 # do not move the host's player after the guests
    [int]    $LoadTimeoutMinutes = 15
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'KcdmpCommon.ps1')
$api  = 'http://localhost:1403'

if (-not $HostName.StartsWith('[HOST]')) { throw 'HostName must start with "[HOST]" - the guests look for that prefix.' }

function Invoke-GameLua([string] $lua) {
    $cmd = [Uri]::EscapeDataString("#$lua")
    $null = Invoke-WebRequest -Uri "$api/api/System/Console/ExecuteString?command=$cmd" -UseBasicParsing -TimeoutSec 5
}

function Test-WorldLoaded {
    try {
        $xml = (Invoke-WebRequest -Uri "$api/api/rpg/Calendar?depth=1" -UseBasicParsing -TimeoutSec 3).Content
        # Same read as the agent's IsGameReadyAsync: GameTime="<seconds>" above zero.
        return ($xml -match 'GameTime="([^"]+)"' -and [double]$Matches[1] -gt 0)
    } catch { return $false }
}

if (-not $GameExe) { $GameExe = Find-ModdingToolsExe }
if (-not $GameExe -or -not (Test-Path -LiteralPath $GameExe)) {
    throw 'Could not find the KCD2 Modding Tools KingdomCome.exe. Pass it with -GameExe.'
}
Write-Host "Modding Tools game: $GameExe"

# 1. Mod (only while the game is closed - it keeps the pak open).
$modDir = Get-SteamModDir $GameExe
$running = Test-GameRunning
if (-not $running) {
    Install-Mod $modDir
    Write-Host "Mod installed to $modDir"
} elseif (-not (Test-ModCurrent $modDir)) {
    throw 'The game is running with a different version of the mod. Quit it, then run this again.'
}

# 2. Relay.
if (-not $NoRelay) {
    if (Get-NetTCPConnection -LocalPort $RelayPort -State Listen -ErrorAction SilentlyContinue) {
        Write-Host "A relay is already listening on port $RelayPort."
    } else {
        Write-Host "Starting the relay on port $RelayPort..."
        Start-Process -FilePath (Join-Path $here 'relay\KcdMpServer.exe') -ArgumentList '--port', $RelayPort -WorkingDirectory (Join-Path $here 'relay')
    }
}

# 3. Game.
if (-not $running) {
    Write-Host 'Starting the Modding Tools game...'
    Start-Process -FilePath $GameExe -WorkingDirectory (Split-Path -Parent $GameExe)
}

# 4. Wait for a loaded save.
Write-Host "Load a save in the game (waiting up to $LoadTimeoutMinutes minutes)..."
$deadline = (Get-Date).AddMinutes($LoadTimeoutMinutes)
while (-not (Test-WorldLoaded)) {
    if ((Get-Date) -gt $deadline) { throw 'No save was loaded in time (the debug API on :1403 never reported a world clock).' }
    Start-Sleep -Seconds 3
}
Write-Host 'World loaded.'

# 5. Role and follow.
Invoke-GameLua 'KCD2MP_SetWorldRole("host")'
if (-not $NoFollow) { Invoke-GameLua 'KCD2MP_SetWorldHostFollow("on")' }
Write-Host 'This game is the world host.'

# 6. Agent. --hosting only changes the Discord text; voice and Discord are off
#    because nobody is sitting at this machine.
Write-Host "Connecting to the relay as '$HostName'."
& (Join-Path $here 'agent\KcdMpClient.exe') --host 127.0.0.1 --port $RelayPort --name $HostName --hosting --no-voice --no-discord
